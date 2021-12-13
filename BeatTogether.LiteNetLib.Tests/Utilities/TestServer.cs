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
        public const int Port = 9050;

        public event Action<EndPoint, byte[], DeliveryMethod> ReceiveConnectedEvent;
        public event Action<EndPoint, byte[], UnconnectedMessageType> ReceiveUnconnectedEvent;

        private readonly ILogger _logger;

        public TestServer(
            LiteNetConfiguration configuration,
            LiteNetPacketReader packetReader, 
            IServiceProvider serviceProvider,
            ILogger<TestServer> logger) 
            : base(
                  new IPEndPoint(IPAddress.Loopback, Port),
                  configuration,
                  packetReader, 
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

        public override void SendAsync(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            packet.WriteTo(ref bufferWriter);
            _logger.LogTrace($"Sending packet [{string.Join(", ", bufferWriter.Data.ToArray())}]");
            SendAsync(endPoint, bufferWriter.Data);
        }

        public override void OnConnect(EndPoint endPoint)
        {
            _logger.LogInformation($"Client connected from '{endPoint}'");
        }

        public override void OnDisconnect(EndPoint endPoint, DisconnectReason reason)
        {
            _logger.LogInformation($"Client disconnected from '{endPoint}' with reason '{reason}'");
        }

        public override void OnReceiveConnected(EndPoint endPoint, ref SpanBufferReader reader, DeliveryMethod deliveryMethod)
        {
            _logger.LogInformation($"Received connected '{deliveryMethod}' message from '{endPoint}'");
            ReceiveConnectedEvent?.Invoke(endPoint, reader.RemainingData.ToArray(), deliveryMethod);
        }

        public override void OnReceiveUnconnected(EndPoint endPoint, ref SpanBufferReader reader, UnconnectedMessageType messageType)
        {
            _logger.LogInformation($"Received unconnected '{messageType}' message from '{endPoint}'");
            ReceiveUnconnectedEvent?.Invoke(endPoint, reader.RemainingData.ToArray(), messageType);
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
