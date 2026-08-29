using Ryujinx.Common.Helper;
using Ryujinx.Common.Logging;
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
        /// Whether the hosts file on the virtual SD card may override the blacklist
        /// </param>
        /// <param name="redirect">The hosts file's answer, when there is one</param>
        /// <returns>True if the lookup is allowed</returns>
        /// <remarks>
        /// The blacklist exists to keep the guest away from Nintendo's own servers, and it is
        /// checked before the hosts file — so a blacklisted host is refused even when the operator
        /// has pointed it at their own machine. That is right by default and wrong for the one case
        /// this option exists for: a private replacement for those servers, which lives at
        /// <em>exactly</em> those hostnames because that is what the guest asks for.
        ///
        /// The override is deliberately narrow. It applies only to a host the hosts file actually
        /// names, so turning it on cannot open a path to real Nintendo for anything else — the
        /// blacklist still refuses every blocked host with no entry. That distinction is the whole
        /// safety property here: this is a redirect, not a general unblocking.
        /// </remarks>
        public static bool TryAllow(string host, bool redirectToPrivateServers, out IPHostEntry redirect)
        {
            redirect = null;

            if (!IsHostBlocked(host))
            {
                return true;
            }

            if (!redirectToPrivateServers)
            {
                return false;
            }

            if (!DnsMitmResolver.Instance.TryResolveRedirect(host, out redirect))
            {
                // Blocked, and the hosts file says nothing about it. Refused, so that enabling
                // redirection does not become a blanket unblock.
                return false;
            }

            Logger.Info?.Print(LogClass.ServiceSfdnsres,
                $"Blocked host '{host}' redirected to {redirect.AddressList[0]} by the hosts file");

            return true;
        }
    }
}
