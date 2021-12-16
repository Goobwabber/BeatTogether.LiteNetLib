using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Sources;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class UnconnectedPacketHandler : BasePacketHandler<UnconnectedHeader>
    {
        private readonly UnconnectedMessageSource _messageSource;

        public UnconnectedPacketHandler(
            UnconnectedMessageSource messageSource)
        {
            _messageSource = messageSource;
        }

        public override Task Handle(EndPoint endPoint, UnconnectedHeader packet, ref SpanBufferReader reader)
        {
            if (_messageSource != null)
                _messageSource.Signal(endPoint, ref reader, UnconnectedMessageType.BasicMessage);
            return Task.CompletedTask;
        }
    }
}
