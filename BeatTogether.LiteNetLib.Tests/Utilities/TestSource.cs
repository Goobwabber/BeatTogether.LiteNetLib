using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Sources;
using BeatTogether.LiteNetLib.Util;
using Microsoft.Extensions.Logging;
using System;
using System.Net;

namespace BeatTogether.LiteNetLib.Tests.Utilities
{
    class TestSource : ConnectedMessageSource
    {
        public event Action<EndPoint, byte[], DeliveryMethod> ReceiveConnectedEvent;

        private readonly ILogger _logger;

        public TestSource(
            LiteNetConfiguration configuration,
            LiteNetServer server,
            ILogger<TestSource> logger)
            : base(
                  configuration,
                  server)
        {
            _logger = logger;
        }

        public override void Signal(EndPoint remoteEndPoint, ChanneledHeader header, ref SpanBuffer reader)
        {
            //_logger.LogTrace($"Received connected message length '{reader.RemainingSize}' from '{remoteEndPoint}'");
            base.Signal(remoteEndPoint, header, ref reader);
        }

        public override void OnReceive(EndPoint remoteEndPoint, ref SpanBuffer reader, DeliveryMethod method)
        {
            _logger.LogInformation($"Received connected '{method}' message from '{remoteEndPoint}'");
            ReceiveConnectedEvent?.Invoke(remoteEndPoint, reader.RemainingData.ToArray(), method);
        }
    }
}
