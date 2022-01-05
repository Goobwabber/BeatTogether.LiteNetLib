using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Sources;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class ChanneledPacketHandler : BasePacketHandler<ChanneledHeader>
    {
        private readonly LiteNetConfiguration _configuration;
        private readonly ConnectedMessageSource _messageSource;

        public ChanneledPacketHandler(
            LiteNetConfiguration configuration,
            ConnectedMessageSource messageSource)
        {
            _configuration = configuration;
            _messageSource = messageSource;
        }

        public override Task Handle(EndPoint endPoint, ChanneledHeader packet, ref SpanBufferReader reader)
        {
            if (_messageSource == null)
                return Task.CompletedTask;
            if (packet.Sequence > _configuration.MaxSequence)
                return Task.CompletedTask; // 'Bad sequence'
            _messageSource.Signal(endPoint, packet, ref reader);
            return Task.CompletedTask;
        }
    }
}
