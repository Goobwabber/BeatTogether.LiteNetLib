using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Extensions;
using BeatTogether.LiteNetLib.Tests.Utilities;
using Krypton.Buffers;
using LiteNetLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.Net;
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
                .AddSingleton<LiteNetAcknowledger>()
                .AddSingleton<LiteNetPacketReader, DebugReader>()
                .AddLiteNetMessaging();

            _serviceProvider = serviceCollection.BuildServiceProvider();
            _server = _serviceProvider.GetService<TestServer>();
            _serverListener = _serviceProvider.GetService<ListenerService>();
            var clientLogger = _serviceProvider.GetService<ILogger<NetManager>>();

            _clientNetListener.NetworkErrorEvent += (endPoint, error) =>
            {
                clientLogger.LogError($"Error: {error}");
            };

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
        public void DeliveryFromServerTest()
        {
            bool msgDelivered = false;
            bool msgReceived = false;
            const int testSize = 250;
            _serverListener.ConnectedEvent += endPoint =>
            {
                msgDelivered = true;
                byte[] arr = new byte[testSize];
                arr[0] = 196;
                arr[testSize - 1] = 254;
                _server.Send(endPoint, new ReadOnlySpan<byte>(arr));
            };
            _clientNetListener.NetworkReceiveEvent += (endPoint, data, method) =>
            {
                // 4 is the size of the channeled message header
                Assert.AreEqual(testSize, data.RawDataSize - 4);
                Assert.AreEqual(196, data.RawData[0 + 4]);
                Assert.AreEqual(254, data.RawData[testSize + 4 - 1]);
                Assert.AreEqual(DeliveryMethod.ReliableOrdered, method);
                msgReceived = true;
            };

            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");

            while (_clientNetManager.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }

            while (!msgDelivered || !msgReceived)
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
                Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
            }

            Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void DeliveryFromClientTest()
        {
            bool msgDelivered = false;
            bool msgReceived = false;
            const int testSize = 250;
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
                arr[testSize - 1] = 254;
                peer.SendWithDeliveryEvent(arr, 0, DeliveryMethod.ReliableUnordered, testData);
            };
            _serverListener.ReceiveConnectedEvent += (endPoint, data, method) =>
            {
                var reader = new SpanBufferReader(data);
                Assert.AreEqual(testSize, reader.RemainingSize);
                Assert.AreEqual(196, reader.RemainingData[0]);
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
        public void FragmentFromServerTest()
        {
            bool msgDelivered = false;
            bool msgReceived = false;
            const int testSize = 250 * 1024;
            _serverListener.ConnectedEvent += endPoint =>
            {
                msgDelivered = true;
                byte[] arr = new byte[testSize];
                arr[0] = 196;
                arr[7000] = 32;
                arr[12499] = 200;
                arr[testSize - 1] = 254;
                _server.Send(endPoint, new ReadOnlySpan<byte>(arr));
            };
            _clientNetListener.NetworkReceiveEvent += (endPoint, data, method) =>
            {
                // 4 is the size of the channeled message header
                Assert.AreEqual(testSize, data.RawDataSize - 4);
                Assert.AreEqual(196, data.RawData[0 + 4]);
                Assert.AreEqual(32, data.RawData[7000 + 4]);
                Assert.AreEqual(200, data.RawData[12499 + 4]);
                Assert.AreEqual(254, data.RawData[testSize + 4 - 1]);
                Assert.AreEqual(DeliveryMethod.ReliableOrdered, method);
                msgReceived = true;
            };

            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");

            while (_clientNetManager.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }

            while (!msgDelivered || !msgReceived)
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
                Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
            }

            Assert.AreEqual(1, _clientNetManager.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void FragmentFromClientTest()
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
            EndPoint clientEndPoint = null!;
            _clientNetListener.PeerDisconnectedEvent += (peer, info) => clientDisconnected = true;
            _serverListener.DisconnectedEvent += (endPoint, reason, data) => serverDisconnected = true;
            _serverListener.ConnectedEvent += endPoint => clientEndPoint = endPoint;

            _clientNetManager.Connect("127.0.0.1", TestServer.Port, "");
            while (_clientNetManager.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }

            _server.Disconnect(clientEndPoint);

            while (!clientDisconnected && !serverDisconnected)
            {
                Thread.Sleep(15);
                _clientNetManager.PollEvents();
            }

            Assert.True(clientDisconnected);
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
