using BeatTogether.LiteNetLib.Util;
using System;
using System.Net;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public interface IPacketLayer
    {
        public void ProcessInboundPacket(EndPoint endPoint, ref Span<byte> data);
        public void ProcessOutBoundPacket(EndPoint endPoint, ref Span<byte> data);
        public void ProcessInboundPacket(EndPoint endPoint, ref Memory<byte> data);
        public void ProcessOutBoundPacket(EndPoint endPoint, ref Memory<byte> data);
    }
}
