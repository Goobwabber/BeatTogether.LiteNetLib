using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Dispatchers;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class AckPacketHandler : BasePacketHandler<AckHeader>
    {
        private readonly ConnectedMessageDispatcher _messageDispatcher;

        public AckPacketHandler(
            ConnectedMessageDispatcher messageDispatcher)
        {
            _messageDispatcher = messageDispatcher;
        }

        public override Task Handle(EndPoint endPoint, AckHeader packet, ref SpanBufferReader reader)
        {
            if (_messageDispatcher == null)
                return Task.CompletedTask;
            foreach (int acknowledgement in packet.Acknowledgements)
                _messageDispatcher.Acknowledge(endPoint, packet.ChannelId, acknowledgement);
            return Task.CompletedTask;
        }
    }
}
