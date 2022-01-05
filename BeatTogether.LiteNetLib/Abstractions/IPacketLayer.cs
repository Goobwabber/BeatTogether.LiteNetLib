using System;
using System.Net;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public interface IPacketLayer
    {
        public void ProcessInboundPacket(EndPoint endPoint, Span<byte> data);
        public void ProcessOutBoundPacket(EndPoint endPoint, Span<byte> data);
    }
}
