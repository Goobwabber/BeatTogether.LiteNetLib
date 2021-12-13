using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class AckPacketHandler : BasePacketHandler<AckHeader>
    {
        private readonly ReliableDispatcher _reliableDispatcher;

        public AckPacketHandler(
            ReliableDispatcher reliableDispatcher)
        {
            _reliableDispatcher = reliableDispatcher;
        }

        public override Task Handle(EndPoint endPoint, AckHeader packet, ref SpanBufferReader reader)
        {
            foreach (int acknowledgement in packet.Acknowledgements)
                _reliableDispatcher.Acknowledge(endPoint, packet.ChannelId, acknowledgement);
            return Task.CompletedTask;
        }
    }
}
