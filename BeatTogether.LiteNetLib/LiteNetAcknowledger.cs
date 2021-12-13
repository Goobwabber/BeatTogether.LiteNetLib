using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using Krypton.Buffers;
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

            _server.ClientDisconnectEvent += (endPoint, _) => Cleanup(endPoint);
        }

        public void Cleanup(EndPoint endPoint)
            => _channelWindows.TryRemove(endPoint, out _);

        /// <summary>
        /// Sends an acknowledgement back for a received reliable message
        /// </summary>
        /// <param name="endPoint">Originating endpoint</param>
        /// <param name="channelId">Message channel</param>
        /// <param name="sequenceId">Message sequence</param>
        /// <returns>True if acknowledgement was not already sent</returns>
        public bool Acknowledge(EndPoint endPoint, byte channelId, int sequenceId)
        {
            var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                .GetOrAdd(channelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            var alreadyAcked = window.Add(sequenceId);
            _server.SendAsync(endPoint, new AckHeader
            {
                Sequence = (ushort)window.GetWindowPosition(),
                ChannelId = channelId,
                Acknowledgements = window.GetWindow()
            });
            return alreadyAcked;
        }
    }
}
