using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using Krypton.Buffers;
using System;
using System.Collections.Concurrent;
using System.Net;

namespace BeatTogether.LiteNetLib.Sources
{
    public abstract class ConnectedMessageSource : IDisposable
    {
        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<ushort, FragmentBuilder>> _fragmentBuilders = new();
        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<byte, AckWindow>> _channelWindows = new();
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

        public virtual void Signal(EndPoint remoteEndPoint, UnreliableHeader header, ref SpanBufferReader reader)
            => OnReceive(remoteEndPoint, ref reader, DeliveryMethod.Unreliable);

        public virtual void Signal(EndPoint remoteEndPoint, ChanneledHeader header, ref SpanBufferReader reader)
        {
            var window = _channelWindows.GetOrAdd(remoteEndPoint, _ => new())
                .GetOrAdd(header.ChannelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            var alreadyReceived = !window.Add(header.Sequence);
            var windowArray = window.GetWindow(out int windowPosition);

            _server.SendAsync(remoteEndPoint, new AckHeader
            {
                Sequence = (ushort)windowPosition,
                ChannelId = header.ChannelId,
                Acknowledgements = windowArray
            });

            if (header.IsFragmented)
            {
                var builder = _fragmentBuilders.GetOrAdd(remoteEndPoint, _ => new())
                    .GetOrAdd(header.FragmentId, _ => new(header.FragmentsTotal));

                if (builder.AddFragment(header.FragmentPart, reader.RemainingData))
                {
                    var writer = new SpanBufferWriter(412);
                    builder.WriteTo(ref writer);
                    var fragmentReader = new SpanBufferReader(writer.Data);

                    OnReceive(remoteEndPoint, ref fragmentReader, (DeliveryMethod)header.ChannelId);
                }
            }
            else
            {
                if (alreadyReceived)
                    return;
                OnReceive(remoteEndPoint, ref reader, (DeliveryMethod)header.ChannelId);
            }
        }

        public abstract void OnReceive(EndPoint remoteEndPoint, ref SpanBufferReader reader, DeliveryMethod method);
    }
}
