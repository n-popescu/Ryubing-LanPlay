using NUnit.Framework;
using Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres.Proxy;
using System.Net;

namespace Ryujinx.Tests.HLE
{
    /// <summary>
    /// Tests for the DNS block list, and specifically for the one case where the hosts file on the
    /// virtual SD card is allowed to override it.
    /// </summary>
    /// <remarks>
    /// The block list is checked before the hosts file, which is right by default and wrong for one
    /// case: a private replacement for Nintendo's servers lives at exactly those hostnames, because
    /// that is what the guest asks for. The override exists for that, and the property worth
    /// protecting is that it is <em>narrow</em> — a blocked host with no hosts-file entry stays
    /// blocked, so turning it on is a redirect and not a general unblocking.
    /// </remarks>
    public class DnsBlacklistTests
    {
        // Blocked by Patterns.BlockedHosts: the Splatoon 3 NPLN tenant and Nintendo's account
        // server. Both are hosts a private server would serve.
        private const string BlockedTenant = "t-dce9377b-lp1.lp1.t.npln.srv.nintendo.net";
        private const string BlockedAccounts = "accounts.nintendo.com";

        // Not blocked, and served by a private server all the same.
        private const string AllowedDAuth = "dauth-lp1.ndas.srv.nintendo.net";

        [Test]
        public void TheBlockListStillBlocksWithoutTheOverride()
        {
            Assert.That(DnsBlacklist.IsHostBlocked(BlockedTenant), Is.True);
            Assert.That(DnsBlacklist.IsHostBlocked(BlockedAccounts), Is.True);

            Assert.That(
                DnsBlacklist.TryAllow(BlockedTenant, redirectToPrivateServers: false, out _),
                Is.False,
                "a blocked host was allowed with the override off");
        }

        [Test]
        public void AnUnblockedHostIsAllowedAndNotPreResolved()
        {
            // Allowed, and with no redirect attached: the caller then resolves it the ordinary way,
            // which is what keeps the hosts file's own wildcard handling in one place.
            Assert.That(
                DnsBlacklist.TryAllow(AllowedDAuth, redirectToPrivateServers: false, out IPHostEntry entry),
                Is.True);
            Assert.That(entry, Is.Null);

            Assert.That(
                DnsBlacklist.TryAllow(AllowedDAuth, redirectToPrivateServers: true, out entry),
                Is.True);
            Assert.That(entry, Is.Null);
        }

        [Test]
        public void ABlockedHostWithNoHostsFileEntryStaysBlocked()
        {
            // The safety property. Turning the override on must not become a blanket unblock: a
            // host nobody has pointed anywhere is still refused, so the guest cannot reach
            // Nintendo's own servers through it.
            //
            // No hosts file has been loaded in this test process, so every lookup is a miss.
            Assert.That(
                DnsBlacklist.TryAllow(BlockedTenant, redirectToPrivateServers: true, out _),
                Is.False,
                "the override unblocked a host the hosts file says nothing about");

            Assert.That(
                DnsBlacklist.TryAllow(BlockedAccounts, redirectToPrivateServers: true, out _),
                Is.False);
        }

        [Test]
        public void AHostsFileMissWithNoEntriesResolvesNothing()
        {
            // TryResolveRedirect must not fall back to real DNS: the block list decides whether a
            // lookup may happen at all, and a resolver that quietly answered from upstream would
            // make the decision meaningless.
            Assert.That(
                DnsMitmResolver.Instance.TryResolveRedirect(BlockedTenant, out IPHostEntry entry),
                Is.False);
            Assert.That(entry, Is.Null);
        }
    }
}
