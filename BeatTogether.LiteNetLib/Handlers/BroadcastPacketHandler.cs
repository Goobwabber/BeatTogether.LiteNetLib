using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class BroadcastPacketHandler : BasePacketHandler<BroadcastHeader>
    {
        private readonly LiteNetServer _server;

        public BroadcastPacketHandler(
            LiteNetServer server)
        {
            _server = server;
        }

        public override Task Handle(EndPoint endPoint, BroadcastHeader packet, ref SpanBufferReader reader)
        {
            _server.OnReceiveUnconnected(endPoint, ref reader, UnconnectedMessageType.Broadcast);
            return Task.CompletedTask;
        }
    }
}
