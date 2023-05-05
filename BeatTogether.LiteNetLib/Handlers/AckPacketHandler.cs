using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Dispatchers;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Util;
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

        public override Task Handle(EndPoint endPoint, AckHeader packet, ref SpanBuffer reader)
        {
            if (_messageDispatcher == null)
                return Task.CompletedTask;

            // 0 to WindowSize - 1, values missing weren't acknowledged
            foreach (var ack in packet.Acknowledgements)
            {
                // should just be '(packet.Sequence + acknowledgement) % _conguration.MaxPacketSize' but litenetlib does things weirdly
                // my thought process: when 'packet.Sequence = 134' the '0'th ack should be the ack for '134'
                // in actuality: when 'packet.Sequence = 134' the '134 % WindowSize' ack is the ack for '134'
                // this is really stupid. 
                var id = (packet.Sequence + (ack - packet.Sequence % _configuration.WindowSize + _configuration.WindowSize) % _configuration.WindowSize) % _configuration.MaxSequence;
                _messageDispatcher.Acknowledge(endPoint, packet.ChannelId, id);
            }

            return Task.CompletedTask;
        }
    }
}
