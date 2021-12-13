using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class PongHandler : BasePacketHandler<PongHeader>
    {
        private readonly LiteNetConnectionPinger _pinger;

        public PongHandler(
            LiteNetConnectionPinger pinger)
        {
            _pinger = pinger;
        }

        public override Task Handle(EndPoint endPoint, PongHeader packet, ref SpanBufferReader reader)
        {
            _pinger.HandlePong(endPoint, packet.Sequence, packet.Time);
            return Task.CompletedTask;
        }
    }
}
