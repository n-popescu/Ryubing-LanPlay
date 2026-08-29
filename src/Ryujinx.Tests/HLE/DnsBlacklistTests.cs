using NUnit.Framework;
using Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres.Proxy;
using System.Net;

namespace Ryujinx.Tests.HLE
{
    /// <summary>
    /// Tests for the DNS block list, and for the two independent ways a blocked host may be
    /// redirected instead: the hosts file on the virtual SD card, and a single configured
    /// fallback address.
    /// </summary>
    /// <remarks>
    /// The block list is checked before either mechanism, which is right by default and wrong
    /// for one case: a private replacement for Nintendo's servers lives at exactly those
    /// hostnames, because that is what the guest asks for. The property worth protecting for
    /// both mechanisms is that they are <em>narrow</em> — a blocked host neither one names stays
    /// blocked, so turning either one on is a redirect and not a general unblocking.
    /// </remarks>
    public class DnsBlacklistTests
    {
        // Blocked by Patterns.BlockedHosts: the Splatoon 3 NPLN tenant and Nintendo's account
        // server. Both are hosts a private server would serve.
        private const string BlockedTenant = "t-dce9377b-lp1.lp1.t.npln.srv.nintendo.net";
        private const string BlockedAccounts = "accounts.nintendo.com";

        // Also blocked (matches Patterns.DomainLp1Ns), and the one host with its own address:
        // Pia's NAT-check requires it to answer from a SECOND, DIFFERENT address than everything
        // else, or the console de-duplicates the probe and NAT detection never completes.
        private const string BlockedNatCheckSecondary = "nncs2-lp1.n.n.srv.nintendo.net";

        // Not blocked, and served by a private server all the same.
        private const string AllowedDAuth = "dauth-lp1.ndas.srv.nintendo.net";

        private const string PrimaryAddress = "192.168.1.50";
        private const string SecondaryAddress = "192.168.1.51";

        [Test]
        public void TheBlockListStillBlocksWithNeitherMechanismConfigured()
        {
            Assert.That(DnsBlacklist.IsHostBlocked(BlockedTenant), Is.True);
            Assert.That(DnsBlacklist.IsHostBlocked(BlockedAccounts), Is.True);

            Assert.That(
                DnsBlacklist.TryAllow(BlockedTenant, redirectToPrivateServers: false, privateServerAddress: "", privateServerNatCheckSecondaryAddress: "", out _),
                Is.False,
                "a blocked host was allowed with both mechanisms off");
        }

        [Test]
        public void AnUnblockedHostIsAllowedAndNotPreResolved()
        {
            // Allowed, and with no redirect attached: the caller then resolves it the ordinary way,
            // which is what keeps the hosts file's own wildcard handling in one place.
            Assert.That(
                DnsBlacklist.TryAllow(AllowedDAuth, redirectToPrivateServers: false, privateServerAddress: "", privateServerNatCheckSecondaryAddress: "", out IPHostEntry entry),
                Is.True);
            Assert.That(entry, Is.Null);

            Assert.That(
                DnsBlacklist.TryAllow(AllowedDAuth, redirectToPrivateServers: true, privateServerAddress: PrimaryAddress, privateServerNatCheckSecondaryAddress: "", out entry),
                Is.True);
            Assert.That(entry, Is.Null);
        }

