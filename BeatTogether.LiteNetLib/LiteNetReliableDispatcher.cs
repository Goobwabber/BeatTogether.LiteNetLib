using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using Krypton.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetReliableDispatcher
    {
        /// <summary>
        /// Number of channels there are for each delivery method
        /// </summary>
        public const int ChannelsPerDeliveryMethod = 4;

        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<byte, WindowQueue>> _channelWindows = new();
        private readonly LiteNetConfiguration _configuration;
        private readonly LiteNetServer _server;

        public LiteNetReliableDispatcher(
            LiteNetConfiguration configuration,
            LiteNetServer server)
        {
            _configuration = configuration;
            _server = server;
        }

        public void Cleanup(EndPoint endPoint)
            => _channelWindows.TryRemove(endPoint, out _);

        public void Acknowledge(EndPoint endPoint, byte channelId, int sequenceId)
        {
            if (_channelWindows.TryGetValue(endPoint, out var channels))
                if (channels.TryGetValue(channelId, out var window))
                    window.Dequeue(sequenceId);
        }

        public Task Send(EndPoint endPoint, INetSerializable message, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        {
            if (deliveryMethod == DeliveryMethod.Unreliable || deliveryMethod == DeliveryMethod.Sequenced)
                throw new System.Exception();
            return Send(endPoint, message, (byte)((int)deliveryMethod * ChannelsPerDeliveryMethod));
        }

        public async Task Send(EndPoint endPoint, INetSerializable message, byte channelId)
        {
            var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                .GetOrAdd(channelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            await window.Enqueue(out int queueIndex);
            await SendAndRetry(endPoint, window, message, channelId, queueIndex);
        }

        private async Task SendAndRetry(EndPoint endPoint, WindowQueue window, INetSerializable message, byte channelId, int queueIndex)
        {
            var retryCount = 0;
            while (_configuration.MaximumReliableRetries >= 0 ? retryCount++ < _configuration.MaximumReliableRetries : true) 
            {
                var acknowledgementTask = window.WaitForDequeue(queueIndex);
                SendInternal(endPoint, message, channelId, queueIndex);
                await Task.WhenAny(
                    acknowledgementTask,
                    Task.Delay(_configuration.ReliableRetryDelay)
                );
                if (acknowledgementTask.IsCompleted)
                    break;
                // Failed, try again
            }
        }

        private void SendInternal(EndPoint endPoint, INetSerializable message, byte channelId, int sequence)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            new ChanneledHeader
            {
                ChannelId = channelId,
                Sequence = (ushort)sequence
            }.WriteTo(ref bufferWriter);
            message.WriteTo(ref bufferWriter);
            _server.SendAsync(endPoint, bufferWriter);
        }
    }
}
