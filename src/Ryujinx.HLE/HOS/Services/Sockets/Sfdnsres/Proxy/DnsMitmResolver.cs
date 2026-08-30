using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Sockets.Nsd;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Net;

namespace Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres.Proxy
{
    class DnsMitmResolver
    {
        private const string HostsFilePath = "/atmosphere/hosts/default.txt";

        private static DnsMitmResolver _instance;
        public static DnsMitmResolver Instance => _instance ??= new DnsMitmResolver();

        private readonly Dictionary<string, IPAddress> _mitmHostEntries = new();

        /// <summary>
        /// The address most recently handed out by a `getaddrinfo`-style lookup for a given
        /// port, regardless of which host it came from.
        /// </summary>
        /// <remarks>
        /// Exists for one specific failure mode: some guest network stacks issue exactly one
        /// address lookup for a host:port pair and then connect immediately after, but arrive at
        /// <c>connect()</c> with the address lost -- 0.0.0.0 / :: -- while the port survives
        /// intact. When that happens there is no way to recover the intended host from the
        /// connect call alone, but the port is still a strong hint: it is very likely the same
        /// target whose address this resolver just handed back. Recording it here lets
        /// <c>ManagedSocket.Connect</c> substitute a real address instead of failing outright.
        /// Keyed by port rather than by host because that is all a lost-address connect call has
        /// left to key on.
        /// </remarks>
        private readonly ConcurrentDictionary<int, IPAddress> _lastResolvedByPort = new();

        /// <summary>
        /// Records the address a `getaddrinfo`-style lookup just resolved for <paramref name="port"/>,
        /// for <see cref="TryGetLastResolved"/> to consult later.
        /// </summary>
        public void RememberResolution(int port, IPAddress address)
        {
            if (port != 0 && address != null)
            {
                _lastResolvedByPort[port] = address;
            }
        }

        /// <summary>
        /// Looks up the address most recently resolved for <paramref name="port"/>, if any.
        /// </summary>
        public bool TryGetLastResolved(int port, out IPAddress address) =>
            _lastResolvedByPort.TryGetValue(port, out address);

        public void ReloadEntries(ServiceCtx context)
        {
            string sdPath = FileSystem.VirtualFileSystem.GetSdCardPath();
            string filePath = FileSystem.VirtualFileSystem.GetFullPath(sdPath, HostsFilePath);

            _mitmHostEntries.Clear();

            if (File.Exists(filePath))
            {
                using FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read);
                using StreamReader reader = new(fileStream);

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    if (line == null)
                    {
                        break;
                    }

                    // Ignore comments and empty lines
                    if (line.StartsWith('#') || line.Trim().Length == 0)
                    {
                        continue;
                    }

                    string[] entry = line.Split([' ', '\t'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    // Hosts file example entry:
                    // 127.0.0.1  localhost loopback

                    // 0. Check the size of the array
                    if (entry.Length < 2)
                    {
                        Logger.Warning?.PrintMsg(LogClass.ServiceBsd, $"Invalid entry in hosts file: {line}");

                        continue;
                    }

                    // 1. Parse the address
                    if (!IPAddress.TryParse(entry[0], out IPAddress address))
                    {
                        Logger.Warning?.PrintMsg(LogClass.ServiceBsd, $"Failed to parse IP address in hosts file: {entry[0]}");

                        continue;
                    }

                    // 2. Check for AMS hosts file extension: "%"
                    for (int i = 1; i < entry.Length; i++)
                    {
                        entry[i] = entry[i].Replace("%", IManager.NsdSettings.Environment);
                    }

                    // 3. Add hostname to entry dictionary (updating duplicate entries)
                    foreach (string hostname in entry[1..])
                    {
                        _mitmHostEntries[hostname] = address;
                    }
                }
            }
        }

        /// <summary>
        /// Looks a host up in the hosts file, without falling back to real DNS.
        /// </summary>
        /// <param name="host">The hostname the guest asked for</param>
        /// <param name="entry">The redirect, when the hosts file names this host</param>
        /// <returns>True if the hosts file names this host</returns>
        /// <remarks>
        /// Separate from <see cref="ResolveAddress"/> because callers need to know
        /// <em>whether</em> a host is redirected, not just where to. <see cref="IResolver"/>
        /// uses it to let a redirected host past the DNS blacklist: the blacklist exists to keep
        /// the guest away from Nintendo's servers, and a host the operator has explicitly pointed
        /// at their own machine is not one of those. A host with no entry stays blocked, so
        /// enabling redirection cannot quietly open a path to real Nintendo.
        /// </remarks>
        public bool TryResolveRedirect(string host, out IPHostEntry entry)
        {
            foreach (KeyValuePair<string, IPAddress> hostEntry in _mitmHostEntries)
            {
                // Check for AMS hosts file extension: "*"
                // NOTE: MatchesSimpleExpression also allows "?" as a wildcard
                if (FileSystemName.MatchesSimpleExpression(hostEntry.Key, host))
                {
                    entry = new IPHostEntry
                    {
                        AddressList = [hostEntry.Value],
                        // The hostname the GUEST asked for, not the pattern that matched it.
                        // With a wildcard entry the pattern is not a hostname at all, and
                        // returning it made the guest's TLS see a name mismatch against a
                        // certificate that was in fact correct.
                        HostName = host,
                        Aliases = [],
                    };

                    return true;
                }
            }

            entry = null;

            return false;
        }

        public IPHostEntry ResolveAddress(string host)
        {
            if (TryResolveRedirect(host, out IPHostEntry redirect))
            {
                Logger.Info?.PrintMsg(LogClass.ServiceBsd,
                    $"Redirecting '{host}' to: {redirect.AddressList[0]}");

                return redirect;
            }

            // No match has been found, resolve the host using regular dns
            return Dns.GetHostEntry(host);
        }
    }
}
