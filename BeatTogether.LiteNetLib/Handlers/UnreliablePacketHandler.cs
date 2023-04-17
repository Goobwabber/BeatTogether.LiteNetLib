using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Sources;
using BeatTogether.LiteNetLib.Util;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class UnreliablePacketHandler : BasePacketHandler<UnreliableHeader>
    {
        private readonly ConnectedMessageSource _messageSource;

        public UnreliablePacketHandler(
            ConnectedMessageSource messageSource)
        {
            _messageSource = messageSource;
        }

        public override Task Handle(EndPoint endPoint, UnreliableHeader packet, ref MemoryBuffer reader)
        {
            if (_messageSource != null)
                _messageSource.Signal(endPoint, packet, ref reader);
            return Task.CompletedTask;
        }
    }
}
