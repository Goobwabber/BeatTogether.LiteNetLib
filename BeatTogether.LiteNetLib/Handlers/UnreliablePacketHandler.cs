using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class UnreliablePacketHandler : BasePacketHandler<UnreliableHeader>
    {
        private readonly ILiteNetListener _listener;

        public UnreliablePacketHandler(
            ILiteNetListener listener)
        {
            _listener = listener;
        }

        public override Task Handle(EndPoint endPoint, UnreliableHeader packet, ref SpanBufferReader reader)
        {
            _listener.OnNetworkReceive(endPoint, ref reader, DeliveryMethod.Unreliable);
            return Task.CompletedTask;
        }
    }
}
