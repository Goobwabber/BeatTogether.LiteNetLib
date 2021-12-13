using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using Krypton.Buffers;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class ChanneledPacketHandler : BasePacketHandler<ChanneledHeader>
    {
        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<byte, ArrayWindow>> _channelWindows = new();
        private readonly LiteNetConfiguration _configuration;
        private readonly LiteNetServer _server;
        private readonly LiteNetAcknowledger _acknowledger;
        private readonly ILiteNetListener _listener;

        public ChanneledPacketHandler(
            LiteNetConfiguration configuration,
            LiteNetServer server,
            LiteNetAcknowledger acknowledger,
            ILiteNetListener listener)
        {
            _configuration = configuration;
            _server = server;
            _acknowledger = acknowledger;
            _listener = listener;
        }

        public override Task Handle(EndPoint endPoint, ChanneledHeader packet, ref SpanBufferReader reader)
        {
            if (packet.Sequence > _configuration.MaxSequence)
                return Task.CompletedTask; // 'Bad sequence'
            if (!_acknowledger.Acknowledge(endPoint, packet.ChannelId, packet.Sequence))
                return Task.CompletedTask; // Already handled this packet
            _listener.OnNetworkReceive(endPoint, ref reader, (Enums.DeliveryMethod)packet.ChannelId);
            return Task.CompletedTask;
        }
    }
}
