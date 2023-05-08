using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using BeatTogether.LiteNetLib.Util;
using System;
using System.Collections.Generic;
using System.Net;

namespace BeatTogether.LiteNetLib.Sources
{
    public abstract class ConnectedMessageSource : IDisposable
    {
        private readonly Dictionary<EndPoint, (object, Dictionary<ushort, FragmentBuilder>)> _fragmentBuilders = new();
        private readonly object _fragmentLock = new();
        private readonly Dictionary<EndPoint, (object, Dictionary<byte, AckWindow>)> _channelWindows = new();
        private readonly object _channelWindowLock = new();
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
        {
            lock(_channelWindowLock){
                _channelWindows.Remove(endPoint, out _);
            }
            lock(_fragmentLock){
                _fragmentBuilders.Remove(endPoint, out _);
            }
        }

        public virtual void Signal(EndPoint remoteEndPoint, UnreliableHeader header, ref SpanBuffer reader)
            => OnReceive(remoteEndPoint, ref reader, DeliveryMethod.Unreliable);


        private void GetWindow(EndPoint endPoint, byte channelId, out AckWindow window)
        {
            (object, Dictionary<byte, AckWindow>) windows;
            lock (_channelWindowLock)
            {
                if (!_channelWindows.TryGetValue(endPoint, out windows))
                    _channelWindows.Add(endPoint, windows = (new(), new()));
            }
            lock (windows.Item1)
            {
                if (!windows.Item2.TryGetValue(channelId, out window!))
                    windows.Item2.Add(channelId, window = new(_configuration.WindowSize, _configuration.MaxSequence));
            }
        }
        private void GetFragmentBuilder(EndPoint endPoint, ushort FragmentId, ushort TotalFragments, out FragmentBuilder Builder)
        {
            (object, Dictionary<ushort, FragmentBuilder>) Builders;
            lock (_fragmentLock)
            {
                if (!_fragmentBuilders.TryGetValue(endPoint, out Builders))
                    _fragmentBuilders.Add(endPoint, Builders = (new(), new()));
            }
            lock (Builders.Item1)
            {
                if (!Builders.Item2.TryGetValue(FragmentId, out Builder!))
                    Builders.Item2.Add(FragmentId, Builder = new(TotalFragments));
            }
        }
        private void DiscardFragmentBuilder(EndPoint endPoint, ushort FragmentId)
        {
            (object, Dictionary<ushort, FragmentBuilder>) Builders;
            lock (_fragmentLock)
            {
                if (!_fragmentBuilders.TryGetValue(endPoint, out Builders))
                    _fragmentBuilders.Add(endPoint, Builders = (new(), new()));
            }
            lock (Builders.Item1)
            {
                Builders.Item2.Remove(FragmentId, out _);
            }
        }

        public virtual void Signal(EndPoint remoteEndPoint, ChanneledHeader header, ref SpanBuffer reader)
        {
            GetWindow(remoteEndPoint, header.ChannelId, out var window);
            var alreadyReceived = !window.Add(header.Sequence);
            var windowArray = window.GetWindow(out int windowPosition);

            _server.SendAsync(remoteEndPoint, new AckHeader
            {
                Sequence = (ushort)windowPosition,
                ChannelId = header.ChannelId,
                Acknowledgements = windowArray,
                WindowSize = _configuration.WindowSize
            });

            if (header.IsFragmented)
            {
                GetFragmentBuilder(remoteEndPoint, header.FragmentId, header.FragmentsTotal, out var builder);

                if (builder.AddFragment(header.FragmentPart, reader.RemainingData))
                {
                    var fragmentReader = new SpanBuffer(stackalloc byte[header.FragmentsTotal * 1024]); //Almost max size of packet * total fragments
                    builder.WriteTo(ref fragmentReader);
                    SpanBuffer CombinedFragments = new(fragmentReader.Data);
                    DiscardFragmentBuilder(remoteEndPoint, header.FragmentId);
                    OnReceive(remoteEndPoint, ref CombinedFragments, (DeliveryMethod)header.ChannelId);
                }
            }
            else
            {
                if (alreadyReceived)
                    return;
                OnReceive(remoteEndPoint, ref reader, (DeliveryMethod)header.ChannelId);
            }
        }

        public abstract void OnReceive(EndPoint remoteEndPoint, ref SpanBuffer reader, DeliveryMethod method);
    }
}
