using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
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
        private readonly LiteNetConfiguration _configuration;

        public AckPacketHandler(
            ConnectedMessageDispatcher messageDispatcher,
            LiteNetConfiguration configuration)
        {
            _messageDispatcher = messageDispatcher;
            _configuration = configuration;
        }

        public override Task Handle(EndPoint endPoint, AckHeader packet, ref SpanBufferReader reader)
        {
            if (_messageDispatcher == null)
                return Task.CompletedTask;
            foreach (int acknowledgement in packet.Acknowledgements)
            {
                // should just be `(packet.Sequence + acknowledgement) % _conguration.MaxPacketSize` but litenetlib does things weirdly
                var sequenceId = (packet.Sequence + ((acknowledgement - (acknowledgement % _configuration.WindowSize) + _configuration.WindowSize) % _configuration.WindowSize)) % _configuration.MaxPacketSize;
                _messageDispatcher.Acknowledge(endPoint, packet.ChannelId, sequenceId);
            }
            return Task.CompletedTask;
        }
    }
}
