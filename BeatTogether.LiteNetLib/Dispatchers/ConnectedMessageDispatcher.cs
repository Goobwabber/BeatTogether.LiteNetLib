using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using BeatTogether.LiteNetLib.Util;
using Krypton.Buffers;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Dispatchers
{
    public class ConnectedMessageDispatcher : IDisposable
    {
        public const int ChanneledHeaderSize = 4;
        public const int FragmentedHeaderSize = ChanneledHeaderSize + 6;

        private object _fragmentLock = new();

        private readonly ConcurrentDictionary<EndPoint, ushort> _fragmentIds = new();
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
        public Task Send(EndPoint endPoint, Span<byte> message, DeliveryMethod method)
        {
            if (method == DeliveryMethod.Unreliable)
                return SendUnreliable(endPoint, message);
            var channelId = (byte)method;
            if (method == DeliveryMethod.Sequenced)
            {
                var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                    .GetOrAdd(channelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
                _ = window.Enqueue(out int queueIndex);
                return SendChanneled(endPoint, message, channelId, queueIndex);
            }
            if (message.Length + ChanneledHeaderSize + 256 <= _configuration.MaxPacketSize)
            {
                var memoryWriter = new MemoryBuffer(new byte[message.Length + 10], false);
                memoryWriter.SetOffset(ChanneledHeaderSize);
                memoryWriter.WriteBytes(message);
                return SendAndRetry(endPoint, memoryWriter, channelId);
            }

            int maxMessageSize = _configuration.MaxPacketSize - FragmentedHeaderSize - 256;
            int fragmentCount = message.Length / maxMessageSize + ((message.Length % maxMessageSize == 0) ? 0 : 1);
            if (fragmentCount > ushort.MaxValue) // ushort is used to identify each fragment
                throw new Exception(); // TODO

            ushort fragmentId;
            lock (_fragmentLock)
            {
                fragmentId = _fragmentIds.GetOrAdd(endPoint, 0);
                _fragmentIds[endPoint]++;
            }
            Task[] fragmentTasks = new Task[fragmentCount];
            int fragmentOffset = 0;
            int Remaining = 0;
            for (ushort i = 0; i < fragmentCount; i++)
            {
                var memoryWriter = new MemoryBuffer(new byte[message.Length + ChanneledHeaderSize], false);
                memoryWriter.SetOffset(10);
                Remaining = message.Length - fragmentOffset;
                var sliceSize = Remaining > maxMessageSize ? maxMessageSize : Remaining;
                memoryWriter.WriteBytes(message.Slice(fragmentOffset, sliceSize));
                fragmentOffset += sliceSize;
                fragmentTasks[i] = SendAndRetry(endPoint, memoryWriter, channelId, true, fragmentId, i, (ushort)fragmentCount);
            }
            return Task.WhenAll(fragmentTasks);
        }

        private async Task SendAndRetry(EndPoint endPoint, MemoryBuffer message, byte channelId, bool fragmented = false, ushort fragmentId = 0, ushort fragmentPart = 0, ushort fragmentsTotal = 0)
        {
            var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                .GetOrAdd(channelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            await window.Enqueue(out int queueIndex);

            int OldOffset = message.Offset;
            message.SetOffset(0);
            new ChanneledHeader
            {
                IsFragmented = fragmented,
                ChannelId = channelId,
                Sequence = (ushort)queueIndex,
                FragmentId = fragmentId,
                FragmentPart = fragmentPart,
                FragmentsTotal = fragmentsTotal
            }.WriteTo(ref message);
            message.SetOffset(OldOffset);
            var ackTask = window.WaitForDequeue(queueIndex);
            var retryCount = 0;
            long TimeOfSend = DateTime.UtcNow.Ticks;
            while (DateTime.UtcNow.Ticks - TimeOfSend < _configuration.TimeoutSeconds * TimeSpan.TicksPerSecond)
            {
                retryCount++;
                if (!_channelWindows.TryGetValue(endPoint, out var channels) || !channels.TryGetValue(channelId, out _))
                    break; // Channel destroyed, stop sending
                if (ackTask.IsCompleted)
                    break;
                await _server.SendAsync(endPoint, message.Data);
                await Task.WhenAny(
                    ackTask,
                    Task.Delay(retryCount < _configuration.ReliableRetries ? _configuration.ReliableRetryDelay : _configuration.ReliableRetryDelayAfterRetrys)
                );
            }
        }

        private Task SendChanneled(EndPoint endPoint, ReadOnlySpan<byte> message, byte channelId, int sequence)
        {
            if (message.Length > _configuration.MaxPacketSize - ChanneledHeaderSize)
                throw new Exception();
            var bufferWriter = new SpanBuffer(stackalloc byte[message.Length + ChanneledHeaderSize], false); //Should be memoryBuffer
            new ChanneledHeader
            {
                ChannelId = channelId,
                Sequence = (ushort)sequence
            }.WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message);
            return InternalSendChanneled(endPoint, bufferWriter.Data.ToArray());
        }
        private async Task InternalSendChanneled(EndPoint endPoint, Memory<byte> buffer)
        {
            await _server.SendAsync(endPoint, buffer);
        }

        private readonly UnreliableHeader _unreliableHeader = new UnreliableHeader();
        private Task SendUnreliable(EndPoint endPoint, ReadOnlySpan<byte> message)
        {
            if (message.Length > _configuration.MaxPacketSize - 1)
                throw new Exception();
            var bufferWriter = new SpanBuffer(stackalloc byte[message.Length + 1], false); //Should be memoryBuffer
            _unreliableHeader.WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message);
            return InternalSendUnreliable(endPoint, bufferWriter.Data.ToArray());
        }
        private async Task InternalSendUnreliable(EndPoint endPoint, Memory<byte> buffer)
        {
            await _server.SendAsync(endPoint, buffer);
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
