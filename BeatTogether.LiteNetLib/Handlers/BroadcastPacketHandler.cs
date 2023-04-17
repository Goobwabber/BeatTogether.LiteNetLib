using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Sources;
using BeatTogether.LiteNetLib.Util;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class BroadcastPacketHandler : BasePacketHandler<BroadcastHeader>
    {
        private readonly UnconnectedMessageSource _messageSource;

        public BroadcastPacketHandler(
            UnconnectedMessageSource messageSource)
        {
            _messageSource = messageSource;
        }

        public override Task Handle(EndPoint endPoint, BroadcastHeader packet, ref MemoryBuffer reader)
        {
            if (_messageSource != null)
                _messageSource.Signal(endPoint, ref reader, UnconnectedMessageType.Broadcast);
            return Task.CompletedTask;
        }
    }
}