        [Test]
        public void ABlockedHostWithNoHostsFileEntryAndNoFallbackAddressStaysBlocked()
        {
            // The safety property, for the hosts-file mechanism specifically. Turning the
            // override on must not become a blanket unblock: a host nobody has pointed anywhere
            // is still refused, so the guest cannot reach Nintendo's own servers through it.
            //
            // No hosts file has been loaded in this test process, so every lookup is a miss.
            Assert.That(
                DnsBlacklist.TryAllow(BlockedTenant, redirectToPrivateServers: true, privateServerAddress: "", privateServerNatCheckSecondaryAddress: "", out _),
                Is.False,
                "the hosts-file override unblocked a host the hosts file says nothing about");

            Assert.That(
                DnsBlacklist.TryAllow(BlockedAccounts, redirectToPrivateServers: true, privateServerAddress: "", privateServerNatCheckSecondaryAddress: "", out _),
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

        [Test]
        public void AConfiguredFallbackAddressRedirectsABlockedHostWithNoHostsFileEntry()
        {
            // The fallback address is its own switch, independent of the hosts-file bool: an
            // operator who never ticks RedirectNintendoServers still gets redirected here.
            Assert.That(
                DnsBlacklist.TryAllow(BlockedTenant, redirectToPrivateServers: false, privateServerAddress: PrimaryAddress, privateServerNatCheckSecondaryAddress: "", out IPHostEntry entry),
                Is.True);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.AddressList, Is.EqualTo(new[] { IPAddress.Parse(PrimaryAddress) }));
            Assert.That(entry.HostName, Is.EqualTo(BlockedTenant),
                "the guest's TLS compares this against the server's certificate; the pattern that matched is not a hostname");
        }

        [Test]
        public void AnEmptyFallbackAddressLeavesAnUncoveredHostBlocked()
        {
            // The safety property for the fallback mechanism, mirroring the hosts-file one above:
            // an empty address is not configured, not a redirect to nowhere.
            Assert.That(
                DnsBlacklist.TryAllow(BlockedTenant, redirectToPrivateServers: false, privateServerAddress: "", privateServerNatCheckSecondaryAddress: "", out _),
                Is.False);

            Assert.That(
                DnsBlacklist.TryAllow(BlockedTenant, redirectToPrivateServers: false, privateServerAddress: "not-an-ip-address", privateServerNatCheckSecondaryAddress: "", out _),
                Is.False,
                "a hostname was accepted where only an IP literal should be -- the resolver that consumes this IS the one that intercepts DNS, so a hostname here would be circular");
        }

        [Test]
        public void TheNatCheckSecondaryHostGetsTheSecondaryAddressNotThePrimary()
        {
            // The one property that matters most for this field: nncs2 must NOT end up on the
            // same address as everything else, or Pia's NAT-check silently never completes.
            Assert.That(
                DnsBlacklist.TryAllow(BlockedNatCheckSecondary, redirectToPrivateServers: false, privateServerAddress: PrimaryAddress, privateServerNatCheckSecondaryAddress: SecondaryAddress, out IPHostEntry entry),
                Is.True);
            Assert.That(entry.AddressList, Is.EqualTo(new[] { IPAddress.Parse(SecondaryAddress) }));

            // A different blocked host, in the same call, still gets the primary address.
            Assert.That(
                DnsBlacklist.TryAllow(BlockedTenant, redirectToPrivateServers: false, privateServerAddress: PrimaryAddress, privateServerNatCheckSecondaryAddress: SecondaryAddress, out entry),
                Is.True);
            Assert.That(entry.AddressList, Is.EqualTo(new[] { IPAddress.Parse(PrimaryAddress) }));
        }

        [Test]
        public void TheNatCheckSecondaryHostStaysBlockedWithNoSecondaryAddressConfigured()
        {
            // Deliberately does NOT fall back to the primary address: answering nncs1 and nncs2
            // from the same address is the exact failure this field exists to prevent, so a
            // missing secondary address must not silently reuse the primary one.
            Assert.That(
                DnsBlacklist.TryAllow(BlockedNatCheckSecondary, redirectToPrivateServers: false, privateServerAddress: PrimaryAddress, privateServerNatCheckSecondaryAddress: "", out _),
                Is.False);
        }

        [Test]
        public void AHostsFileEntryWinsOverTheFallbackAddress()
        {
            // The hosts file is the more specific mechanism -- naming one host by hand should
            // not be overridden by the blunter, everything-blocked fallback. No hosts file is
            // loaded in this test process, so this exercises the ordering via the miss case: the
            // hosts-file branch is tried and fails before the fallback is tried at all, which
            // AConfiguredFallbackAddressRedirectsABlockedHostWithNoHostsFileEntry above already
            // shows takes over precisely when the hosts-file branch has nothing.
            Assert.That(
                DnsBlacklist.TryAllow(BlockedTenant, redirectToPrivateServers: true, privateServerAddress: PrimaryAddress, privateServerNatCheckSecondaryAddress: "", out IPHostEntry entry),
                Is.True);
            Assert.That(entry.AddressList, Is.EqualTo(new[] { IPAddress.Parse(PrimaryAddress) }),
                "fell through to the fallback address, which is only correct because the hosts file (loaded from an empty file in this process) has no entry for this host");
        }
    }
}
