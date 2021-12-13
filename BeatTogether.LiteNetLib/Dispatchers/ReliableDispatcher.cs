using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Delegates;
using BeatTogether.LiteNetLib.Dispatchers.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using Krypton.Buffers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib
{
    public class ReliableDispatcher : IMessageDispatcher
    {
        public const byte ChannelId = (byte)DeliveryMethod.ReliableOrdered;

        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<byte, QueueWindow>> _channelWindows = new();
        private readonly LiteNetConfiguration _configuration;
        private readonly LiteNetServer _server;

        public ReliableDispatcher(
            LiteNetConfiguration configuration,
            LiteNetServer server)
        {
            _configuration = configuration;
            _server = server;

            _server.ClientDisconnectEvent += (endPoint, _) => Cleanup(endPoint);
        }

        public void Send(EndPoint endPoint, ref ReadOnlySpan<byte> message)
            => Send(endPoint, new ReadOnlyMemory<byte>(message.ToArray()));

        public async Task Send(EndPoint endPoint, ReadOnlyMemory<byte> message)
        {
            var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                .GetOrAdd(ChannelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            await window.Enqueue(out int queueIndex);
            await SendAndRetry(endPoint, message, ChannelId, queueIndex);
        }

        private async Task SendAndRetry(EndPoint endPoint, ReadOnlyMemory<byte> message, byte channelId, int queueIndex)
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

        private void Send(EndPoint endPoint, ReadOnlyMemory<byte> message, byte channelId, int sequence)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            new ChanneledHeader
            {
                ChannelId = channelId,
                Sequence = (ushort)sequence
            }.WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message.Span);
            _server.SendAsync(endPoint, message.Span);
        }

        /// <summary>
        /// Acknowledges a message so we know to stop sending it
        /// </summary>
        /// <param name="endPoint">Originating endpoint</param>
        /// <param name="channelId">Channel of the message</param>
        /// <param name="sequenceId">Sequence of the message</param>
        /// <returns>Whether the message was successfully acknowledged</returns>
        public bool Acknowledge(EndPoint endPoint, byte channelId, int sequenceId)
            => _channelWindows.TryGetValue(endPoint, out var channels)
            && channels.TryGetValue(channelId, out var window)
            && window.Dequeue(sequenceId);

        public void Cleanup(EndPoint endPoint)
            => _channelWindows.TryRemove(endPoint, out _);
    }
}
