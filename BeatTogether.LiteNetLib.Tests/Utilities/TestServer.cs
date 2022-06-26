using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using Krypton.Buffers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Tests.Utilities
{
    public class TestServer : LiteNetServer, IHostedService
    {
        public const int _Port = 9050;

        private readonly ILogger _logger;

        public TestServer(
            LiteNetConfiguration configuration,
            LiteNetPacketRegistry registry,
            IServiceProvider serviceProvider,
            ILogger<TestServer> logger) 
            : base(
                  new IPEndPoint(IPAddress.Loopback, _Port),
                  configuration,
                  registry,
                  serviceProvider)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Start();
            _logger.LogInformation($"Started test server on {Endpoint}");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stop();
            _logger.LogInformation($"Stopped test server on {Endpoint}");
            return Task.CompletedTask;
        }

        protected override void HandlePacket(EndPoint endPoint, ReadOnlySpan<byte> buffer)
        {
            _logger.LogTrace($"Recieved from '{endPoint}': [{string.Join(", ", buffer.ToArray())}]");
            try
            {
                base.HandlePacket(endPoint, buffer);
            }
            catch(ObjectDisposedException e)
            {

            }
        }

        public override async Task<bool> SendAsync(EndPoint endPoint, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            bool value = await base.SendAsync(endPoint, buffer, cancellationToken);
            if (buffer.Length < 100)
                _logger.LogTrace($"Sent packet [{string.Join(", ", buffer.ToArray())}]");
            return value;
        }

        public override void OnConnect(EndPoint endPoint)
        {
            _logger.LogInformation($"Client connected from '{endPoint}'");
        }

        public override void OnDisconnect(EndPoint endPoint, DisconnectReason reason)
        {
            _logger.LogInformation($"Client disconnected from '{endPoint}' with reason '{reason}'");
        }

        public override void OnLatencyUpdate(EndPoint endPoint, int latency)
        {
            _logger.LogInformation($"Latency for '{endPoint}' updated to '{latency}'");
        }

        public override bool ShouldAcceptConnection(EndPoint endPoint, ref SpanBufferReader additionalData)
        {
            _logger.LogInformation($"Connection request received from '{endPoint}'");
            return true;
        }
    }
}
