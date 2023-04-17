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
using System.Buffers;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetServer : AsyncUdpServer
    {
        public event ClientConnectHandler ClientConnectEvent = null!;
        public event ClientDisconnectHandler ClientDisconnectEvent = null!;

        private readonly ConcurrentDictionary<EndPoint, (int, int)> _PongSequence = new();
        private readonly ConcurrentDictionary<EndPoint, CancellationTokenSource> _pingCts = new();
        private readonly ConcurrentDictionary<EndPoint, long> _connectionTimes = new();
        private readonly ConcurrentDictionary<EndPoint, long> _lastPacketTimes = new();

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
            : base(endPoint, configuration.RecieveAsync, configuration.MaxHandlesWhileRecieving)
        {
            _configuration = configuration;
            _packetRegistry = packetRegistry;
            _serviceProvider = serviceProvider;
            _packetLayer = packetLayer;
        }

        public LiteNetServer(
            IPEndPoint endPoint,
            LiteNetConfiguration configuration,
            LiteNetPacketRegistry packetRegistry,
            IServiceProvider serviceProvider,
            bool RecvAsync,
            int AsyncCount,
            IPacketLayer? packetLayer = null)
            : base(endPoint, RecvAsync, AsyncCount)
        {
            _configuration = configuration;
            _packetRegistry = packetRegistry;
            _serviceProvider = serviceProvider;
            _packetLayer = packetLayer;
        }

        protected override void OnReceived(EndPoint endPoint, Memory<byte> buffer)
        {
            if (_lastPacketTimes.ContainsKey(endPoint))
                _lastPacketTimes[endPoint] = DateTime.UtcNow.Ticks;
            ReceivePacket(endPoint, buffer);
        }

        private void ReceivePacket(EndPoint endPoint, Memory<byte> buffer)
        {
            if (_packetLayer != null)
            {
                Memory<byte> IncommingData = new(buffer.ToArray());
                _packetLayer.ProcessInboundPacket(endPoint, ref IncommingData);
                buffer = IncommingData;
            }
            HandlePacket(endPoint, buffer);
        }

        internal protected virtual void HandlePacket(EndPoint endPoint, Memory<byte> buffer)
        {
            if (buffer.Length > 0)
            {
                var bufferReader = new MemoryBuffer(buffer);
                PacketProperty property = (PacketProperty)(buffer.Span[0] & 0x1f); // 0x1f 00011111
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

        public override async Task SendAsync(EndPoint endPoint, Memory<byte> buffer)
        {
            if (_packetLayer != null)
            {
                Memory<byte> OutBuffer = new(buffer.ToArray());
                _packetLayer.ProcessOutBoundPacket(endPoint, ref OutBuffer);
                buffer = OutBuffer;
            }
            await base.SendAsync(endPoint, buffer);
        }

        public override void SendSerial(EndPoint endPoint, Span<byte> buffer)
        {
            if (_packetLayer != null)
            {
                Span<byte> OutData = new(buffer.ToArray());
                _packetLayer.ProcessOutBoundPacket(endPoint, ref OutData);
                buffer = OutData;
            }
            base.SendSerial(endPoint, buffer);
        }

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
        /// Whether the server should accept a connection
        /// </summary>
        /// <param name="endPoint">Endpoint connection request was received from</param>
        /// <param name="additionalData">Additional data sent with the request</param>
        /// <returns></returns>
        public virtual bool ShouldAcceptConnection(EndPoint endPoint, ref MemoryBuffer additionalData)
            => false;

        /// <summary>
        /// Sends a raw serializable packet to an async endpoint
        /// </summary>
        /// <param name="endPoint">Endpoint to send packet to</param>
        /// <param name="packet">The raw data to send (must include LiteNetLib headers)</param>
        public async virtual void SendAsync(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new MemoryBuffer(GC.AllocateArray<byte>(412, true));
            packet.WriteTo(ref bufferWriter);
            await SendAsync(endPoint, bufferWriter.Data);
        }

        /// <summary>
        /// Sends a raw serializable packe to an endpoint
        /// </summary>
        /// <param name="endPoint">Endpoint to send packet to</param>
        /// <param name="packet">The raw data to send (must include LiteNetLib headers)</param>
        /// <returns>Task that is completed when the packet has been sent</returns>
        public virtual void Send(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new SpanBuffer(stackalloc byte[412]);
            packet.WriteTo(ref bufferWriter);
            SendSerial(endPoint, bufferWriter.Data);
    }

        /// <summary>
        /// Disconnects a connected endpoint
        /// </summary>
        /// <param name="endPoint">Endpoint to disconnect</param>
        /// <param name="reason">Reason for disconnecting</param>
        public void Disconnect(EndPoint endPoint, DisconnectReason reason = DisconnectReason.DisconnectPeerCalled)
        {
            if (!_connectionTimes.TryRemove(endPoint, out long connectionTime))
                return;
            Send(endPoint, new DisconnectHeader
            {
                ConnectionTime = connectionTime
            });
            HandleDisconnect(endPoint, reason);
        }

        public bool HasConnected(EndPoint endPoint)
            => _connectionTimes.TryGetValue(endPoint, out _);

        internal void HandleConnect(EndPoint endPoint, long connectionTime)
        {
            _connectionTimes[endPoint] = connectionTime;
            _lastPacketTimes[endPoint] = DateTime.UtcNow.Ticks;
            Task.Run(async () => await PingClient(endPoint));
            Task.Run(async () => await CheckForTimeout(endPoint));
            OnConnect(endPoint);
            ClientConnectEvent?.Invoke(endPoint);
        }

        internal void HandleDisconnect(EndPoint endPoint, DisconnectReason reason)
        {
            _connectionTimes.TryRemove(endPoint, out _);
            if (_pingCts.TryRemove(endPoint, out var ping))
                ping.Cancel();
            _PongSequence.TryRemove(endPoint, out _);
            _lastPacketTimes.TryRemove(endPoint, out _);
            OnDisconnect(endPoint, reason);
            ClientDisconnectEvent?.Invoke(endPoint, reason);
        }

        internal void HandlePong(EndPoint endPoint, int sequence, long time)
        {
            if(_PongSequence.TryGetValue(endPoint, out (int,int) ServerSequence))
            {
                if (ServerSequence.Item1 == sequence)
                    OnLatencyUpdate(endPoint, (DateTime.UtcNow.Millisecond - ServerSequence.Item2) / 2);
            }
        }

        /// <summary>
        /// Creates a loop where it will ping the client and wait for a pong
        /// Will disconnect the client if not received a pong after specified timeout
        /// </summary>
        /// <param name="endPoint">Client to send pings to</param>
        private async Task PingClient(EndPoint endPoint)
        {
            int Sequence = 0;
            int PingSendTime = 0;
            var cancellation = _pingCts.GetOrAdd(endPoint, _ => new());
            while (!cancellation.IsCancellationRequested)
            {
                Sequence = (Sequence + 1) % _configuration.MaxSequence; // Fist send must be 1
                PingSendTime = DateTime.UtcNow.Millisecond;
                _PongSequence[Endpoint] = (Sequence, PingSendTime);

                Send(endPoint, new PingHeader
                {
                    Sequence = (ushort)Sequence
                });
                await Task.Delay(_configuration.PingDelay);
            }
        }

        private async Task CheckForTimeout(EndPoint endPoint)
        {
            var cancellation = _pingCts.GetOrAdd(endPoint, _ => new());
            while (!cancellation.IsCancellationRequested)
            {
                var nowTime = DateTime.UtcNow.Ticks;
                var lastPacketTime = _lastPacketTimes[endPoint];
                if (nowTime - lastPacketTime > 5 * TimeSpan.TicksPerSecond)
                    Disconnect(endPoint, DisconnectReason.Timeout);
                await Task.Delay(_configuration.TimeoutRefreshDelay);
            }
        }
    }
}
