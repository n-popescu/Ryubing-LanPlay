using Ryujinx.HLE.HOS.Services.Sockets.Bsd;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Impl;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy;
using Ryujinx.HLE.HOS.Services.Ssl.Types;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Ryujinx.HLE.HOS.Services.Ssl.SslService
{
    class SslManagedSocketConnection : ISslConnectionBase
    {
        public int SocketFd { get; }

        public ISocket Socket { get; }

        private readonly BsdContext _bsdContext;
        private readonly SslVersion _sslVersion;
        /// <summary>
        /// Path to a PEM bundle of certificate authorities to trust in ADDITION to the host's, for
        /// a private replacement of Nintendo's servers. Empty means ordinary validation.
        /// </summary>
        private readonly string _privateServerCaBundle;
        private SslStream _stream;
        private bool _isBlockingSocket;
        private int _previousReadTimeout;

        public SslManagedSocketConnection(
            BsdContext bsdContext,
            SslVersion sslVersion,
            int socketFd,
            ISocket socket,
            string privateServerCaBundle = null)
        {
            _bsdContext = bsdContext;
            _sslVersion = sslVersion;
            _privateServerCaBundle = privateServerCaBundle;

            SocketFd = socketFd;
            Socket = socket;
        }

        private void StartSslOperation()
        {
            // Save blocking state
            _isBlockingSocket = Socket.Blocking;

            // Force blocking for SslStream
            Socket.Blocking = true;
        }

        private void EndSslOperation()
        {
            // Restore blocking state
            Socket.Blocking = _isBlockingSocket;
        }

        private void StartSslReadOperation()
        {
            StartSslOperation();

            if (!_isBlockingSocket)
            {
                _previousReadTimeout = _stream.ReadTimeout;

                _stream.ReadTimeout = 1;
            }
        }

        private void EndSslReadOperation()
        {
            if (!_isBlockingSocket)
            {
                _stream.ReadTimeout = _previousReadTimeout;
            }

            EndSslOperation();
        }

        // NOTE: We silence warnings about TLS 1.0 and 1.1 as games will likely use it.
#pragma warning disable SYSLIB0039
        private SslProtocols TranslateSslVersion(SslVersion version)
        {
            return (version & SslVersion.VersionMask) switch
            {
                SslVersion.Auto => SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13,
                SslVersion.TlsV10 => SslProtocols.Tls,
                SslVersion.TlsV11 => SslProtocols.Tls11,
                SslVersion.TlsV12 => SslProtocols.Tls12,
                SslVersion.TlsV13 => SslProtocols.Tls13,
                _ => throw new NotImplementedException(version.ToString()),
            };
        }
#pragma warning restore SYSLIB0039

        /// <summary>
        /// Retrieve the hostname of the current remote in case the provided hostname is null or empty.
        /// </summary>
        /// <param name="hostName">The current hostname</param>
        /// <returns>Either the resolved or provided hostname</returns>
        /// <remarks>
        /// This is done to avoid getting an <see cref="System.Security.Authentication.AuthenticationException"/>
        /// as the remote certificate will be rejected with <c>RemoteCertificateNameMismatch</c> due to an empty hostname.
        /// This is not what the switch does!
        /// It might just skip remote hostname verification if the hostname wasn't set with <see cref="ISslConnection.SetHostName"/> before.
        /// TODO: Remove this as soon as we know how the switch deals with empty hostnames
        /// </remarks>
        private string RetrieveHostName(string hostName)
        {
            if (!string.IsNullOrEmpty(hostName))
            {
                return hostName;
            }

            try
            {
                return Dns.GetHostEntry(Socket.RemoteEndPoint.Address).HostName;
            }
            catch (SocketException)
            {
                return hostName;
            }
        }

        public ResultCode Handshake(string hostName)
        {
            StartSslOperation();

            // The socket is not always backed by a host socket: with LAN Play or RyuLDN selected it is a
            // virtual one, and casting it to DefaultSocket used to throw an InvalidCastException here,
            // which surfaced as the game failing to reach the online service.
            ISocketImpl socketImpl = ((ManagedSocket)Socket).Socket;

            Stream socketStream = socketImpl is DefaultSocket hostSocket
                ? new NetworkStream(hostSocket.BaseSocket, false)
                : new SocketImplStream(socketImpl);

            // A validation callback ONLY when extra certificate authorities are configured, and
            // that callback adds trust rather than removing checks -- a name mismatch or an
            // expired certificate still fails. Null keeps .NET's default validation, which is
            // what every connection gets when the option is off. See PrivateServerTrust.
            _stream = new SslStream(socketStream, false,
                PrivateServerTrust.BuildCallback(_privateServerCaBundle), null);
            hostName = RetrieveHostName(hostName);
            _stream.AuthenticateAsClient(hostName, null, TranslateSslVersion(_sslVersion), false);
            EndSslOperation();

            return ResultCode.Success;
        }

        public ResultCode Peek(out int peekCount, Memory<byte> buffer)
        {
            // NOTE: We cannot support that on .NET SSL API.
            // As Nintendo's curl implementation detail check if a connection is alive via Peek, we just return that it would block to let it know that it's alive.
            peekCount = -1;

            return ResultCode.WouldBlock;
        }

        public int Pending()
        {
            // Unsupported
            return 0;
        }

        private bool TryTranslateWinSockError(bool isBlocking, WsaError error, out ResultCode resultCode)
        {
            switch (error)
            {
                case WsaError.WSAETIMEDOUT:
                    resultCode = isBlocking ? ResultCode.Timeout : ResultCode.WouldBlock;
                    return true;
                case WsaError.WSAECONNABORTED:
                    resultCode = ResultCode.ConnectionAbort;
                    return true;
                case WsaError.WSAECONNRESET:
                    resultCode = ResultCode.ConnectionReset;
                    return true;
                default:
                    resultCode = ResultCode.Success;
                    return false;
            }
        }

        public ResultCode Read(out int readCount, Memory<byte> buffer)
        {
            if (!Socket.Poll(0, SelectMode.SelectRead))
            {
                readCount = -1;

                return ResultCode.WouldBlock;
            }

            StartSslReadOperation();

            try
            {
                readCount = _stream.Read(buffer.Span);
            }
            catch (IOException exception)
            {
                readCount = -1;

                if (exception.InnerException is SocketException socketException)
                {
                    WsaError socketErrorCode = (WsaError)socketException.SocketErrorCode;

                    if (TryTranslateWinSockError(_isBlockingSocket, socketErrorCode, out ResultCode result))
                    {
                        return result;
                    }
                    else
                    {
                        throw socketException;
                    }
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                EndSslReadOperation();
            }

            return ResultCode.Success;
        }

        public ResultCode Write(out int writtenCount, ReadOnlyMemory<byte> buffer)
        {
            if (!Socket.Poll(0, SelectMode.SelectWrite))
            {
                writtenCount = 0;

                return ResultCode.WouldBlock;
            }

            StartSslOperation();

            try
            {
                _stream.Write(buffer.Span);
            }
            catch (IOException exception)
            {
                writtenCount = -1;

                if (exception.InnerException is SocketException socketException)
                {
                    WsaError socketErrorCode = (WsaError)socketException.SocketErrorCode;

                    if (TryTranslateWinSockError(_isBlockingSocket, socketErrorCode, out ResultCode result))
                    {
                        return result;
                    }
                    else
                    {
                        throw socketException;
                    }
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                EndSslOperation();
            }

            // .NET API doesn't provide the size written, assume all written.
            writtenCount = buffer.Length;

            return ResultCode.Success;
        }

        public ResultCode GetServerCertificate(string hostname, Span<byte> certificates, out uint storageSize, out uint certificateCount)
        {
            byte[] rawCertData = _stream.RemoteCertificate.GetRawCertData();

            storageSize = (uint)rawCertData.Length;
            certificateCount = 1;

            if (rawCertData.Length > certificates.Length)
            {
                return ResultCode.CertBufferTooSmall;
            }

            rawCertData.CopyTo(certificates);

            return ResultCode.Success;
        }

        public void Dispose()
        {
            _bsdContext.CloseFileDescriptor(SocketFd);
        }
    }
}
