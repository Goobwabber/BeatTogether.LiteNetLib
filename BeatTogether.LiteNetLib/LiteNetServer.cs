using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using NetCoreServer;
using System;
using System.Net;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetServer : UdpServer
    {
        private readonly LiteNetPacketReader _packetReader;

        public LiteNetServer(
            LiteNetPacketReader packetReader,
            IPEndPoint endPoint) 
            : base(endPoint)
        {
            _packetReader = packetReader;
        }

        public void SendRaw(EndPoint endPoint, INetSerializable packet)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            // TODO: Add encryption
            packet.WriteTo(ref bufferWriter);
            SendAsync(endPoint, bufferWriter.Data);
        }

        public void SendUnreliable(EndPoint endPoint, INetSerializable message)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            // TODO: Add encryption
            new UnreliableHeader().WriteTo(ref bufferWriter); // Write unreliable header
            message.WriteTo(ref bufferWriter); // Write packet
            SendAsync(endPoint, bufferWriter); // Send data
        }

        protected override void OnStarted() 
            => ReceiveAsync();

        protected override void OnReceived(EndPoint endPoint, ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > 0)
            {
                var bufferReader = new SpanBufferReader(buffer);
                // TODO: Add encryption
                INetSerializable packet = _packetReader.ReadFrom(ref bufferReader);
            }
            ReceiveAsync();
        }
    }
}
