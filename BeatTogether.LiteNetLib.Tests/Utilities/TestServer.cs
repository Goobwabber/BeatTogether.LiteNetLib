﻿using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Util;
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
                  serviceProvider, 
                  5)
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

        protected override void HandlePacket(EndPoint endPoint, Span<byte> buffer)
        {
            _logger.LogTrace($"Recieved from '{endPoint}': [{string.Join(", ", buffer.ToArray())}]");
            try
            {
                base.HandlePacket(endPoint, buffer);
            }
            catch(ObjectDisposedException /*e*/)
            {

            }
        }

        public async override Task SendAsync(EndPoint endPoint, Memory<byte> buffer)
        {
            await base.SendAsync(endPoint, buffer);
            if (buffer.Length < 100)
                _logger.LogTrace($"Sent packet [{string.Join(", ", buffer.ToArray())}]");
            return;
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

        public override Task<bool> ShouldAcceptConnection(EndPoint endPoint, byte[] additionalData)
        {
            _logger.LogInformation($"Connection request received from '{endPoint}'");
            return Task.FromResult(true);
        }
    }
}
