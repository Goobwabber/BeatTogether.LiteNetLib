using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class UnconnectedPacketHandler : IPacketHandler<UnconnectedHeader>
    {
        private readonly ILiteNetListener _listener;

        public UnconnectedPacketHandler(
            ILiteNetListener listener)
        {
            _listener = listener;
        }

        public Task Handle(IPEndPoint endPoint, UnconnectedHeader packet, ref SpanBufferReader reader)
        {
            _listener.OnNetworkReceiveUnconnected(endPoint, ref reader, UnconnectedMessageType.BasicMessage);
            return Task.CompletedTask;
        }
    }
}
