using Ryujinx.Common.Helper;
using Ryujinx.Common.Logging;
using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres.Proxy
{
    static class DnsBlacklist
    {
        public static bool IsHostBlocked(string host)
        {
            foreach (Regex regex in Patterns.BlockedHosts)
            {
                if (regex.IsMatch(host))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Decides whether a lookup may proceed, and to where.
        /// </summary>
        /// <param name="host">The hostname the guest asked for</param>
        /// <param name="redirectToPrivateServers">
        /// Whether the hosts file on the virtual SD card may override the blacklist. See
        /// <see cref="HleConfiguration.RedirectNintendoServers"/>.
        /// </param>
        /// <param name="privateServerAddress">
        /// A single fallback address for a blocked host the hosts file does not more
        /// specifically cover, or empty for none. See
        /// <see cref="HleConfiguration.PrivateServerAddress"/>.
        /// </param>
        /// <param name="privateServerNatCheckSecondaryAddress">
        /// Where the <c>nncs2</c> NAT-check host resolves instead of
        /// <paramref name="privateServerAddress"/>. See
        /// <see cref="HleConfiguration.PrivateServerNatCheckSecondaryAddress"/>.
        /// </param>
        /// <param name="redirect">Where the host resolves to, when it is allowed through</param>
        /// <returns>True if the lookup is allowed</returns>
        /// <remarks>
        /// The blacklist exists to keep the guest away from Nintendo's own servers, and it is
        /// checked before either redirect mechanism — so a blacklisted host is refused even when
        /// the operator has pointed it at their own machine. That is right by default and wrong
        /// for the one case these options exist for: a private replacement for those servers,
        /// which lives at <em>exactly</em> those hostnames because that is what the guest asks
        /// for.
        ///
        /// Both mechanisms are deliberately narrow in the same way: neither can point the guest
        /// anywhere outside the curated set of Nintendo-online-service hostname shapes
        /// <see cref="Patterns.BlockedHosts"/> already matches. A host with no hosts-file entry
        /// and no fallback address configured stays refused. That is the whole safety property
        /// here: this is a redirect, not a general unblocking.
        /// </remarks>
        public static bool TryAllow(
            string host,
            bool redirectToPrivateServers,
            string privateServerAddress,
            string privateServerNatCheckSecondaryAddress,
            out IPHostEntry redirect)
        {
            redirect = null;

            if (!IsHostBlocked(host))
            {
                return true;
            }

            if (redirectToPrivateServers &&
                DnsMitmResolver.Instance.TryResolveRedirect(host, out redirect))
            {
                Logger.Info?.Print(LogClass.ServiceSfdnsres,
                    $"Blocked host '{host}' redirected to {redirect.AddressList[0]} by the hosts file");

                return true;
            }

            if (TryResolveFallbackAddress(host, privateServerAddress, privateServerNatCheckSecondaryAddress, out IPAddress fallback))
            {
                redirect = new IPHostEntry
                {
                    HostName = host,
                    Aliases = [],
                    AddressList = [fallback],
                };

                Logger.Info?.Print(LogClass.ServiceSfdnsres,
                    $"Blocked host '{host}' redirected to {fallback} by the configured private server address");

                return true;
            }

            // Blocked, and neither mechanism names this host. Refused, so that configuring
            // either one does not become a blanket unblock.
            redirect = null;

            return false;
        }

        /// <summary>
        /// The single configured address a blocked host without a more specific hosts-file
        /// entry falls back to, or false when none is configured.
        /// </summary>
        /// <remarks>
        /// <c>nncs2</c> is excluded from <paramref name="privateServerAddress"/> and answered
        /// from <paramref name="privateServerNatCheckSecondaryAddress"/> instead: Pia probes
        /// <c>nncs1</c> and <c>nncs2</c> as two separate servers and one NAT-check message type
        /// must be answered from a DIFFERENT address, or the console de-duplicates the probe and
        /// NAT detection never completes. Answering both from the same address is a real, silent
        /// failure mode, not a theoretical one -- see docs/private-servers.md.
        /// </remarks>
        private static bool TryResolveFallbackAddress(
            string host,
            string privateServerAddress,
            string privateServerNatCheckSecondaryAddress,
            out IPAddress address)
        {
            if (host.StartsWith("nncs2", StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.TryParse(privateServerNatCheckSecondaryAddress, out address);
            }

            return IPAddress.TryParse(privateServerAddress, out address);
        }
    }
}
