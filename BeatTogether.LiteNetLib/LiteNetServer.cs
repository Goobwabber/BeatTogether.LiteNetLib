using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Delegates;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AsyncUdp;
using BeatTogether.LiteNetLib.Util;
using System.Collections.Generic;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetServer : AsyncUdpServer
    {
        public event ClientConnectHandler ClientConnectEvent = null!;
        public event ClientDisconnectHandler ClientDisconnectEvent = null!;

        private readonly Dictionary<EndPoint, (int, long)> _PongSequence = new();  //TODO check these are all handled correctly
        private readonly object _PongAccess = new();
        private readonly Dictionary<EndPoint, long> _connectionTimes = new();
        private readonly object _connectionTimeAccess = new();
        private readonly Dictionary<EndPoint, long> _lastPacketTimes = new();
        private readonly object _lastPacketTimeAccess = new();

        private SemaphoreSlim SendSemaphore;
        ReuseableBufferPool _PacketSendProcessingBuffers;

        private readonly LiteNetConfiguration _configuration;
        private readonly LiteNetPacketRegistry _packetRegistry;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPacketLayer? _packetLayer;

        public LiteNetServer(
            IPEndPoint endPoint,
            LiteNetConfiguration configuration,
            LiteNetPacketRegistry packetRegistry,
            IServiceProvider serviceProvider,
            IPacketLayer? packetLayer = null)
            : base(endPoint, 1, 2048)
        {
            _configuration = configuration;
            _packetRegistry = packetRegistry;
            _serviceProvider = serviceProvider;
            _packetLayer = packetLayer;
            SendSemaphore = new(_configuration.MaxConcurrentSends);
            _PacketSendProcessingBuffers = new(2048, _configuration.MaxConcurrentSends);
        }

        public LiteNetServer(
            IPEndPoint endPoint,
            LiteNetConfiguration configuration,
            LiteNetPacketRegistry packetRegistry,
            IServiceProvider serviceProvider,
            int ConcurrentRecv,
            IPacketLayer? packetLayer = null)
            : base(endPoint, ConcurrentRecv, 2048)
        {
            _configuration = configuration;
            _packetRegistry = packetRegistry;
            _serviceProvider = serviceProvider;
            _packetLayer = packetLayer;
            SendSemaphore = new(_configuration.MaxConcurrentSends);
            _PacketSendProcessingBuffers = new(2048, _configuration.MaxConcurrentSends);
        }

        protected override void OnReceived(EndPoint endPoint, Memory<byte> buffer)
        {
            lock (_lastPacketTimeAccess)
            {
                if (_lastPacketTimes.ContainsKey(endPoint))
                    _lastPacketTimes[endPoint] = DateTime.UtcNow.Ticks;
            }
            ReceivePacket(endPoint, buffer.Span);
        }

        private void ReceivePacket(EndPoint endPoint, Span<byte> buffer)
        {
            if (_packetLayer != null)
            {
                _packetLayer.ProcessInboundPacket(endPoint, ref buffer);
                HandlePacket(endPoint, buffer);
                return;
            }
            HandlePacket(endPoint, buffer);
        }

        internal protected virtual void HandlePacket(EndPoint endPoint, Span<byte> buffer)
        {
            if (buffer.Length > 0)
            {
                var bufferReader = new SpanBuffer(buffer);
                PacketProperty property = (PacketProperty)(buffer[0] & 0x1f); // 0x1f 00011111
                if (!_packetRegistry.TryCreatePacket(property, out var packet))
                    return;
                try
                {
                    packet.ReadFrom(ref bufferReader);
                }
                catch (EndOfBufferException) { return; } //Will ignore erranous packets that have incorrect data
                var packetHandlerType = typeof(IPacketHandler<>)
                        .MakeGenericType(packet.GetType());
                var packetHandler = _serviceProvider.GetService(packetHandlerType);
                if (packetHandler == null)
                    return;
                ((IPacketHandler)packetHandler).Handle(endPoint, packet, ref bufferReader);
            }
        }

        

        public async override Task SendAsync(EndPoint endPoint, Memory<byte> buffer)
        {
            if (_packetLayer != null)
            {
                await SendSemaphore.WaitAsync();
                _PacketSendProcessingBuffers.GetBuffer(out var ProcessingBuffer, out var BufferOffset);
                buffer.CopyTo(ProcessingBuffer);
                ProcessingBuffer = ProcessingBuffer.Slice(0, buffer.Length);
                _packetLayer.ProcessOutBoundPacket(endPoint, ref ProcessingBuffer);
                await base.SendAsync(endPoint, ProcessingBuffer);
                _PacketSendProcessingBuffers.ReturnBuffer(BufferOffset);
                SendSemaphore.Release();
                return;
            }
            await SendSemaphore.WaitAsync();
            await base.SendAsync(endPoint, buffer);
            SendSemaphore.Release();
        }

        private void ProcessOutbound(EndPoint endPoint, ref Memory<byte> buffer)
        {
            if (_packetLayer != null)
            {
                Span<byte> span = buffer.Span;
                _packetLayer.ProcessOutBoundPacket(endPoint, ref span);
                buffer = span.ToArray();
            }
        }

        /// <summary>
        /// Whether the server should accept a connection
        /// </summary>
        /// <param name="endPoint">Endpoint connection request was received from</param>
        /// <param name="additionalData">Additional data sent with the request</param>
        /// <returns></returns>
        public virtual async Task<bool> ShouldAcceptConnection(EndPoint endPoint, MemoryBuffer additionalData)
            => false;

        /// <summary>
        /// Called when an endpoint connects
        /// </summary>
        /// <param name="endPoint">Endpoint that connected</param>
        public virtual void OnConnect(EndPoint endPoint) { }

        /// <summary>
        /// Called when an endpoint disconnects
        /// </summary>
        /// <param name="endPoint">Endpoint that disconnected</param>
        /// <param name="reason">Reason for disconnect</param>
        public virtual void OnDisconnect(EndPoint endPoint, DisconnectReason reason) { }

        /// <summary>
        /// Called when latency information is updated
        /// </summary>
        /// <param name="endPoint">Endpoint latency was updated for</param>
        /// <param name="latency">Latency value in milliseconds</param>
        public virtual void OnLatencyUpdate(EndPoint endPoint, int latency) { }

        /// <summary>
        /// Sends a raw serializable packet to an endpoint
        /// </summary>
        /// <param name="endPoint">Endpoint to send packet to</param>
        /// <param name="packet">The raw data to send (must include LiteNetLib headers)</param>
        public virtual void SendAsync(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new SpanBuffer(stackalloc byte[412]);
            packet.WriteTo(ref bufferWriter);
            _ = SendAsync(endPoint, new Memory<byte>(bufferWriter.Data.ToArray()));
        }

        /// <summary>
        /// Disconnects a connected endpoint
        /// </summary>
        /// <param name="endPoint">Endpoint to disconnect</param>
        /// <param name="reason">Reason for disconnecting</param>
        public void Disconnect(EndPoint endPoint, DisconnectReason reason = DisconnectReason.DisconnectPeerCalled)
        {
            long connectionTime;
            lock (_connectionTimeAccess)
            {
                if (!_connectionTimes.Remove(endPoint, out connectionTime))
                    return;
            }
            SendAsync(endPoint, new DisconnectHeader
            {
                ConnectionTime = connectionTime
            });
            HandleDisconnect(endPoint, reason);
        }

        public bool HasConnected(EndPoint endPoint)
        {
            lock (_connectionTimeAccess)
            {
                return _connectionTimes.TryGetValue(endPoint, out _);
            }
        }

        internal void HandleConnect(EndPoint endPoint, long connectionTime)
        {
            lock (_connectionTimeAccess)
            {
                _connectionTimes[endPoint] = connectionTime;
            }
            lock (_lastPacketTimeAccess)
            {
                _lastPacketTimes[endPoint] = DateTime.UtcNow.Ticks;
            }
            Task.Run(async () => await PingClient(endPoint));
            Task.Run(async () => await CheckForTimeout(endPoint));
            OnConnect(endPoint);
            ClientConnectEvent?.Invoke(endPoint);
        }

        internal void HandleDisconnect(EndPoint endPoint, DisconnectReason reason)
        {
            lock (_connectionTimeAccess)
            {
                _connectionTimes.Remove(endPoint, out _);
            }
            lock (_PongAccess)
            {
                _PongSequence.Remove(endPoint, out _);
            }
            lock (_lastPacketTimeAccess)
            {
                _lastPacketTimes.Remove(endPoint, out _);
            }
            OnDisconnect(endPoint, reason);
            ClientDisconnectEvent?.Invoke(endPoint, reason);
        }

        internal void HandlePong(EndPoint endPoint, int sequence, long time)
        {
            (int, long) ServerSequence;
            bool Correct;
            lock (_PongAccess)
            {
                Correct = _PongSequence.TryGetValue(endPoint, out ServerSequence);
            }
            if (Correct && ServerSequence.Item1 == sequence)
                OnLatencyUpdate(endPoint, (DateTime.UtcNow - new DateTime(ServerSequence.Item2)).Milliseconds / 2);
        }

        /// <summary>
        /// Creates a loop where it will ping the client and wait for a pong
        /// Will disconnect the client if not received a pong after specified timeout
        /// </summary>
        /// <param name="endPoint">Client to send pings to</param>
        private async Task PingClient(EndPoint endPoint)
        {
            int Sequence = 0;
            long PingSendTime = 0;
            lock (_PongAccess)
            {
                _PongSequence[endPoint] = (Sequence, PingSendTime);
            }
            while (true)
            {
                Sequence = (Sequence + 1) % _configuration.MaxSequence; // Fist send must be 1
                PingSendTime = DateTime.UtcNow.Ticks;
                lock (_PongAccess)
                {
                    if (!_PongSequence.ContainsKey(endPoint))
                        return;
                    _PongSequence[endPoint] = (Sequence, PingSendTime);
                }
                SendAsync(endPoint, new PingHeader
                {
                    Sequence = (ushort)Sequence
                });
                await Task.Delay(_configuration.PingDelay);
            }
        }

        private async Task CheckForTimeout(EndPoint endPoint)
        {
            while (true)
            {
                var nowTime = DateTime.UtcNow.Ticks;
                long lastPacketTime;
                lock (_lastPacketTimeAccess)
                {
                    if (!_lastPacketTimes.TryGetValue(endPoint, out lastPacketTime))
                        return;
                }
                if (nowTime - lastPacketTime > 5 * TimeSpan.TicksPerSecond)
                    Disconnect(endPoint, DisconnectReason.Timeout);
                await Task.Delay(_configuration.TimeoutRefreshDelay);
            }
        }
    }
}
