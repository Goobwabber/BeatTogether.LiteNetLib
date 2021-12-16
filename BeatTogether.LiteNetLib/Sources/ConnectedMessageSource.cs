using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using Krypton.Buffers;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Sources
{
    public abstract class ConnectedMessageSource : IDisposable
    {
        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<byte, ArrayWindow>> _channelWindows = new();
        private readonly LiteNetConfiguration _configuration;
        private readonly LiteNetServer _server;

        public ConnectedMessageSource(
            LiteNetConfiguration configuration,
            LiteNetServer server)
        {
            _configuration = configuration;
            _server = server;

            _server.ClientDisconnectEvent += HandleDisconnect;
        }

        public void Dispose()
            => _server.ClientDisconnectEvent -= HandleDisconnect;

        public void HandleDisconnect(EndPoint endPoint, DisconnectReason reason)
            => _channelWindows.TryRemove(endPoint, out _);

        public void Signal(EndPoint remoteEndPoint, UnreliableHeader header, ref SpanBufferReader reader)
            => OnReceive(remoteEndPoint, ref reader, DeliveryMethod.Unreliable);

        public void Signal(EndPoint remoteEndPoint, ChanneledHeader header, ref SpanBufferReader reader)
        {
            var window = _channelWindows.GetOrAdd(remoteEndPoint, _ => new())
                .GetOrAdd(header.ChannelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            var alreadyReceived = window.Add(header.Sequence);
            _server.SendAsync(remoteEndPoint, new AckHeader
            {
                Sequence = (ushort)window.GetWindowPosition(),
                ChannelId = header.ChannelId,
                Acknowledgements = window.GetWindow()
            });
            if (alreadyReceived)
                return;

            OnReceive(remoteEndPoint, ref reader, (DeliveryMethod)header.ChannelId);
        }

        public abstract void OnReceive(EndPoint remoteEndPoint, ref SpanBufferReader reader, DeliveryMethod method);
    }
}
