using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class PongHandler : IPacketHandler<PongHeader>
    {
        public Task Handle(IPEndPoint endPoint, PongHeader packet, ref SpanBufferReader reader)
        {
            throw new NotImplementedException(); // TODO
        }
    }
}
