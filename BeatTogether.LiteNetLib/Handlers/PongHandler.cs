using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class PongHandler : BasePacketHandler<PongHeader>
    {
        private readonly LiteNetServer _server;

        public PongHandler(
            LiteNetServer server)
        {
            _server = server;
        }

        public override Task Handle(EndPoint endPoint, PongHeader packet, ref SpanBufferReader reader)
        {
            _server.HandlePong(endPoint, packet.Sequence, packet.Time);
            return Task.CompletedTask;
        }
    }
}
