using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using System.Collections.Concurrent;
using System.Net;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetAcknowledger
    {
        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<byte, ArrayWindow>> _channelWindows = new();
        private readonly LiteNetConfiguration _configuration;
        private readonly LiteNetServer _server;

        public LiteNetAcknowledger(
            LiteNetConfiguration configuration,
            LiteNetServer server)
        {
            _configuration = configuration;
            _server = server;

            _server.CleanupEvent += Cleanup;
        }

        public void Cleanup(EndPoint endPoint)
            => _channelWindows.TryRemove(endPoint, out _);

        public void HandleMessage(EndPoint endPoint, byte channelId, int sequenceId)
        {
            var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                .GetOrAdd(channelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            window.Add(sequenceId);
            _server.SendAsync(endPoint, new AckHeader
            {
                Sequence = (ushort)window.GetWindowPosition(),
                ChannelId = channelId,
                Acknowledgements = window.GetWindow()
            });
        }
    }
}
