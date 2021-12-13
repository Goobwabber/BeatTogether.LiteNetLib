using BeatTogether.LiteNetLib.Abstractions;
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
    public class LiteNetServer : UdpServer
    {
        // Milliseconds between every ping
        public const int PingDelay = 1000;

        // Milliseconds without pong before client will timeout
        public const int TimeoutDelay = 5000;

        public event ClientConnectHandler ClientConnectEvent;
        public event ClientDisconnectHandler ClientDisconnectEvent;
        public event ClientLatencyHandler ClientLatencyEvent;

        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<int, TaskCompletionSource<long>>> _pongTasks = new();
        private readonly ConcurrentDictionary<EndPoint, CancellationTokenSource> _pingCts = new();

        private readonly ConcurrentDictionary<EndPoint, long> _connectionTimes = new();
        private readonly LiteNetPacketReader _packetReader;
        private readonly IServiceProvider _serviceProvider;

        public LiteNetServer(
            IPEndPoint endPoint,
            LiteNetPacketReader packetReader,
            IServiceProvider serviceProvider) 
            : base(endPoint)
        {
            _packetReader = packetReader;
            _serviceProvider = serviceProvider;
        }

        protected override void OnStarted()
            => ReceiveAsync();

        protected override void OnReceived(EndPoint endPoint, ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > 0)
            {
                var bufferReader = new SpanBufferReader(buffer);
                INetSerializable packet = _packetReader.ReadFrom(ref bufferReader);
                var packetHandlerType = typeof(IPacketHandler<>)
                        .MakeGenericType(packet.GetType());
                var packetHandler = _serviceProvider.GetService(packetHandlerType);
                if (packetHandler == null)
                    return;
                ((IPacketHandler)packetHandler).Handle(endPoint, packet, ref bufferReader);
            }
            ReceiveAsync();
        }

        public virtual void SendAsync(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            packet.WriteTo(ref bufferWriter);
            SendAsync(endPoint, bufferWriter.Data);
        }

        public void Disconnect(EndPoint endPoint)
            => Disconnect(endPoint, DisconnectReason.DisconnectPeerCalled);

        internal void Disconnect(EndPoint endPoint, DisconnectReason reason)
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
            ClientConnectEvent?.Invoke(endPoint);
        }

        internal void HandleDisconnect(EndPoint endPoint, DisconnectReason reason)
        {
            _connectionTimes.TryRemove(endPoint, out _);
            if (_pingCts.TryRemove(endPoint, out var ping))
                ping.Cancel();
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
                        ClientLatencyEvent?.Invoke(endPoint, latency);

                        Task.Delay(TimeoutDelay, timeoutCts.Token).ContinueWith(timeout =>
                        {
                            if (!timeout.IsCanceled)
                                HandleDisconnect(endPoint, DisconnectReason.Timeout);
                        });
                    });

                stopwatch.Start();
                SendAsync(endPoint, new PingHeader
                {
                    Sequence = (ushort)sequence
                });
                await Task.Delay(PingDelay);
            }
        }
    }
}
