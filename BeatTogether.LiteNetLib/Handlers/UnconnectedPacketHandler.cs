using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class UnconnectedPacketHandler : BasePacketHandler<UnconnectedHeader>
    {
        private readonly LiteNetServer _server;

        public UnconnectedPacketHandler(
            LiteNetServer server)
        {
            _server = server;
        }

        public override Task Handle(EndPoint endPoint, UnconnectedHeader packet, ref SpanBufferReader reader)
        {
            _server.OnReceiveUnconnected(endPoint, ref reader, UnconnectedMessageType.BasicMessage);
            return Task.CompletedTask;
        }
    }
}
