using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class PingHandler : BasePacketHandler<PingHeader>
    {
        private readonly LiteNetServer _server;

        public PingHandler(
            LiteNetServer server)
        {
            _server = server;
        }

        public override Task Handle(EndPoint endPoint, PingHeader packet, ref SpanBufferReader reader)
        {
            _server.SendAsync(endPoint, new PongHeader
            {
                Sequence = packet.Sequence, // TODO: weird sequencing shit
                Time = DateTime.UtcNow.Ticks
            });
            return Task.CompletedTask;
        }
    }
}
