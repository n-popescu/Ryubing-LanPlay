using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Types;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Impl;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnRyu.Proxy
{
    /// <summary>
    /// This socket is forwarded through a TCP stream that goes through the Ldn server.
    /// The Ldn server will then route the packets we send (or need to receive) within the virtual adhoc network.
    /// </summary>
    class LdnProxySocket : ISocketImpl
    {
        private readonly LdnProxy _proxy;

        private bool _isListening;
        private readonly List<LdnProxySocket> _listenSockets = [];

        private readonly Queue<ProxyConnectRequest> _connectRequests = new();

        private readonly AutoResetEvent _acceptEvent = new(false);
        private readonly int _acceptTimeout = -1;

        private readonly Queue<int> _errors = new();

        private readonly AutoResetEvent _connectEvent = new(false);
        private const int ConnectTimeoutMilliseconds = 10000;
        private ProxyConnectResponse _connectResponse;

        private int _receiveTimeout = -1;
        private readonly AutoResetEvent _receiveEvent = new(false);
        private readonly Queue<ProxyDataPacket> _receiveQueue = new();

        // private int _sendTimeout = -1; // Sends are techically instant right now, so not _really_ used.

        private bool _connecting;
        private bool _readShutdown;
        private bool _writeShutdown;
        private bool _disconnectSent;
        private bool _closed;

        private readonly Dictionary<SocketOptionName, int> _socketOptions = new()
        {
            { SocketOptionName.Broadcast,       0 }, //TODO: honor this value
            { SocketOptionName.DontLinger,      0 },
            { SocketOptionName.Debug,           0 },
            { SocketOptionName.Error,           0 },
            { SocketOptionName.KeepAlive,       0 },
            { SocketOptionName.OutOfBandInline, 0 },
            { SocketOptionName.ReceiveBuffer,   131072 },
            { SocketOptionName.ReceiveTimeout,  -1 },
            { SocketOptionName.SendBuffer,      131072 },
            { SocketOptionName.SendTimeout,     -1 },
            { SocketOptionName.Type,            0 },
            { SocketOptionName.ReuseAddress,    0 } //TODO: honor this value
        };

        public EndPoint RemoteEndPoint { get; private set; }

        public EndPoint LocalEndPoint { get; private set; }

        public bool Connected { get; private set; }

        public bool IsBound { get; private set; }

        public AddressFamily AddressFamily { get; }

        public SocketType SocketType { get; }

        public ProtocolType ProtocolType { get; }

        public bool Blocking { get; set; }

        public int Available
        {
            get
            {
                int result = 0;

                lock (_receiveQueue)
                {
                    foreach (ProxyDataPacket data in _receiveQueue)
                    {
                        result += data.Data.Length;
                    }
                }

                return result;
            }
        }

        public bool Readable
        {
            get
            {
                if (_isListening)
                {
                    lock (_connectRequests)
                    {
                        return _connectRequests.Count > 0;
                    }
                }
                else
                {
                    if (_readShutdown)
                    {
                        return true;
                    }

                    lock (_receiveQueue)
                    {
                        return _receiveQueue.Count > 0;
                    }
                }
            }
        }
        public bool Writable => !_writeShutdown && (Connected || ProtocolType == ProtocolType.Udp);
        public bool Error
        {
            get
            {
                lock (_errors)
                {
                    return _errors.Count > 0;
                }
            }
        }

        public LdnProxySocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, LdnProxy proxy)
        {
            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;

            _proxy = proxy;
            _socketOptions[SocketOptionName.Type] = (int)socketType;

            proxy.RegisterSocket(this);
        }

        private IPEndPoint EnsureLocalEndpoint(bool replace)
        {
            if (LocalEndPoint != null)
            {
                if (replace)
                {
                    _proxy.ReturnEphemeralPort(ProtocolType, (ushort)((IPEndPoint)LocalEndPoint).Port);
                }
                else
                {
                    return (IPEndPoint)LocalEndPoint;
                }
            }

            IPEndPoint localEp = new(_proxy.LocalAddress, _proxy.GetEphemeralPort(ProtocolType));
            LocalEndPoint = localEp;

            return localEp;
        }

        public LdnProxySocket AsAccepted(IPEndPoint localEp, IPEndPoint remoteEp)
        {
            Connected = true;
            LocalEndPoint = localEp;
            RemoteEndPoint = remoteEp;

            _proxy.SignalConnected(localEp, remoteEp, ProtocolType);

            return this;
        }

        private void SignalError(WsaError error)
        {
            lock (_errors)
            {
                _errors.Enqueue((int)error);
            }
        }

        private IPEndPoint GetEndpoint(uint ipv4, ushort port)
        {
            byte[] address = BitConverter.GetBytes(ipv4);
            Array.Reverse(address);

            return new IPEndPoint(new IPAddress(address), port);
        }

        public void IncomingData(ProxyDataPacket packet)
        {
            if (!_closed)
            {
                lock (_receiveQueue)
                {
                    _receiveQueue.Enqueue(packet);
                }

                _receiveEvent.Set();
            }
        }

        public ISocketImpl Accept()
        {
            if (!_isListening)
            {
                throw new InvalidOperationException();
            }

            // Accept a pending request to this socket.

            lock (_connectRequests)
            {
                if (!Blocking && _connectRequests.Count == 0)
                {
                    throw new SocketException((int)WsaError.WSAEWOULDBLOCK);
                }
            }

            while (true)
            {
                _acceptEvent.WaitOne(_acceptTimeout);

                lock (_connectRequests)
                {
                    while (_connectRequests.Count > 0)
                    {
                        ProxyConnectRequest request = _connectRequests.Dequeue();

                        if (_connectRequests.Count > 0)
                        {
                            _acceptEvent.Set(); // Still more accepts to do.
                        }

                        // Is this request made for us?
                        IPEndPoint endpoint = GetEndpoint(request.Info.DestIpV4, request.Info.DestPort);

                        if (MatchesLocalEndpoint(endpoint))
                        {
                            // Yes - let's accept.
                            IPEndPoint remoteEndpoint = GetEndpoint(request.Info.SourceIpV4, request.Info.SourcePort);

                            LdnProxySocket socket = new LdnProxySocket(AddressFamily, SocketType, ProtocolType, _proxy)
                            {
                                Blocking = Blocking,
                            }.AsAccepted((IPEndPoint)LocalEndPoint, remoteEndpoint);

                            lock (_listenSockets)
                            {
                                _listenSockets.Add(socket);
                            }

                            return socket;
                        }
                    }
                }
            }
        }

        private bool MatchesLocalEndpoint(IPEndPoint endpoint)
        {
            if (LocalEndPoint is not IPEndPoint localEndpoint || endpoint.Port != localEndpoint.Port)
            {
                return false;
            }

            return localEndpoint.Address.Equals(IPAddress.Any) ||
                localEndpoint.Address.Equals(IPAddress.IPv6Any) ||
                endpoint.Address.Equals(localEndpoint.Address);
        }

        public void Bind(EndPoint localEP)
        {
            ArgumentNullException.ThrowIfNull(localEP);

            if (LocalEndPoint != null)
            {
                _proxy.ReturnEphemeralPort(ProtocolType, (ushort)((IPEndPoint)LocalEndPoint).Port);
            }

            IPEndPoint asIPEndpoint = (IPEndPoint)localEP;
            if (asIPEndpoint.Port == 0)
            {
                asIPEndpoint.Port = (ushort)_proxy.GetEphemeralPort(ProtocolType);
            }

            LocalEndPoint = (IPEndPoint)localEP;

            IsBound = true;
        }

        public void Close()
        {
            _closed = true;

            _proxy.UnregisterSocket(this);

            if (Connected)
            {
                Disconnect(false);
            }

            lock (_listenSockets)
            {
                foreach (LdnProxySocket socket in _listenSockets)
                {
                    socket.Close();
                }
            }

            _isListening = false;
        }

        public void Connect(EndPoint remoteEP)
        {
            if (_isListening)
            {
                throw new InvalidOperationException();
            }

            if (remoteEP is not IPEndPoint)
            {
                throw new NotSupportedException();
            }

            if (ProtocolType == ProtocolType.Tcp)
            {
                if (Connected)
                {
                    if (RemoteEndPoint is IPEndPoint currentRemote && currentRemote.Equals(remoteEP))
                    {
                        return;
                    }

                    throw new SocketException((int)WsaError.WSAEISCONN);
                }

                if (_connecting)
                {
                    throw new SocketException((int)WsaError.WSAEALREADY);
                }
            }

            IPEndPoint localEp = EnsureLocalEndpoint(true);

            RemoteEndPoint = (IPEndPoint)remoteEP;
            _connecting = true;

            _proxy.RequestConnection(localEp, (IPEndPoint)remoteEP, ProtocolType);

            if (!Blocking && ProtocolType == ProtocolType.Tcp)
            {
                throw new SocketException((int)WsaError.WSAEWOULDBLOCK);
            }

            if (!_connectEvent.WaitOne(ConnectTimeoutMilliseconds))
            {
                _connecting = false;
                SignalError(WsaError.WSAETIMEDOUT);

                throw new SocketException((int)WsaError.WSAETIMEDOUT);
            }

            if (_connectResponse.Info.SourceIpV4 == 0)
            {
                throw new SocketException((int)WsaError.WSAECONNREFUSED);
            }

            _connectResponse = default;
        }

        public void HandleConnectResponse(ProxyConnectResponse obj)
        {
            if (!_connecting)
            {
                return;
            }

            _connecting = false;

            _connectResponse = obj;

            if (obj.Info.SourceIpV4 != 0)
            {
                IPEndPoint remoteEp = GetEndpoint(obj.Info.SourceIpV4, obj.Info.SourcePort);
                RemoteEndPoint = remoteEp;

                Connected = true;
            }
            else
            {
                // Connection failed

                SignalError(WsaError.WSAECONNREFUSED);
            }

            _connectEvent.Set();
        }

        public void Disconnect(bool reuseSocket)
        {
            if (Connected)
            {
                SignalDisconnect();

                ConnectionEnded();
            }

            _readShutdown = true;
            _writeShutdown = true;

            _receiveEvent.Set();
            _acceptEvent.Set();
            _connectEvent.Set();
        }

        private void ConnectionEnded()
        {
            if (Connected)
            {
                RemoteEndPoint = null;
                Connected = false;
            }
        }

        private void SignalDisconnect()
        {
            if (_disconnectSent ||
                LocalEndPoint is not IPEndPoint localEndpoint ||
                RemoteEndPoint is not IPEndPoint remoteEndpoint)
            {
                return;
            }

            _disconnectSent = true;
            _proxy.EndConnection(localEndpoint, remoteEndpoint, ProtocolType);
        }

        public void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
        {
            if (optionLevel != SocketOptionLevel.Socket)
            {
                throw new NotImplementedException();
            }

            if (_socketOptions.TryGetValue(optionName, out int result))
            {
                if (optionName == SocketOptionName.Error)
                {
                    lock (_errors)
                    {
                        result = _errors.Count > 0 ? _errors.Dequeue() : 0;
                    }

                    if (result == 0 && _connecting)
                    {
                        result = (int)WsaError.WSAEINPROGRESS;
                    }
                }

                byte[] data = BitConverter.GetBytes(result);
                Array.Copy(data, 0, optionValue, 0, Math.Min(data.Length, optionValue.Length));
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public void Listen(int backlog)
        {
            if (!IsBound)
            {
                throw new SocketException();
            }

            _isListening = true;
        }

        public void HandleConnectRequest(ProxyConnectRequest obj)
        {
            lock (_connectRequests)
            {
                _connectRequests.Enqueue(obj);
            }

            _acceptEvent.Set();
        }

        public void HandleDisconnect(ProxyDisconnectMessage message)
        {
            _readShutdown = true;

            _receiveEvent.Set();
            _acceptEvent.Set();
            _connectEvent.Set();
        }

        public int Receive(Span<byte> buffer)
        {
            EndPoint dummy = new IPEndPoint(IPAddress.Any, 0);

            return ReceiveFrom(buffer, SocketFlags.None, ref dummy);
        }

        public int Receive(Span<byte> buffer, SocketFlags flags)
        {
            EndPoint dummy = new IPEndPoint(IPAddress.Any, 0);

            return ReceiveFrom(buffer, flags, ref dummy);
        }

        public int Receive(Span<byte> buffer, SocketFlags flags, out SocketError socketError)
        {
            EndPoint dummy = new IPEndPoint(IPAddress.Any, 0);

            return ReceiveFrom(buffer, flags, out socketError, ref dummy);
        }

        public int ReceiveFrom(Span<byte> buffer, SocketFlags flags, ref EndPoint remoteEp)
        {
            // We just receive all packets meant for us anyways regardless of EP in the actual implementation.
            // The point is mostly to return the endpoint that we got the data from.

            if (!Connected && !_readShutdown && ProtocolType == ProtocolType.Tcp)
            {
                throw new SocketException((int)WsaError.WSAECONNRESET);
            }

            lock (_receiveQueue)
            {
                if (_receiveQueue.Count > 0)
                {
                    return ReceiveFromQueue(buffer, flags, ref remoteEp);
                }
                else if (_readShutdown)
                {
                    return 0;
                }
                else if (!Blocking)
                {
                    throw new SocketException((int)WsaError.WSAEWOULDBLOCK);
                }
            }

            int timeout = _receiveTimeout;
            long deadline = timeout > 0 ? Environment.TickCount64 + timeout : 0;

            while (true)
            {
                int waitTimeout = -1;

                if (timeout > 0)
                {
                    long remaining = deadline - Environment.TickCount64;

                    if (remaining <= 0)
                    {
                        break;
                    }

                    waitTimeout = remaining > int.MaxValue ? int.MaxValue : (int)remaining;
                }

                bool signalled = _receiveEvent.WaitOne(waitTimeout);

                if (!Connected && !_readShutdown && ProtocolType == ProtocolType.Tcp)
                {
                    throw new SocketException((int)WsaError.WSAECONNRESET);
                }

                lock (_receiveQueue)
                {
                    if (_receiveQueue.Count > 0)
                    {
                        return ReceiveFromQueue(buffer, flags, ref remoteEp);
                    }
                    else if (_readShutdown)
                    {
                        return 0;
                    }
                }

                if (!signalled && timeout > 0)
                {
                    break;
                }
            }

            throw new SocketException((int)WsaError.WSAEWOULDBLOCK);
        }

        public int ReceiveFrom(Span<byte> buffer, SocketFlags flags, out SocketError socketError, ref EndPoint remoteEp)
        {
            // We just receive all packets meant for us anyways regardless of EP in the actual implementation.
            // The point is mostly to return the endpoint that we got the data from.

            if (!Connected && !_readShutdown && ProtocolType == ProtocolType.Tcp)
            {
                socketError = SocketError.ConnectionReset;
                return -1;
            }

            lock (_receiveQueue)
            {
                if (_receiveQueue.Count > 0)
                {
                    return ReceiveFromQueue(buffer, flags, out socketError, ref remoteEp);
                }
                else if (_readShutdown)
                {
                    socketError = SocketError.Success;
                    return 0;
                }
                else if (!Blocking)
                {
                    throw new SocketException((int)WsaError.WSAEWOULDBLOCK);
                }
            }

            int timeout = _receiveTimeout;
            long deadline = timeout > 0 ? Environment.TickCount64 + timeout : 0;

            while (true)
            {
                int waitTimeout = -1;

                if (timeout > 0)
                {
                    long remaining = deadline - Environment.TickCount64;

                    if (remaining <= 0)
                    {
                        break;
                    }

                    waitTimeout = remaining > int.MaxValue ? int.MaxValue : (int)remaining;
                }

                bool signalled = _receiveEvent.WaitOne(waitTimeout);

                if (!Connected && !_readShutdown && ProtocolType == ProtocolType.Tcp)
                {
                    socketError = SocketError.ConnectionReset;
                    return -1;
                }

                lock (_receiveQueue)
                {
                    if (_receiveQueue.Count > 0)
                    {
                        return ReceiveFromQueue(buffer, flags, out socketError, ref remoteEp);
                    }
                    else if (_readShutdown)
                    {
                        socketError = SocketError.Success;
                        return 0;
                    }
                }

                if (!signalled && timeout > 0)
                {
                    break;
                }
            }

            socketError = SocketError.WouldBlock;
            return -1;
        }

        private int ReceiveFromQueue(Span<byte> buffer, SocketFlags flags, ref EndPoint remoteEp)
        {
            int size = buffer.Length;

            // Assumes we have the receive queue lock, and at least one item in the queue.
            ProxyDataPacket packet = _receiveQueue.Peek();

            remoteEp = GetEndpoint(packet.Header.Info.SourceIpV4, packet.Header.Info.SourcePort);

            bool peek = (flags & SocketFlags.Peek) != 0;

            int read;

            if (packet.Data.Length > size)
            {
                read = size;

                // Cannot fit in the output buffer. Copy up to what we've got.
                packet.Data.AsSpan(0, size).CopyTo(buffer);

                if (ProtocolType == ProtocolType.Udp)
                {
                    // Udp overflows, loses the data, then throws an exception.

                    if (!peek)
                    {
                        _receiveQueue.Dequeue();
                    }

                    throw new SocketException((int)WsaError.WSAEMSGSIZE);
                }
                else if (ProtocolType == ProtocolType.Tcp)
                {
                    // Split the data at the buffer boundary. It will stay on the recieve queue.

                    byte[] newData = new byte[packet.Data.Length - size];
                    Array.Copy(packet.Data, size, newData, 0, newData.Length);

                    packet.Data = newData;
                }
            }
            else
            {
                read = packet.Data.Length;

                packet.Data.AsSpan(0, packet.Data.Length).CopyTo(buffer);

                if (!peek)
                {
                    _receiveQueue.Dequeue();
                }
            }

            return read;
        }

        private int ReceiveFromQueue(Span<byte> buffer, SocketFlags flags, out SocketError socketError, ref EndPoint remoteEp)
        {
            int size = buffer.Length;

            // Assumes we have the receive queue lock, and at least one item in the queue.
            ProxyDataPacket packet = _receiveQueue.Peek();

            remoteEp = GetEndpoint(packet.Header.Info.SourceIpV4, packet.Header.Info.SourcePort);

            bool peek = (flags & SocketFlags.Peek) != 0;

            int read;

            if (packet.Data.Length > size)
            {
                read = size;

                // Cannot fit in the output buffer. Copy up to what we've got.
                packet.Data.AsSpan(0, size).CopyTo(buffer);

                if (ProtocolType == ProtocolType.Udp)
                {
                    // Udp overflows, loses the data, then throws an exception.

                    if (!peek)
                    {
                        _receiveQueue.Dequeue();
                    }

                    socketError = SocketError.MessageSize;
                    return -1;
                }
                else if (ProtocolType == ProtocolType.Tcp)
                {
                    // Split the data at the buffer boundary. It will stay on the recieve queue.

                    byte[] newData = new byte[packet.Data.Length - size];
                    Array.Copy(packet.Data, size, newData, 0, newData.Length);

                    packet.Data = newData;
                }
            }
            else
            {
                read = packet.Data.Length;

                packet.Data.AsSpan(0, packet.Data.Length).CopyTo(buffer);

                if (!peek)
                {
                    _receiveQueue.Dequeue();
                }
            }

            socketError = SocketError.Success;

            return read;
        }

        public int Send(ReadOnlySpan<byte> buffer)
        {
            // Send to the remote host chosen when we "connect" or "accept".
            if (_writeShutdown)
            {
                throw new SocketException((int)WsaError.WSAESHUTDOWN);
            }

            if (!Connected)
            {
                throw new SocketException();
            }

            return SendTo(buffer, SocketFlags.None, RemoteEndPoint);
        }

        public int Send(ReadOnlySpan<byte> buffer, SocketFlags flags)
        {
            // Send to the remote host chosen when we "connect" or "accept".
            if (_writeShutdown)
            {
                throw new SocketException((int)WsaError.WSAESHUTDOWN);
            }

            if (!Connected)
            {
                throw new SocketException();
            }

            return SendTo(buffer, flags, RemoteEndPoint);
        }

        public int Send(ReadOnlySpan<byte> buffer, SocketFlags flags, out SocketError socketError)
        {
            // Send to the remote host chosen when we "connect" or "accept".
            if (_writeShutdown)
            {
                socketError = SocketError.Shutdown;
                return -1;
            }

            if (!Connected)
            {
                throw new SocketException();
            }

            return SendTo(buffer, flags, out socketError, RemoteEndPoint);
        }

        public int SendTo(ReadOnlySpan<byte> buffer, SocketFlags flags, EndPoint remoteEP)
        {
            if (_writeShutdown)
            {
                throw new SocketException((int)WsaError.WSAESHUTDOWN);
            }

            if (!Connected && ProtocolType == ProtocolType.Tcp)
            {
                throw new SocketException((int)WsaError.WSAECONNRESET);
            }

            IPEndPoint localEp = EnsureLocalEndpoint(false);

            if (remoteEP is not IPEndPoint)
            {
                throw new NotSupportedException();
            }

            return _proxy.SendTo(buffer, flags, localEp, (IPEndPoint)remoteEP, ProtocolType);
        }

        public int SendTo(ReadOnlySpan<byte> buffer, SocketFlags flags, out SocketError socketError, EndPoint remoteEP)
        {
            if (_writeShutdown)
            {
                socketError = SocketError.Shutdown;
                return -1;
            }

            if (!Connected && ProtocolType == ProtocolType.Tcp)
            {
                socketError = SocketError.ConnectionReset;
                return -1;
            }

            IPEndPoint localEp = EnsureLocalEndpoint(false);

            if (remoteEP is not IPEndPoint)
            {
                // throw new NotSupportedException();
                socketError = SocketError.OperationNotSupported;
                return -1;
            }

            socketError = SocketError.Success;

            return _proxy.SendTo(buffer, flags, localEp, (IPEndPoint)remoteEP, ProtocolType);
        }

        public bool Poll(int microSeconds, SelectMode mode)
        {
            return mode switch
            {
                SelectMode.SelectRead => Readable,
                SelectMode.SelectWrite => Writable,
                SelectMode.SelectError => Error,
                _ => false
            };
        }

        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue)
        {
            if (optionLevel != SocketOptionLevel.Socket)
            {
                throw new NotImplementedException();
            }

            switch (optionName)
            {
                case SocketOptionName.SendTimeout:
                    //_sendTimeout = optionValue;
                    break;
                case SocketOptionName.ReceiveTimeout:
                    _receiveTimeout = optionValue;
                    break;
            }

            lock (_socketOptions)
            {
                _socketOptions[optionName] = optionValue;
            }
        }

        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue)
        {
            // Just linger uses this for now in BSD, which we ignore.
        }

        public void Shutdown(SocketShutdown how)
        {
            switch (how)
            {
                case SocketShutdown.Both:
                    _readShutdown = true;
                    _writeShutdown = true;
                    SignalDisconnect();
                    _receiveEvent.Set();
                    break;
                case SocketShutdown.Receive:
                    _readShutdown = true;
                    _receiveEvent.Set();
                    break;
                case SocketShutdown.Send:
                    _writeShutdown = true;
                    SignalDisconnect();
                    break;
            }
        }

        public void ProxyDestroyed()
        {
            _closed = true;
            _readShutdown = true;
            _writeShutdown = true;
            ConnectionEnded();

            _receiveEvent.Set();
            _acceptEvent.Set();
            _connectEvent.Set();
        }

        public void Dispose()
        {
        }
    }
}
