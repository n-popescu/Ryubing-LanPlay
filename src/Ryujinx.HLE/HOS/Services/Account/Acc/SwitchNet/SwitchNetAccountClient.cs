using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.HLE.HOS.Services.Account.Acc.SwitchNet
{
    /// <summary>
    /// Logs into a self-hosted SwitchNet server and returns the BAAS id token a
    /// game needs to reach the game server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On real hardware the console's own account sysmodule does this: it presents a
    /// device certificate to dauth, exchanges the resulting device token for an
    /// anonymous BAAS token, and logs in with the device account id and password held
    /// in its save data. An emulator has none of that, so Ryujinx fabricates an id
    /// token locally -- correct in shape, signed with a throwaway key nothing can
    /// verify. That is fine against a server that never checks, and useless against
    /// one that does.
    /// </para>
    /// <para>
    /// This performs the same three calls a console makes, against the operator's own
    /// server, and returns a token that server actually signed:
    /// </para>
    /// <list type="number">
    /// <item>POST dauth <c>/v6/device_auth_token</c> -- a device token. SwitchNet
    /// accepts any device certificate by design, so there is nothing to present;
    /// this is a deliberate decision on its side, not a gap on ours.</item>
    /// <item>POST BAAS <c>/1.0.0/application/token</c> -- exchanges that for an
    /// anonymous access token.</item>
    /// <item>POST BAAS <c>/1.0.0/login</c> -- the device account id and password,
    /// returning the id token.</item>
    /// </list>
    /// <para>
    /// No Nintendo credential is presented, verified or forged anywhere in this: every
    /// token involved is one the operator's own server signed with its own key.
    /// </para>
    /// </remarks>
    public sealed class SwitchNetAccountClient : IDisposable
    {
        /// <summary>The hostname a console uses for dauth. A protocol fact, not a deployment one.</summary>
        private const string DAuthHost = "dauth-lp1.ndas.srv.nintendo.net";

        /// <summary>The hostname a console uses for BAAS.</summary>
        private const string BaasHost = "e0d67c509fb203858ebcb2fe3f88c2aa.baas.nintendo.com";

        /// <summary>
        /// The client id a console asks dauth for a token for.
        /// </summary>
        /// <remarks>
        /// SwitchNet does not check it -- it is echoed into the token's audience -- but
        /// sending the value a console sends keeps a capture of this traffic
        /// comparable with one taken from hardware.
        /// </remarks>
        private const string BaasClientId = "8f849b5d34778d8e";

        /// <summary>
        /// How long before a token actually expires it is treated as expired.
        /// </summary>
        /// <remarks>
        /// A token that expires while a match is being set up fails in the game rather
        /// than here, where the reason would be legible. Refreshing early costs one
        /// request nobody notices.
        /// </remarks>
        private static readonly TimeSpan _expiryMargin = TimeSpan.FromMinutes(5);

        private readonly HttpClient _http;
        private readonly SwitchNetAccountOptions _options;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private string _idToken;
        private DateTime _idTokenExpiry;

        public SwitchNetAccountClient(SwitchNetAccountOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _options = options;

            SocketsHttpHandler handler = new()
            {
                // A system proxy must not be consulted. The SwitchNet server is on the
                // operator's own network and reached by an address they typed in; an
                // ambient HTTPS_PROXY would have .NET try to CONNECT through it and
                // fail in a way that names the proxy rather than anything here.
                UseProxy = false,
                Proxy = null,

                // Every request keeps its Nintendo hostname in the URI -- so the Host
                // header and the TLS SNI are both what the server expects -- while the
                // socket is dialled at the operator's address instead. SwitchNet's edge
                // routes on exactly those two things, so a client that connects by IP
                // and does not set them lands on whichever server block nginx happens
                // to list first.
                ConnectCallback = async (ctx, ct) =>
                {
                    Socket socket = new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(options.ServerEndPoint, ct).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                },

                SslOptions = new SslClientAuthenticationOptions
                {
                    // A self-hosted server presents a certificate signed by the
                    // operator's own CA, which this machine has no reason to trust.
                    // Accepted here and ONLY here: this handler is used for nothing but
                    // the operator's own SwitchNet server, at an address they typed in
                    // themselves. It does not widen what anything else in the emulator
                    // trusts.
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                },
            };

            _http = new HttpClient(handler) { Timeout = options.Timeout };
        }

        /// <summary>
        /// Returns a valid id token, logging in only if the cached one is missing or
        /// close to expiry.
        /// </summary>
        public async Task<string> GetIdTokenAsync(CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_idToken != null && DateTime.UtcNow + _expiryMargin < _idTokenExpiry)
                {
                    return _idToken;
                }

                LoginResult result = await LoginAsync(cancellationToken).ConfigureAwait(false);

                _idToken = result.IdToken;
                _idTokenExpiry = result.ExpiresAt;

                return _idToken;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Performs the full three-call login, ignoring any cached token.</summary>
        public async Task<LoginResult> LoginAsync(CancellationToken cancellationToken)
        {
            string deviceToken = await PostFormAsync(
                $"https://{DAuthHost}/v6/device_auth_token",
                null,
                new Dictionary<string, string> { ["client_id"] = BaasClientId },
                "device_auth_token",
                cancellationToken).ConfigureAwait(false);

            string accessToken = await PostFormAsync(
                $"https://{BaasHost}/1.0.0/application/token",
                null,
                new Dictionary<string, string>
                {
                    ["grantType"] = "public_client",
                    ["assertion"] = deviceToken,
                },
                "accessToken",
                cancellationToken).ConfigureAwait(false);

            using HttpResponseMessage response = await PostFormRawAsync(
                $"https://{BaasHost}/1.0.0/login",
                accessToken,
                new Dictionary<string, string>
                {
                    ["id"] = _options.DeviceAccountId,
                    ["password"] = _options.Password,
                },
                cancellationToken).ConfigureAwait(false);

            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // BAAS answers a wrong device account or password with a specific
                // errorCode, and that string is the whole diagnosis -- surfacing it
                // is the difference between "login failed" and "you typed the
                // password wrong".
                throw new SwitchNetLoginException(
                    $"login was refused ({(int)response.StatusCode}): {DescribeError(body)}");
            }

            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("idToken", out JsonElement idTokenElement))
            {
                throw new SwitchNetLoginException("the login response carried no idToken");
            }

            string idToken = idTokenElement.GetString() ?? "";
            if (string.IsNullOrEmpty(idToken))
            {
                throw new SwitchNetLoginException("the login response carried an empty idToken");
            }

            string userId = "";
            string nickname = "";
            if (root.TryGetProperty("user", out JsonElement user))
            {
                userId = (user.TryGetProperty("id", out JsonElement id) ? id.GetString() : null) ?? "";
                nickname = (user.TryGetProperty("nickname", out JsonElement n) ? n.GetString() : null) ?? "";
            }

            // The token's own expiry, not the response's expiresIn: expiresIn is the
            // ACCESS token's lifetime, and the id token is a different token with a
            // different one. Using the wrong number here means either refreshing
            // pointlessly or handing the game a token that has already expired.
            DateTime expiresAt = ReadExpiry(idToken);

            return new LoginResult(idToken, userId, nickname, expiresAt);
        }

        /// <summary>Reads the 'exp' claim without verifying the signature.</summary>
        /// <remarks>
        /// Unverified on purpose, and safe: this token is not a credential being
        /// checked, it is one this client just received over a connection it made, to
        /// be handed straight back to the server that signed it. The only thing read
        /// is when to ask for another. A conservative fallback covers a token whose
        /// expiry cannot be read at all.
        /// </remarks>
        private static DateTime ReadExpiry(string jwt)
        {
            try
            {
                string[] parts = jwt.Split('.');
                if (parts.Length == 3)
                {
                    string payload = parts[1].Replace('-', '+').Replace('_', '/');
                    payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

                    using JsonDocument document = JsonDocument.Parse(Convert.FromBase64String(payload));
                    if (document.RootElement.TryGetProperty("exp", out JsonElement exp)
                        && exp.TryGetInt64(out long seconds))
                    {
                        return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to the conservative default below.
            }

            return DateTime.UtcNow + TimeSpan.FromMinutes(30);
        }

        private async Task<string> PostFormAsync(
            string url,
            string bearer,
            Dictionary<string, string> form,
            string field,
            CancellationToken cancellationToken)
        {
            using HttpResponseMessage response =
                await PostFormRawAsync(url, bearer, form, cancellationToken).ConfigureAwait(false);

            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new SwitchNetLoginException(
                    $"{new Uri(url).Host} answered {(int)response.StatusCode}: {DescribeError(body)}");
            }

            using JsonDocument document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty(field, out JsonElement element))
            {
                throw new SwitchNetLoginException($"{new Uri(url).Host} returned no '{field}'");
            }

            string value = element.GetString() ?? "";
            if (string.IsNullOrEmpty(value))
            {
                throw new SwitchNetLoginException($"{new Uri(url).Host} returned an empty '{field}'");
            }

            return value;
        }

        private async Task<HttpResponseMessage> PostFormRawAsync(
            string url,
            string bearer,
            Dictionary<string, string> form,
            CancellationToken cancellationToken)
        {
            HttpRequestMessage request = new(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(form),
            };
            if (bearer != null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
            }

            try
            {
                return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                throw new SwitchNetLoginException(
                    $"could not reach {new Uri(url).Host} at {_options.ServerEndPoint}: {e.Message}", e);
            }
            catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SwitchNetLoginException(
                    $"{new Uri(url).Host} at {_options.ServerEndPoint} did not answer within {_options.Timeout.TotalSeconds:0}s", e);
            }
            finally
            {
                request.Dispose();
            }
        }

        /// <summary>Pulls the errorCode out of a BAAS error body, or falls back to the body.</summary>
        private static string DescribeError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "(no body)";
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("errorCode", out JsonElement code))
                {
                    return code.GetString() ?? "(unnamed error)";
                }
            }
            catch (JsonException)
            {
                // Not JSON. The raw body is still the most useful thing to say.
            }

            return body.Length > 200 ? body[..200] + "..." : body;
        }

        public void Dispose()
        {
            _http.Dispose();
            _lock.Dispose();
        }
    }

    public sealed class SwitchNetAccountOptions
    {
        /// <summary>Where the SwitchNet edge actually is. Every request is dialled here.</summary>
        public EndPoint ServerEndPoint { get; init; }

        public string DeviceAccountId { get; init; }
        public string Password { get; init; }

        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    }

    public sealed record LoginResult(string IdToken, string UserId, string Nickname, DateTime ExpiresAt);

    /// <summary>A login that failed for a reason worth telling the operator.</summary>
    public sealed class SwitchNetLoginException : Exception
    {
        public SwitchNetLoginException(string message) : base(message) { }
        public SwitchNetLoginException(string message, Exception inner) : base(message, inner) { }
    }
}
