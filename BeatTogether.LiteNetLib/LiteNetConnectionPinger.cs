using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetConnectionPinger
    {
        // Milliseconds between every ping
        public const int PingDelay = 1000;

        // Milliseconds before connection times out
        public const int TimeoutDelay = 5000;

        private readonly ConcurrentDictionary<EndPoint, int> _latencies = new();
        private readonly ConcurrentDictionary<EndPoint, CancellationTokenSource> _pingCancellers = new();
        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<int, TaskCompletionSource<long>>> _pongTasks = new();
        private readonly LiteNetConfiguration _configuration;
        private readonly LiteNetServer _server;
        private readonly ILiteNetListener _listener;
        private readonly ILogger _logger;

        public LiteNetConnectionPinger(
            LiteNetConfiguration configuration,
            LiteNetServer server,
            ILiteNetListener listener,
            ILogger<LiteNetConnectionPinger> logger)
        {
            _configuration = configuration;
            _server = server;
            _listener = listener;
            _logger = logger;

            _server.ClientConnectEvent += AddConnection;
            _server.ClientDisconnectEvent += Cleanup;
        }

        public async void AddConnection(EndPoint endPoint)
        {
            // TODO: this current setup will cause issues if a packet is dropped

            Cleanup(endPoint);
            _logger.LogTrace($"Beginning {endPoint} ping cycle");

            int pingSequence = 1;
            var pingCanceller = _pingCancellers.GetOrAdd(endPoint, _ => new());
            while (!pingCanceller.IsCancellationRequested)
            {
                await Task.Delay(PingDelay);

                var timeoutTask = Task.Delay(TimeoutDelay);
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                _server.SendAsync(endPoint, new PingHeader
                {
                    Sequence = (ushort)pingSequence
                });

                long pongTime;
                var pongTask = _pongTasks.GetOrAdd(endPoint, _ => new())
                        .GetOrAdd(pingSequence, _ => new()).Task;
                await Task.WhenAny(pongTask, timeoutTask);
                if (pongTask.IsCompleted)
                {
                    pongTime = pongTask.Result;
                }
                else
                {
                    _logger.LogWarning($"{endPoint} timed out!");
                    _server.Disconnect(endPoint, DisconnectReason.Timeout);
                    return;
                }

                stopwatch.Stop();

                var latency = (int)stopwatch.ElapsedMilliseconds / 2;
                _latencies.AddOrUpdate(endPoint, latency, (_, _) => latency);
                _logger.LogTrace($"{endPoint} latency: {latency}");

                pingSequence = (pingSequence + 1) % _configuration.MaxSequence;
            }
        }

        protected void Cleanup(EndPoint endPoint, DisconnectReason reason, ref SpanBufferReader reader)
            => Cleanup(endPoint);

        public void Cleanup(EndPoint endPoint)
        {
            if (_pingCancellers.TryGetValue(endPoint, out var canceller))
                canceller.Cancel();
        }

        public void HandlePong(EndPoint endPoint, int sequence, long time)
        {
            if (_pongTasks.TryGetValue(endPoint, out var pongs))
                if (pongs.TryRemove(sequence, out var pongTask))
                    pongTask.SetResult(time);
        }

        public bool TryGetLatency(EndPoint endPoint, [MaybeNullWhen(false)] out int latency)
            => _latencies.TryGetValue(endPoint, out latency);
    }
}
