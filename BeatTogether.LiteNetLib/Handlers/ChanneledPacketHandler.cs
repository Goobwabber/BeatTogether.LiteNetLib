using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class ChanneledPacketHandler : BasePacketHandler<ChanneledHeader>
    {
        private readonly LiteNetConfiguration _configuration;

        public ChanneledPacketHandler(
            LiteNetConfiguration configuration)
        {
            _configuration = configuration;
        }

        public override Task Handle(EndPoint endPoint, ChanneledHeader packet, ref SpanBufferReader reader)
        {
            if (packet.Sequence > _configuration.MaxSequence)
                return Task.CompletedTask; // 'Bad sequence'
            throw new NotImplementedException();
        }
    }
}
