using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Types;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Proxy
{
    class LdnProxy : IDisposable
    {
        public EndPoint LocalEndpoint { get; }
        public IPAddress LocalAddress { get; }

        private readonly List<LdnProxySocket> _sockets = [];
        private readonly Dictionary<ProtocolType, EphemeralPortPool> _ephemeralPorts = new();

        private readonly IProxyClient _parent;
        private RyuLdnProtocol _protocol;
        private readonly uint _subnetMask;
        private readonly uint _localIp;
        private readonly uint _broadcast;
        private bool _tcpWarningLogged;

        public LdnProxy(ProxyConfig config, IProxyClient client, RyuLdnProtocol protocol)
        {
            _parent = client;
            _protocol = protocol;

            _ephemeralPorts[ProtocolType.Udp] = new EphemeralPortPool();
            _ephemeralPorts[ProtocolType.Tcp] = new EphemeralPortPool();

            byte[] address = BitConverter.GetBytes(config.ProxyIp);
            Array.Reverse(address);
            LocalAddress = new IPAddress(address);

            _subnetMask = config.ProxySubnetMask;
            _localIp = config.ProxyIp;
            _broadcast = _localIp | (~_subnetMask);

            RegisterHandlers(protocol);
        }

        public bool Supported(AddressFamily domain, SocketType type, ProtocolType protocol)
        {
            if (protocol == ProtocolType.Tcp && !_tcpWarningLogged)
            {
                Logger.Warning?.PrintMsg(LogClass.ServiceLdn, "LDN proxy TCP networking is experimental.");
                _tcpWarningLogged = true;
            }

            return domain == AddressFamily.InterNetwork && (protocol == ProtocolType.Tcp || protocol == ProtocolType.Udp);
        }

        private void RegisterHandlers(RyuLdnProtocol protocol)
        {
            protocol.ProxyConnect += HandleConnectionRequest;
            protocol.ProxyConnectReply += HandleConnectionResponse;
            protocol.ProxyData += HandleData;
            protocol.ProxyDisconnect += HandleDisconnect;

            _protocol = protocol;
        }

        public void UnregisterHandlers(RyuLdnProtocol protocol)
        {
            protocol.ProxyConnect -= HandleConnectionRequest;
            protocol.ProxyConnectReply -= HandleConnectionResponse;
            protocol.ProxyData -= HandleData;
            protocol.ProxyDisconnect -= HandleDisconnect;
        }

        public ushort GetEphemeralPort(ProtocolType type)
        {
            return _ephemeralPorts[type].Get();
        }

        public void ReturnEphemeralPort(ProtocolType type, ushort port)
        {
            _ephemeralPorts[type].Return(port);
        }

        public void RegisterSocket(LdnProxySocket socket)
        {
            lock (_sockets)
            {
                _sockets.Add(socket);
            }
        }

        public void UnregisterSocket(LdnProxySocket socket)
        {
            lock (_sockets)
            {
                _sockets.Remove(socket);
            }
        }

        private void ForRoutedSockets(ProxyInfo info, Action<LdnProxySocket> action)
        {
            lock (_sockets)
            {
                foreach (LdnProxySocket socket in _sockets)
                {
                    // Must match protocol and destination port.
                    if (socket.ProtocolType != info.Protocol || socket.LocalEndPoint is not IPEndPoint endpoint || endpoint.Port != info.DestPort)
                    {
                        continue;
                    }

                    // We can assume packets routed to us have been sent to our destination.
                    // They will either be sent to us, or broadcast packets.

                    action(socket);
                }
            }
        }

        private void ForConnectionResponseSockets(ProxyInfo info, Action<LdnProxySocket> action)
        {
            lock (_sockets)
            {
                foreach (LdnProxySocket socket in _sockets)
                {
                    if (socket.ProtocolType != info.Protocol ||
                        socket.LocalEndPoint is not IPEndPoint localEndpoint ||
                        localEndpoint.Port != info.DestPort ||
                        socket.RemoteEndPoint is not IPEndPoint remoteEndpoint ||
                        remoteEndpoint.Port != info.SourcePort)
                    {
                        continue;
                    }

                    action(socket);
                }
            }
        }

        private void ForDataSockets(ProxyInfo info, Action<LdnProxySocket> action)
        {
            lock (_sockets)
            {
                foreach (LdnProxySocket socket in _sockets)
                {
                    if (socket.ProtocolType != info.Protocol ||
                        socket.LocalEndPoint is not IPEndPoint localEndpoint ||
                        localEndpoint.Port != info.DestPort ||
                        !EndpointMatches(localEndpoint, info.DestIpV4))
                    {
                        continue;
                    }

                    if (info.Protocol == ProtocolType.Tcp &&
                        (!socket.Connected ||
                         socket.RemoteEndPoint is not IPEndPoint remoteEndpoint ||
                         remoteEndpoint.Port != info.SourcePort ||
                         !EndpointMatches(remoteEndpoint, info.SourceIpV4)))
                    {
                        continue;
                    }

                    action(socket);
                }
            }
        }

        public void HandleConnectionRequest(LdnHeader header, ProxyConnectRequest request)
        {
            bool routed = false;

            ForRoutedSockets(request.Info, (socket) =>
            {
                routed = true;
                socket.HandleConnectRequest(request);
            });

            if (!routed)
            {
                Logger.Warning?.PrintMsg(LogClass.ServiceLdn, $"ProxyConnect had no listening socket: {FormatInfo(request.Info)}");
            }
        }

        public void HandleConnectionResponse(LdnHeader header, ProxyConnectResponse response)
        {
            bool routed = false;

            ForConnectionResponseSockets(response.Info, (socket) =>
            {
                routed = true;
                socket.HandleConnectResponse(response);
            });

            if (!routed)
            {
                Logger.Warning?.PrintMsg(LogClass.ServiceLdn, $"ProxyConnectReply had no connecting socket: {FormatInfo(response.Info)}");
            }
        }

        public void HandleData(LdnHeader header, ProxyDataHeader proxyHeader, byte[] data)
        {
            ProxyDataPacket packet = new() { Header = proxyHeader, Data = data };

            bool routed = false;

            ForDataSockets(proxyHeader.Info, (socket) =>
            {
                routed = true;
                socket.IncomingData(packet);
            });

            if (!routed)
            {
                Logger.Warning?.PrintMsg(LogClass.ServiceLdn, $"ProxyData had no receiving socket: {FormatInfo(proxyHeader.Info)}");
            }
        }

        public void HandleDisconnect(LdnHeader header, ProxyDisconnectMessage disconnect)
        {
            bool routed = false;

            ForDataSockets(disconnect.Info, (socket) =>
            {
                routed = true;
                socket.HandleDisconnect(disconnect);
            });

            if (!routed)
            {
                Logger.Warning?.PrintMsg(LogClass.ServiceLdn, $"ProxyDisconnect had no connected socket: {FormatInfo(disconnect.Info)}");
            }
        }

        private uint GetIpV4(IPEndPoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);

            if (endpoint.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new NotSupportedException();
            }

            if (endpoint.Address.Equals(IPAddress.Any))
            {
                return _localIp;
            }

            byte[] address = endpoint.Address.GetAddressBytes();
            Array.Reverse(address);

            return BitConverter.ToUInt32(address);
        }

        private bool EndpointMatches(IPEndPoint endpoint, uint ipv4)
        {
            return endpoint.Address.Equals(IPAddress.Any) ||
                endpoint.Address.Equals(IPAddress.IPv6Any) ||
                GetIpV4(endpoint) == ipv4;
        }

        private static string FormatInfo(ProxyInfo info)
        {
            return $"{FormatIp(info.SourceIpV4)}:{info.SourcePort} -> {FormatIp(info.DestIpV4)}:{info.DestPort} ({info.Protocol})";
        }

        private static string FormatIp(uint ipv4)
        {
            byte[] address = BitConverter.GetBytes(ipv4);
            Array.Reverse(address);

            return new IPAddress(address).ToString();
        }

        private ProxyInfo MakeInfo(IPEndPoint localEp, IPEndPoint remoteEP, ProtocolType type)
        {
            return new ProxyInfo
            {
                SourceIpV4 = GetIpV4(localEp),
                SourcePort = (ushort)localEp.Port,

                DestIpV4 = GetIpV4(remoteEP),
                DestPort = (ushort)remoteEP.Port,

                Protocol = type
            };
        }

        public void RequestConnection(IPEndPoint localEp, IPEndPoint remoteEp, ProtocolType type)
        {
            // We must ask the other side to initialize a connection, so they can accept a socket for us.

            ProxyConnectRequest request = new()
            {
                Info = MakeInfo(localEp, remoteEp, type)
            };

            _parent.SendAsync(_protocol.Encode(PacketId.ProxyConnect, request));
        }

        public void SignalConnected(IPEndPoint localEp, IPEndPoint remoteEp, ProtocolType type)
        {
            // We must tell the other side that we have accepted their request for connection.

            ProxyConnectResponse request = new()
            {
                Info = MakeInfo(localEp, remoteEp, type)
            };

            _parent.SendAsync(_protocol.Encode(PacketId.ProxyConnectReply, request));
        }

        public void EndConnection(IPEndPoint localEp, IPEndPoint remoteEp, ProtocolType type)
        {
            // We must tell the other side that our connection is dropped.

            ProxyDisconnectMessage request = new()
            {
                Info = MakeInfo(localEp, remoteEp, type),
                DisconnectReason = 0 // TODO
            };

            _parent.SendAsync(_protocol.Encode(PacketId.ProxyDisconnect, request));
        }

        public int SendTo(ReadOnlySpan<byte> buffer, SocketFlags flags, IPEndPoint localEp, IPEndPoint remoteEp, ProtocolType type)
        {
            // We send exactly as much as the user wants us to, currently instantly.
            // TODO: handle over "virtual mtu" (we have a max packet size to worry about anyways). fragment if tcp? throw if udp?

            ProxyDataHeader request = new()
            {
                Info = MakeInfo(localEp, remoteEp, type),
                DataLength = (uint)buffer.Length
            };

            _parent.SendAsync(_protocol.Encode(PacketId.ProxyData, request, buffer.ToArray()));

            return buffer.Length;
        }

        public bool IsBroadcast(uint ip)
        {
            return ip == _broadcast;
        }

        public bool IsMyself(uint ip)
        {
            return ip == _localIp;
        }

        public void Dispose()
        {
            UnregisterHandlers(_protocol);

            lock (_sockets)
            {
                foreach (LdnProxySocket socket in _sockets)
                {
                    socket.ProxyDestroyed();
                }
            }
        }
    }
}
