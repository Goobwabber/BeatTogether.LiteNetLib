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
        private readonly ILiteNetListener _listener;

        public ChanneledPacketHandler(
            LiteNetConfiguration configuration,
            LiteNetServer server,
            ILiteNetListener listener)
        {
            _configuration = configuration;
            _server = server;
            _listener = listener;
        }

        public override Task Handle(EndPoint endPoint, ChanneledHeader packet, ref SpanBufferReader reader)
        {
            if (packet.Sequence > _configuration.MaxSequence)
                return Task.CompletedTask; // 'Bad sequence'
            var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                .GetOrAdd(packet.ChannelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            window.Add(packet.Sequence);
            _server.SendAsync(endPoint, new AckHeader
            {
                Sequence = (ushort)window.GetWindowPosition(),
                ChannelId = packet.ChannelId,
                Acknowledgements = window.GetWindow()
            });
            _listener.OnNetworkReceive(endPoint, ref reader, (Enums.DeliveryMethod)packet.ChannelId);
            return Task.CompletedTask;
        }
    }
}
