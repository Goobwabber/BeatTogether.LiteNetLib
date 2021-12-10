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

        private readonly ILogger _logger;

        public TestServer(
            LiteNetReliableDispatcher reliableDispatcher,
            LiteNetPacketReader packetReader, 
            IServiceProvider serviceProvider,
            ILogger<TestServer> logger) 
            : base(new IPEndPoint(IPAddress.Loopback, Port), reliableDispatcher, packetReader, serviceProvider)
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
    }
}
