using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Extensions;
using BeatTogether.LiteNetLib.Tests.Utilities;
using Krypton.Buffers;
using LiteNetLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Threading;

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
        private ListenerService _serverListener;

        public const int TestTimeout = 4000;

        [SetUp]
        public void Init()
        {
            _clientNetListener = new EventBasedNetListener();
            _clientNetManager = new NetManager(_clientNetListener);
            _clientNetManager.Start();

            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddLogging(builder =>
                    builder
                        .AddDebug()
                        .SetMinimumLevel(LogLevel.Trace)
                    )
                .AddSingleton<LiteNetConfiguration>()
                .AddSingleton<ListenerService>()
                .AddSingleton<ILiteNetListener, ListenerService>(x => x.GetRequiredService<ListenerService>())
                .AddSingleton<TestServer>()
                .AddHostedService(x => x.GetRequiredService<TestServer>())
                .AddSingleton<LiteNetServer, TestServer>(x => x.GetRequiredService<TestServer>())
                .AddSingleton<LiteNetReliableDispatcher>()
                .AddSingleton<LiteNetPacketReader, DebugReader>()
                .AddLiteNetMessaging();

            _serviceProvider = serviceCollection.BuildServiceProvider();
            _server = _serviceProvider.GetService<TestServer>();
            _serverListener = _serviceProvider.GetService<ListenerService>();

            _server.StartAsync(CancellationToken.None);
        }

        [TearDown]
        public void TearDown()
        {
            _clientNetManager.Stop();
            _serviceProvider.Dispose();
        }



        [Test, Timeout(TestTimeout)]
        public void ConnectionByIpV4()
        {
            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");

            while (_clientNetManager.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }

            Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void DeliveryTest()
        {
            bool msgDelivered = false;
            bool msgReceived = false;
            const int testSize = 250 * 1024;
            _clientNetListener.DeliveryEvent += (peer, obj) =>
            {
                Assert.AreEqual(5, (int)obj);
                msgDelivered = true;
            };
            _clientNetListener.PeerConnectedEvent += peer =>
            {
                int testData = 5;
                byte[] arr = new byte[testSize];
                arr[0] = 196;
                arr[7000] = 32;
                arr[12499] = 200;
                arr[testSize - 1] = 254;
                peer.SendWithDeliveryEvent(arr, 0, DeliveryMethod.ReliableUnordered, testData);
            };
            _serverListener.ReceiveConnectedEvent += (endPoint, data, method) =>
            {
                var reader = new SpanBufferReader(data);
                Assert.AreEqual(testSize, reader.RemainingSize);
                Assert.AreEqual(196, reader.RemainingData[0]);
                Assert.AreEqual(32, reader.RemainingData[7000]);
                Assert.AreEqual(200, reader.RemainingData[12499]);
                Assert.AreEqual(254, reader.RemainingData[testSize - 1]);
                msgReceived = true;
            };

            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");

            while (_clientNetManager.ConnectedPeersCount != 1 || !msgDelivered || !msgReceived)
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }

            Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void DisconnectFromServerTest()
        {
            var clientDisconnected = false;
            var serverDisconnected = false;
            _clientNetListener.PeerDisconnectedEvent += (peer, info) => { clientDisconnected = true; };
            _serverListener.DisconnectedEvent += (endPoint, reason, data) => { serverDisconnected = true; };

            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");
            while (_clientNetManager.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }

            // TODO: implement disconnect method on server

            while (!(clientDisconnected && serverDisconnected))
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }

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
            _serverListener.DisconnectedEvent += (endPoint, reason, data) =>
            {
                Assert.AreEqual(Enums.DisconnectReason.RemoteConnectionClose, reason);
                serverDisconnected = true;
            };

            NetPeer serverPeer = _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");
            while (_clientNetManager.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }

            //User server peer from client
            serverPeer.Disconnect();

            while (!(clientDisconnected && serverDisconnected))
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }

            Assert.True(clientDisconnected);
            Assert.True(serverDisconnected);
            Assert.AreEqual(0, _clientNetManager.ConnectedPeersCount);
        }
    }
}
