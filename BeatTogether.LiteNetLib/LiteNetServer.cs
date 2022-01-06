using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Delegates;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using NetCoreServer;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetServer : ConcurrentUdpServer
    {
        // Milliseconds between every ping
        public const int PingDelay = 1000;

        // Milliseconds without pong before client will timeout
        public const int TimeoutDelay = 5000;

        public event ClientConnectHandler ClientConnectEvent = null!;
        public event ClientDisconnectHandler ClientDisconnectEvent = null!;

        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<int, TaskCompletionSource<long>>> _pongTasks = new();
        private readonly ConcurrentDictionary<EndPoint, CancellationTokenSource> _pingCts = new();
        private readonly ConcurrentDictionary<EndPoint, long> _connectionTimes = new();

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
            : base(endPoint)
        {
            _configuration = configuration;
            _packetRegistry = packetRegistry;
            _serviceProvider = serviceProvider;
            _packetLayer = packetLayer;
        }

        protected override void OnStarted()
            => ReceiveAsync();

        protected override void OnReceived(EndPoint endPoint, ReadOnlySpan<byte> buffer)
        {
            ReceivePacket(endPoint, buffer);
            ReceiveAsync();
        }

        private void ReceivePacket(EndPoint endPoint, ReadOnlySpan<byte> buffer)
        {
            if (_packetLayer != null)
            {
                Span<byte> layerBuffer = new(buffer.ToArray());
                _packetLayer.ProcessInboundPacket(endPoint, ref layerBuffer);
                buffer = layerBuffer;
            }

            HandlePacket(endPoint, buffer);
        }

        internal protected virtual void HandlePacket(EndPoint endPoint, ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > 0)
            {
                var bufferReader = new SpanBufferReader(buffer);
                PacketProperty property = (PacketProperty)(buffer[0] & 0x1f); // 0x1f 00011111
                if (!_packetRegistry.TryCreatePacket(property, out var packet))
                    return;
                packet.ReadFrom(ref bufferReader);
                var packetHandlerType = typeof(IPacketHandler<>)
                        .MakeGenericType(packet.GetType());
                var packetHandler = _serviceProvider.GetService(packetHandlerType);
                if (packetHandler == null)
                    return;
                ((IPacketHandler)packetHandler).Handle(endPoint, packet, ref bufferReader);
            }
        }

        public override Task SendAsync(EndPoint endPoint, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_packetLayer != null)
            {
                Span<byte> layerBuffer = new(buffer.ToArray());
                _packetLayer.ProcessOutBoundPacket(endPoint, ref layerBuffer);
                buffer = new ReadOnlyMemory<byte>(layerBuffer.ToArray());
            }
            return base.SendAsync(endPoint, buffer, cancellationToken);
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
        public virtual bool ShouldAcceptConnection(EndPoint endPoint, ref SpanBufferReader additionalData)
            => false;

        /// <summary>
        /// Sends a raw serializable packet to an endpoint
        /// </summary>
        /// <param name="endPoint">Endpoint to send packet to</param>
        /// <param name="packet">The raw data to send (must include LiteNetLib headers)</param>
        public virtual void SendAsync(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            packet.WriteTo(ref bufferWriter);
            SendAsync(endPoint, bufferWriter.Data);
        }

        /// <summary>
        /// Sends a raw serializable packe to an endpoint
        /// </summary>
        /// <param name="endPoint">Endpoint to send packet to</param>
        /// <param name="packet">The raw data to send (must include LiteNetLib headers)</param>
        /// <returns>Task that is completed when the packet has been sent</returns>
        public virtual Task Send(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            packet.WriteTo(ref bufferWriter);
            return SendAsync(endPoint, new ReadOnlyMemory<byte>(bufferWriter.Data.ToArray()), CancellationToken.None);
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
            SendAsync(endPoint, new DisconnectHeader
            {
                ConnectionTime = connectionTime
            });
            HandleDisconnect(endPoint, reason);
        }

        internal void HandleConnect(EndPoint endPoint, long connectionTime)
        {
            _connectionTimes[endPoint] = connectionTime;
            PingClient(endPoint);
            OnConnect(endPoint);
            ClientConnectEvent?.Invoke(endPoint);
        }

        internal void HandleDisconnect(EndPoint endPoint, DisconnectReason reason)
        {
            _connectionTimes.TryRemove(endPoint, out _);
            if (_pingCts.TryRemove(endPoint, out var ping))
                ping.Cancel();
            OnDisconnect(endPoint, reason);
            ClientDisconnectEvent?.Invoke(endPoint, reason);
        }

        /// <summary>
        /// Handles a pong that was sent in response to a ping
        /// </summary>
        /// <param name="endPoint">Client that sent the pong</param>
        /// <param name="sequence">Sequence id of the pong</param>
        /// <param name="time">Time specified by pong</param>
        internal void HandlePong(EndPoint endPoint, int sequence, long time)
        {
            if (_pongTasks.TryRemove(endPoint, out var pongs))
                if (pongs.TryRemove(sequence, out var task))
                    task.SetResult(time);
        }

        /// <summary>
        /// Creates a loop where it will ping the client and wait for a pong
        /// Will disconnect the client if not received a pong after specified timeout
        /// </summary>
        /// <param name="endPoint">Client to send pings to</param>
        private async void PingClient(EndPoint endPoint)
        {
            CancellationTokenSource timeoutCts = new CancellationTokenSource();

            int sequence = 1; // MUST BE ONE OR CLIENT WILL IGNORE
            var cancellation = _pingCts.GetOrAdd(endPoint, _ => new());
            while (!cancellation.IsCancellationRequested)
            {
                var stopwatch = new Stopwatch();
                var pongTask = _pongTasks.GetOrAdd(endPoint, _ => new())
                    .GetOrAdd(sequence, _ => new()).Task
                    .ContinueWith(t => // Don't do anything with time returned by client 
                    {
                        stopwatch.Stop();
                        timeoutCts.Cancel();
                        timeoutCts = new CancellationTokenSource();
                        var latency = stopwatch.ElapsedMilliseconds / 2;
                        OnLatencyUpdate(endPoint, (int)latency);

                        Task.Delay(TimeoutDelay, timeoutCts.Token).ContinueWith(timeout =>
                        {
                            if (!timeout.IsCanceled)
                                Disconnect(endPoint, DisconnectReason.Timeout);
                        });
                    });

                await Send(endPoint, new PingHeader
                {
                    Sequence = (ushort)sequence
                });
                stopwatch.Start();
                sequence = (sequence + 1) % _configuration.MaxSequence;
                await Task.Delay(PingDelay);
            }
        }
    }
}
