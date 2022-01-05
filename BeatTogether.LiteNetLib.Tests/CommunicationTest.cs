using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Dispatchers;
using BeatTogether.LiteNetLib.Extensions;
using BeatTogether.LiteNetLib.Sources;
using BeatTogether.LiteNetLib.Tests.Utilities;
using Krypton.Buffers;
using LiteNetLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Tests
{
    [TestFixture]
    [Category("Communication")]
    public class CommunicationTest
    {
        private NetManager _clientNetManager;
        private EventBasedNetListener _clientNetListener;

        private ServiceProvider _serviceProvider;
        private TestServer _server;
        private ConnectedMessageDispatcher _messageDispatcher;
        private TestSource _messageSource;
        private ILogger _logger;

        public const int TestTimeout = 10000;

        [SetUp]
        public void Init()
        {
            var sw = new Stopwatch();
            sw.Start();
            _clientNetListener = new EventBasedNetListener();
            _clientNetManager = new NetManager(_clientNetListener);
            _clientNetManager.Start();

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
                .AddHostedService(x => x.GetRequiredService<TestServer>()) 
                .AddSingleton<LiteNetServer, TestServer>(x => x.GetRequiredService<TestServer>())
                .AddSingleton<ConnectedMessageDispatcher>()
                .AddSingleton<TestSource>()
                .AddSingleton<ConnectedMessageSource, TestSource>(x => x.GetRequiredService<TestSource>())
                .AddLiteNetMessaging();

            _serviceProvider = serviceCollection.BuildServiceProvider();
            _server = _serviceProvider.GetService<TestServer>();
            _messageDispatcher = _serviceProvider.GetService<ConnectedMessageDispatcher>();
            _messageSource = _serviceProvider.GetService<TestSource>();
            _logger = _serviceProvider.GetService<ILogger<CommunicationTest>>();
            var clientLogger = _serviceProvider.GetService<ILogger<NetManager>>();

            _clientNetListener.NetworkErrorEvent += (endPoint, error) =>
            {
                clientLogger.LogError($"Error: {error}");
            };

            _server.StartAsync(CancellationToken.None);

            sw.Stop();
            _logger.LogInformation($"Setup took '{sw.Elapsed}'");
        }

        [TearDown]
        public void TearDown()
        {
            var sw = new Stopwatch();
            sw.Start();
            _clientNetManager.Stop();
            _serviceProvider.Dispose();
            sw.Stop();
            _logger.LogInformation($"Teardown took '{sw.Elapsed}'");
        }



        public void WaitUntilConnected()
            => WaitUntil(() => _clientNetManager.ConnectedPeersCount == 1);

        public void WaitUntil(Func<bool> value)
        {
            while (!value())
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }
        }

        public void WaitWhileConnectedUntil(Func<bool> value)
        {
            while (!value())
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
                Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
            }
        }

        public byte[] MakeTest(int size, Dictionary<int, byte> test)
        {
            byte[] arr = new byte[size];
            foreach (var item in test)
                arr[item.Key] = item.Value;
            return arr;
        }

        public void AssertTest(Dictionary<int, byte> expected, byte[] actual, int offset)
        {
            foreach (var item in expected)
                Assert.AreEqual(item.Value, actual[item.Key + offset]);
        }



        [Test, Timeout(20000)]
        public void ConnectionByIpV4()
        {
            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");
            WaitUntilConnected();
            // Check if server can hold a connection for 15 seconds
            var time = Task.Delay(15000);
            WaitWhileConnectedUntil(() => time.IsCompleted); 
        }

        [Test, Timeout(TestTimeout)]
        public void DeliveryFromServerTest()
        {
            bool msgDelivered = false;
            bool msgReceived = false;

            const int testSize = 250;
            var test = new Dictionary<int, byte>
            {
                {0, 196},
                {testSize / 2, 56},
                {testSize - 1, 254}
            };

            _server.ClientConnectEvent += endPoint =>
            {
                _messageDispatcher.Send(endPoint, new ReadOnlySpan<byte>(MakeTest(testSize, test)), Enums.DeliveryMethod.ReliableOrdered);
                msgDelivered = true;
            };
            _clientNetListener.NetworkReceiveEvent += (endPoint, data, method) =>
            {
                // 4 is the size of the channeled message header
                Assert.AreEqual(testSize, data.UserDataSize);
                AssertTest(test, data.RawData, data.UserDataOffset);
                msgReceived = true;
            };

            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");
            WaitUntilConnected();
            WaitWhileConnectedUntil(() => msgDelivered && msgReceived);
            Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void DeliveryFromClientTest()
        {
            bool msgDelivered = false;
            bool msgReceived = false;

            const int testSize = 250;
            var test = new Dictionary<int, byte>
            {
                {0, 196},
                {testSize / 2, 56},
                {testSize - 1, 254}
            };

            _clientNetListener.DeliveryEvent += (peer, obj) =>
            {
                Assert.AreEqual(5, (int)obj);
                msgDelivered = true;
            };
            _clientNetListener.PeerConnectedEvent += peer =>
            {
                int testData = 5;
                peer.SendWithDeliveryEvent(MakeTest(testSize, test), 0, DeliveryMethod.ReliableUnordered, testData);
            };
            _messageSource.ReceiveConnectedEvent += (endPoint, data, method) =>
            {
                var reader = new SpanBufferReader(data);
                Assert.AreEqual(testSize, reader.RemainingSize);
                AssertTest(test, data, 0);
                msgReceived = true;
            };

            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");
            WaitUntilConnected();
            WaitWhileConnectedUntil(() => msgDelivered && msgReceived);
            Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void FragmentFromServerTest()
        {
            bool msgDelivered = false;
            bool msgReceived = false;

            const int testSize = 250 * 1024;
            var test = new Dictionary<int, byte>
            {
                {0, 196},
                {7000, 32},
                {12499, 200},
                {testSize - 1, 254}
            };

            _server.ClientConnectEvent += endPoint =>
            {
                _messageDispatcher.Send(endPoint, new ReadOnlySpan<byte>(MakeTest(testSize, test)), Enums.DeliveryMethod.ReliableOrdered);
                msgDelivered = true;
            };
            _clientNetListener.NetworkReceiveEvent += (endPoint, data, method) =>
            {
                // 4 is the size of the channeled message header
                Assert.AreEqual(testSize, data.UserDataSize);
                AssertTest(test, data.RawData, data.UserDataOffset);
                msgReceived = true;
            };

            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");
            WaitUntilConnected();
            WaitWhileConnectedUntil(() => msgDelivered && msgReceived);
            Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void FragmentFromClientTest()
        {
            bool msgDelivered = false;
            bool msgReceived = false;

            const int testSize = 250 * 1024;
            var test = new Dictionary<int, byte>
            {
                {0, 196},
                {7000, 32},
                {12499, 200},
                {testSize - 1, 254}
            };

            _clientNetListener.DeliveryEvent += (peer, obj) =>
            {
                Assert.AreEqual(5, (int)obj);
                msgDelivered = true;
            };
            _clientNetListener.PeerConnectedEvent += peer =>
            {
                int testData = 5;
                peer.SendWithDeliveryEvent(MakeTest(testSize, test), 0, DeliveryMethod.ReliableUnordered, testData);
            };
            _messageSource.ReceiveConnectedEvent += (endPoint, data, method) =>
            {
                var reader = new SpanBufferReader(data);
                Assert.AreEqual(testSize, reader.RemainingSize);
                AssertTest(test, data, 0);
                msgReceived = true;
            };

            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");
            WaitUntilConnected();
            WaitWhileConnectedUntil(() => msgDelivered && msgReceived);
            Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void DisconnectFromServerTest()
        {
            var serverConnected = false;
            var clientDisconnected = false;
            var serverDisconnected = false;
            EndPoint clientEndPoint = null!;
            _clientNetListener.PeerDisconnectedEvent += (peer, info) => clientDisconnected = true;
            _server.ClientDisconnectEvent += (endPoint, reason) => serverDisconnected = true;
            _server.ClientConnectEvent += endPoint =>
            {
                clientEndPoint = endPoint;
                serverConnected = true;
            };

            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");
            WaitUntilConnected();
            WaitUntil(() => serverConnected);
            _server.Disconnect(clientEndPoint, Enums.DisconnectReason.DisconnectPeerCalled);
            WaitUntil(() => clientDisconnected && serverDisconnected);
            Assert.True(clientDisconnected);
            Assert.True(serverDisconnected);
            Assert.AreEqual(0, _clientNetManager.ConnectedPeersCount);
        }
        
        [Test, Timeout(TestTimeout)]
        public void DisconnectFromClientTest()
        {
            var clientDisconnected = false;
            var serverDisconnected = false;

            _clientNetListener.PeerDisconnectedEvent += (peer, info) =>
            {
                Assert.AreEqual(DisconnectReason.DisconnectPeerCalled, info.Reason);
                Assert.AreEqual(0, _clientNetManager.ConnectedPeersCount);
                clientDisconnected = true;
            };
            _server.ClientDisconnectEvent += (endPoint, reason) =>
            {
                Assert.AreEqual(Enums.DisconnectReason.RemoteConnectionClose, reason);
                serverDisconnected = true;
            };

            NetPeer serverPeer = _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");
            WaitUntilConnected();
            serverPeer.Disconnect();
            WaitUntil(() => clientDisconnected && serverDisconnected);
            Assert.True(clientDisconnected);
            Assert.True(serverDisconnected);
            Assert.AreEqual(0, _clientNetManager.ConnectedPeersCount);
        }
    }
}
