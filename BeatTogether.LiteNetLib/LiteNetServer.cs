using BeatTogether.LiteNetLib.Abstractions;
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

        public void Disconnect(EndPoint endPoint)
        {
            if (!_connectionTimes.TryRemove(endPoint, out long connectionTime))
                return;
            SendAsync(endPoint, new DisconnectHeader
            {
                ConnectionTime = connectionTime
            });
        }

        public void AddConnection(EndPoint endPoint, long connectionTime)
            => _connectionTimes[endPoint] = connectionTime;

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

        public void SendAsync(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            packet.WriteTo(ref bufferWriter);
            SendAsync(endPoint, bufferWriter.Data);
        }
    }
}
