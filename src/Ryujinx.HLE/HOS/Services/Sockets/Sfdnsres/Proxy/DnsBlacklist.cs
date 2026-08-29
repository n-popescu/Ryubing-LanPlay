using Ryujinx.Common.Helper;
using Ryujinx.Common.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres.Proxy
{
    static class DnsBlacklist
    {
        private static readonly IPAddress RedirectAddress =
            IPAddress.Parse("9.205.104.23");

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
        /// Kept for API compatibility with the existing caller.
        /// </param>
        /// <param name="redirect">
        /// The redirect address when the host is blocked.
        /// </param>
        /// <returns>True if the lookup is allowed</returns>
        public static bool TryAllow(
            string host,
            bool redirectToPrivateServers,
            out IPHostEntry redirect)
        {
            redirect = null;

            // Hosts which aren't blocked continue through normal DNS resolution.
            if (!IsHostBlocked(host))
            {
                return true;
            }

            // Every blocked host is redirected to the private server.
            redirect = new IPHostEntry
            {
                HostName = host,
                Aliases = [],
                AddressList = [RedirectAddress]
            };

            Logger.Info?.Print(
                LogClass.ServiceSfdnsres,
                $"Blocked host '{host}' redirected to {RedirectAddress}");

            return true;
        }
    }
}
