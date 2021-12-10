using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Tests.Utilities
{
    public class TesterServer : LiteNetServer, IHostedService
    {
        private readonly ILogger _logger;

        public TesterServer(
            LiteNetPacketReader packetReader, 
            IServiceProvider serviceProvider,
            ILogger<TesterServer> logger) 
            : base(packetReader, serviceProvider, new IPEndPoint(IPAddress.Parse("127.0.0.1"), CommunicationTest.DefaultPort))
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
