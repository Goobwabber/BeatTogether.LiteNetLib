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
        private readonly LiteNetReliableDispatcher _reliableDispatcher;
        private readonly LiteNetPacketReader _packetReader;
        private readonly IServiceProvider _serviceProvider;

        public LiteNetServer(
            IPEndPoint endPoint,
            LiteNetReliableDispatcher reliableDispatcher,
            LiteNetPacketReader packetReader,
            IServiceProvider serviceProvider) 
            : base(endPoint)
        {
            _reliableDispatcher = reliableDispatcher;
            _packetReader = packetReader;
            _serviceProvider = serviceProvider;

            _reliableDispatcher.DispatchEvent += (endPoint, buffer)
                => SendAsync(endPoint, buffer);
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

        public void HandleConnect(EndPoint endPoint, long connectionTime)
        {
            _connectionTimes[endPoint] = connectionTime;
            _reliableDispatcher.Cleanup(endPoint);
            ClientConnectEvent?.Invoke(endPoint);
        }

        public void HandleDisconnect(EndPoint endPoint, DisconnectReason reason, ref SpanBufferReader data)
        {
            _connectionTimes.TryRemove(endPoint, out _);
            _reliableDispatcher.Cleanup(endPoint);
            ClientDisconnectEvent?.Invoke(endPoint, reason, ref data);
        }

        public void Disconnect(EndPoint endPoint, DisconnectReason reason)
        {
            if (!_connectionTimes.TryRemove(endPoint, out long connectionTime))
                return;
            SendAsync(endPoint, new DisconnectHeader
            {
                ConnectionTime = connectionTime
            });
            var emptyData = new SpanBufferReader();
            HandleDisconnect(endPoint, reason, ref emptyData);
        }

        public void Send(EndPoint endPoint, ReadOnlySpan<byte> message, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            var memory = new ReadOnlyMemory<byte>(message.ToArray());
            if (method != DeliveryMethod.ReliableOrdered)
                throw new NotImplementedException();
            _reliableDispatcher.Send(endPoint, memory);
        }

        public void SendUnreliable(EndPoint endPoint, ReadOnlySpan<byte> message)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            new UnreliableHeader().WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message);
            SendAsync(endPoint, bufferWriter);
        }

        public void SendUnconnected(EndPoint endPoint, ReadOnlySpan<byte> message)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            new UnconnectedHeader().WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message);
            SendAsync(endPoint, bufferWriter);
        }

        public virtual void SendAsync(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            packet.WriteTo(ref bufferWriter);
            SendAsync(endPoint, bufferWriter.Data);
        }
    }
}
