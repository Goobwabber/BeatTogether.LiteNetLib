using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class AckPacketHandler : IPacketHandler<AckHeader>
    {
        public Task Handle(IPEndPoint endPoint, AckHeader packet, ref SpanBufferReader reader)
        {
            throw new NotImplementedException(); // TODO
        }
    }
}
