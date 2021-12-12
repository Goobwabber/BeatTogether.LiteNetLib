using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Delegates;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using Krypton.Buffers;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetReliableDispatcher
    {
        public event PacketDispatchHandler DispatchEvent;

        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<byte, WindowQueue>> _channelWindows = new();
        private readonly LiteNetConfiguration _configuration;

        public LiteNetReliableDispatcher(
            LiteNetConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Cleanup(EndPoint endPoint)
            => _channelWindows.TryRemove(endPoint, out _);

        public void Acknowledge(EndPoint endPoint, byte channelId, int sequenceId)
        {
            if (_channelWindows.TryGetValue(endPoint, out var channels))
                if (channels.TryGetValue(channelId, out var window))
                    window.Dequeue(sequenceId);
        }

        public async void Send(EndPoint endPoint, ReadOnlyMemory<byte> message, byte channelId = (int)DeliveryMethod.ReliableOrdered)
        {
            var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                .GetOrAdd(channelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            await window.Enqueue(out int queueIndex);
            await SendAndRetry(endPoint, message, channelId, queueIndex);
        }

        protected async Task SendAndRetry(EndPoint endPoint, ReadOnlyMemory<byte> message, byte channelId, int queueIndex)
        {
            var retryCount = 0;
            while (_configuration.MaximumReliableRetries >= 0 ? retryCount++ < _configuration.MaximumReliableRetries : true) 
            {
                if (!_channelWindows.TryGetValue(endPoint, out var channels) || !channels.TryGetValue(channelId, out var window))
                    return; // Channel destroyed, stop sending

                var acknowledgementTask = window.WaitForDequeue(queueIndex);
                Send(endPoint, message, channelId, queueIndex);
                await Task.WhenAny(
                    acknowledgementTask,
                    Task.Delay(_configuration.ReliableRetryDelay)
                );
                if (acknowledgementTask.IsCompleted)
                    break;
                // Failed, try again
            }
        }

        protected void Send(EndPoint endPoint, ReadOnlyMemory<byte> message, byte channelId, int sequence)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            new ChanneledHeader
            {
                ChannelId = channelId,
                Sequence = (ushort)sequence
            }.WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message.Span);
            DispatchEvent?.Invoke(endPoint, bufferWriter.Data);
        }
    }
}
