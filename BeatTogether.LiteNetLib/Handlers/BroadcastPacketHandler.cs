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
        private readonly ILiteNetListener _listener;

        public BroadcastPacketHandler(
            ILiteNetListener listener)
        {
            _listener = listener;
        }

        public override Task Handle(EndPoint endPoint, BroadcastHeader packet, ref SpanBufferReader reader)
        {
            _listener.OnNetworkReceiveUnconnected(endPoint, ref reader, UnconnectedMessageType.Broadcast);
            return Task.CompletedTask;
        }
    }
}
