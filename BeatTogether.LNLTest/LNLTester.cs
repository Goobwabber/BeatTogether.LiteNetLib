using System.Net;
using BeatTogether.LiteNetLib;
using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Dispatchers;
using BeatTogether.LiteNetLib.Extensions;
using BeatTogether.LiteNetLib.Sources;
using BeatTogether.LNLTest.Utilities;
using LiteNetLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BeatTogether.LNLTest
{
    public class LNLTester
    {
        private ILogger _logger;
        private ServiceProvider _serviceProvider;
        private TestServer _testServer;
        private ClientPool _clientPool;

        private void SetupServer()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection
                .AddLogging(builder =>
                    builder
                        .AddDebug()
                        .AddConsole()
                        .SetMinimumLevel(LogLevel.Trace)
                )
                .AddSingleton<LiteNetConfiguration>()
                .AddSingleton<TestServer>()
                .AddSingleton<LiteNetServer>()
                .AddHostedService(x => x.GetRequiredService<TestServer>())
                .AddSingleton<LiteNetServer, TestServer>(x => x.GetRequiredService<TestServer>())
                .AddSingleton<ConnectedMessageDispatcher>()
                .AddSingleton<TestSource>()
                .AddSingleton<ConnectedMessageSource, TestSource>(x => x.GetRequiredService<TestSource>())
                .AddLiteNetMessaging();

            _serviceProvider = serviceCollection.BuildServiceProvider();

            _logger = _serviceProvider.GetRequiredService<ILogger<LNLTester>>();

            _clientPool = new ClientPool(_serviceProvider);
        }
        void WaitUntilAllSettled()
            => WaitUntil(() => _clientPool.IsAllSettled);

        void WaitUntil(Func<bool> value)
        {
            while (!value())
            {
                Thread.Sleep(15);
                _clientPool.PollEvents();
            }
        }

        void ConnectWayTooManyClients(int count = 128)
        {
            for (int i = 0; i < count; i++)
            {
                _clientPool.Connect();
            }
        }

        void DisconnectBunchOfClients(int count = 128)
        {
            for (int i = 0; i < count; i++)
            {
                _clientPool.DisconnectOne();
            }
        }

        public async Task Run()
        {
            SetupServer();

            _testServer = _serviceProvider.GetRequiredService<TestServer>();
            await _testServer.StartAsync(CancellationToken.None);

            ConnectWayTooManyClients(256);
            WaitUntilAllSettled();
            
            _logger.LogInformation("All connected");

            bool stop = false;
            
            var thread = new Thread(() =>
            {
                int pollCtr = 0;
                int garbageCtr = 0;

                while (!stop)
                {
                    Thread.Sleep(10);
                    pollCtr++;
                    garbageCtr++;

                    if (garbageCtr > 50)
                    {
                        _clientPool.SendGarbage(DeliveryMethod.ReliableOrdered);
                    }
                    
                    if (pollCtr > 3)
                    {
                        pollCtr -= 3;
                        _clientPool.PollEvents();
                    }
                }
            });
            thread.Start();

            // 15 mins
            await Task.Delay(900000);
            
            stop = true;
            thread.Join();
            
            _logger.LogInformation("Starting cleanup...");

            _clientPool.DisconnectAll();
            WaitUntilAllSettled();
            
            _logger.LogInformation("All disconnected");
        }
    }
}

