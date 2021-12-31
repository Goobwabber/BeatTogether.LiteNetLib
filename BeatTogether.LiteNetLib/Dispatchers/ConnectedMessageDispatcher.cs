using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using Krypton.Buffers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Dispatchers
{
    public class ConnectedMessageDispatcher : IDisposable
    {
        public const int ChanneledHeaderSize = 4;
        public const int FragmentedHeaderSize = ChanneledHeaderSize + 6;

        private object _fragmentIdLock = new();
        private ushort _fragmentId = 0;

        private readonly ConcurrentDictionary<EndPoint, ConcurrentDictionary<byte, QueueWindow>> _channelWindows = new();
        private readonly LiteNetConfiguration _configuration;
        private readonly LiteNetServer _server;

        public ConnectedMessageDispatcher(
            LiteNetConfiguration configuration,
            LiteNetServer server)
        {
            _configuration = configuration;
            _server = server;

            _server.ClientDisconnectEvent += HandleDisconnect;
        }

        public Task Send(EndPoint endPoint, ReadOnlySpan<byte> message, DeliveryMethod method)
        {
            if (method == DeliveryMethod.Unreliable)
                return SendUnreliable(endPoint, message);
            var channelId = (byte)method;
            if (method == DeliveryMethod.Sequenced) {
                var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                    .GetOrAdd(channelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
                _ = window.Enqueue(out int queueIndex);
                return SendChanneled(endPoint, message, channelId, queueIndex);
            }
            if (message.Length + ChanneledHeaderSize <= _configuration.MaxPacketSize)
                return SendAndRetry(endPoint, new ReadOnlyMemory<byte>(message.ToArray()), channelId);
            int maxMessageSize = _configuration.MaxPacketSize - FragmentedHeaderSize;
            int fragmentCount = message.Length / maxMessageSize + ((message.Length % maxMessageSize == 0) ? 0 : 1);
            if (fragmentCount > ushort.MaxValue) // ushort is used to identify each fragment
                throw new Exception(); // TODO

            ushort fragmentId;
            lock (_fragmentIdLock)
            {
                fragmentId = _fragmentId;
                _fragmentId++;
            }

            List<Task> fragmentTasks = new();
            var bufferReader = new SpanBufferReader(message);
            for (ushort i = 0; i < fragmentCount; i++)
            {
                var sliceSize = bufferReader.RemainingSize > maxMessageSize ? maxMessageSize : bufferReader.RemainingSize;
                var memSlice = new ReadOnlyMemory<byte>(bufferReader.ReadBytes(sliceSize).ToArray());
                fragmentTasks.Add(SendAndRetry(endPoint, memSlice, channelId, true, fragmentId, i, (ushort)fragmentCount));
            }
            return Task.WhenAll(fragmentTasks);
        }

        private async Task SendAndRetry(EndPoint endPoint, ReadOnlyMemory<byte> message, byte channelId, bool fragmented = false, ushort fragmentId = 0, ushort fragmentPart = 0, ushort fragmentsTotal = 0)
        {
            var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                .GetOrAdd(channelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            await window.Enqueue(out int queueIndex);
            var retryCount = 0;
            while (_configuration.MaximumReliableRetries >= 0 ? retryCount++ < _configuration.MaximumReliableRetries : true) 
            {
                if (!_channelWindows.TryGetValue(endPoint, out var channels) || !channels.TryGetValue(channelId, out _))
                    return; // Channel destroyed, stop sending

                var acknowledgementTask = window.WaitForDequeue(queueIndex);
                await SendChanneled(endPoint, message, channelId, queueIndex, fragmented, fragmentId, fragmentPart, fragmentsTotal);
                await Task.WhenAny(
                    acknowledgementTask,
                    Task.Delay(_configuration.ReliableRetryDelay)
                );
                if (acknowledgementTask.IsCompleted)
                    break;
                // Failed, try again
            }
        }

        private Task SendChanneled(EndPoint endPoint, ReadOnlyMemory<byte> message, byte channelId, int sequence, bool fragmented = false, ushort fragmentId = 0, ushort fragmentPart = 0, ushort fragmentsTotal = 0)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            new ChanneledHeader
            {
                IsFragmented = fragmented,
                ChannelId = channelId,
                Sequence = (ushort)sequence,
                FragmentId = fragmentId,
                FragmentPart = fragmentPart,
                FragmentsTotal = fragmentsTotal
            }.WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message.Span);
            return _server.SendAsync(endPoint, new ReadOnlyMemory<byte>(bufferWriter.Data.ToArray()));
        }

        private Task SendChanneled(EndPoint endPoint, ReadOnlySpan<byte> message, byte channelId, int sequence)
        {
            if (message.Length > _configuration.MaxPacketSize)
                throw new Exception();
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            new ChanneledHeader
            {
                ChannelId = channelId,
                Sequence = (ushort)sequence
            }.WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message);
            _server.SendAsync(endPoint, bufferWriter.Data);
            return Task.CompletedTask;
        }

        private Task SendUnreliable(EndPoint endPoint, ReadOnlySpan<byte> message)
        {
            if (message.Length > _configuration.MaxPacketSize)
                throw new Exception();
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            new UnreliableHeader().WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message);
            _server.SendAsync(endPoint, bufferWriter);
            return Task.CompletedTask;
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

        public void HandleDisconnect(EndPoint endPoint, DisconnectReason reason)
            => _channelWindows.TryRemove(endPoint, out _);

        public void Dispose()
            => _server.ClientDisconnectEvent -= HandleDisconnect;
    }
}
