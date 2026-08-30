using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Nifm.StaticService.Types
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 9)]
    struct DnsSetting
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool IsDynamicDnsEnabled;
        public IpV4Address PrimaryDns;
        public IpV4Address SecondaryDns;

        public DnsSetting(IPInterfaceProperties interfaceProperties)
        {
            IsDynamicDnsEnabled = OperatingSystem.IsWindows() && interfaceProperties.IsDynamicDnsEnabled;

            if (interfaceProperties.DnsAddresses.Count == 0)
            {
                PrimaryDns = new IpV4Address();
                SecondaryDns = new IpV4Address();
            }
            else
            {
                PrimaryDns = new IpV4Address(interfaceProperties.DnsAddresses[0]);
                SecondaryDns = new IpV4Address(interfaceProperties.DnsAddresses[interfaceProperties.DnsAddresses.Count > 1 ? 1 : 0]);
            }
        }

        /// <summary>
        /// Reports <paramref name="server"/> as both the primary and secondary DNS server.
        /// </summary>
        /// <remarks>
        /// Redirecting a blocked Nintendo hostname only works for lookups that go through the
        /// emulated <c>sfdnsres</c> service -- <see cref="Sockets.Sfdnsres.Proxy.DnsBlacklist"/>
        /// intercepts those directly, regardless of what DNS server the guest thinks it has. Some
        /// titles' own network stacks resolve at least part of their traffic with their own
        /// bundled DNS client instead, querying whatever server this struct reports rather than
        /// asking the host OS to resolve the name -- and the ordinary constructor reports this
        /// machine's own, real DNS servers, which know nothing about the redirect and either
        /// fail to resolve a private-server hostname or resolve it to Nintendo's real, unreachable
        /// address. A private server that also answers DNS queries directly (as a real console
        /// deployment already points its own network settings at) sidesteps that entirely; a
        /// private server that does not still leaves sfdnsres-based lookups working exactly as
        /// before, so this is not a regression for either case.
        /// </remarks>
        public DnsSetting(IPAddress server)
        {
            IsDynamicDnsEnabled = false;
            PrimaryDns = new IpV4Address(server);
            SecondaryDns = new IpV4Address(server);
        }
    }
}
