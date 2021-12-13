using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Delegates;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using NetCoreServer;
using System;
using System.Collections.Concurrent;
using System.Net;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetServer : UdpServer
    {
        public event ClientConnectHandler ClientConnectEvent;
        public event ClientDisconnectHandler ClientDisconnectEvent;

        private readonly ConcurrentDictionary<EndPoint, long> _connectionTimes = new();
        private readonly LiteNetPacketReader _packetReader;
        private readonly IServiceProvider _serviceProvider;

        public LiteNetServer(
            IPEndPoint endPoint,
            LiteNetPacketReader packetReader,
            IServiceProvider serviceProvider) 
            : base(endPoint)
        {
            _packetReader = packetReader;
            _serviceProvider = serviceProvider;
        }

        protected override void OnStarted()
            => ReceiveAsync();

        protected override void OnReceived(EndPoint endPoint, ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > 0)
            {
                var bufferReader = new SpanBufferReader(buffer);
                INetSerializable packet = _packetReader.ReadFrom(ref bufferReader);
                var packetHandlerType = typeof(IPacketHandler<>)
                        .MakeGenericType(packet.GetType());
                var packetHandler = _serviceProvider.GetService(packetHandlerType);
                if (packetHandler == null)
                    return;
                ((IPacketHandler)packetHandler).Handle(endPoint, packet, ref bufferReader);
            }
            ReceiveAsync();
        }

        public virtual void SendAsync(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            packet.WriteTo(ref bufferWriter);
            SendAsync(endPoint, bufferWriter.Data);
        }

        public void Disconnect(EndPoint endPoint)
            => Disconnect(endPoint, DisconnectReason.DisconnectPeerCalled);

        internal void Disconnect(EndPoint endPoint, DisconnectReason reason)
        {
            if (!_connectionTimes.TryRemove(endPoint, out long connectionTime))
                return;
            SendAsync(endPoint, new DisconnectHeader
            {
                ConnectionTime = connectionTime
            });
            HandleDisconnect(endPoint, reason);
        }

        internal void HandleConnect(EndPoint endPoint, long connectionTime)
        {
            _connectionTimes[endPoint] = connectionTime;
            ClientConnectEvent?.Invoke(endPoint);
        }

        internal void HandleDisconnect(EndPoint endPoint, DisconnectReason reason)
        {
            _connectionTimes.TryRemove(endPoint, out _);
            ClientCleanupEvent?.Invoke(endPoint);
            ClientDisconnectEvent?.Invoke(endPoint, reason);
        }
    }
}
