using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Models;
using BeatTogether.LiteNetLib.Util;
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
            return Send(endPoint, message.ToArray().AsMemory(), method);
        }

        public Task Send(EndPoint endPoint, Memory<byte> message, DeliveryMethod method)
        {
            if (method == DeliveryMethod.Unreliable)
                return SendUnreliable(endPoint, message.Span);
            var channelId = (byte)method;
            if (method == DeliveryMethod.Sequenced) {
                var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                    .GetOrAdd(channelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
                _ = window.Enqueue(out int queueIndex);
                return SendChanneled(endPoint, message.Span, channelId, queueIndex);
            }
            if (message.Length + ChanneledHeaderSize + 256 <= _configuration.MaxPacketSize)
                return SendAndRetry(endPoint, message, channelId);

            int maxMessageSize =  _configuration.MaxPacketSize - FragmentedHeaderSize - 256;
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
            var bufferReader = new MemoryBuffer(message);
            for (ushort i = 0; i < fragmentCount; i++)
            {
                var sliceSize = bufferReader.RemainingSize > maxMessageSize ? maxMessageSize : bufferReader.RemainingSize;
                var memSlice = bufferReader.ReadBytes(sliceSize);
                fragmentTasks[i] = SendAndRetry(endPoint, memSlice, channelId, true, fragmentId, i, (ushort)fragmentCount);
            }
            return Task.WhenAll(fragmentTasks);
        }

        private async Task SendAndRetry(EndPoint endPoint, Memory<byte> message, byte channelId, bool fragmented = false, ushort fragmentId = 0, ushort fragmentPart = 0, ushort fragmentsTotal = 0)
        {
            var window = _channelWindows.GetOrAdd(endPoint, _ => new())
                .GetOrAdd(channelId, _ => new(_configuration.WindowSize, _configuration.MaxSequence));
            await window.Enqueue(out int queueIndex);
            int MessageLength = fragmented ? message.Length + FragmentedHeaderSize : message.Length + ChanneledHeaderSize;
            MemoryBuffer FullMessage = new(GC.AllocateArray<byte>(MessageLength, pinned: true), false);

            WriteHeader(new ChanneledHeader
            {
                IsFragmented = fragmented,
                ChannelId = channelId,
                Sequence = (ushort)queueIndex,
                FragmentId = fragmentId,
                FragmentPart = fragmentPart,
                FragmentsTotal = fragmentsTotal
            },ref FullMessage, message);
            var ackTask = window.WaitForDequeue(queueIndex);
            var ackCts = new CancellationTokenSource();
            _ = ackTask.ContinueWith(_ => ackCts.Cancel()); // Cancel if acknowledged

            var retryCount = 0;
            while (_configuration.MaximumReliableRetries < 0 || retryCount++ < _configuration.MaximumReliableRetries) 
            {
                if (!_channelWindows.TryGetValue(endPoint, out var channels) || !channels.TryGetValue(channelId, out _))
                    return; // Channel destroyed, stop sending

                if (ackTask.IsCompleted)
                    return;
                await _server.SendAsync(endPoint, FullMessage.Data);
                await Task.WhenAny(
                    ackTask,
                    Task.Delay(_configuration.ReliableRetryDelay)
                );
            }
        }
        /// <summary>
        /// Expects the incomming MemoryBuffer to the exact length of the message to be sent
        /// </summary>
        /// <param name="header"></param>
        /// <param name="FullMessage"></param>
        /// <param name="Message"></param>
        private static void WriteHeader(INetSerializable header, ref MemoryBuffer FullMessage, Memory<byte> Message)
        {
            header.WriteTo(ref FullMessage);
            FullMessage.WriteBytes(Message.Span);
        }


        private Task SendChanneled(EndPoint endPoint, ReadOnlySpan<byte> message, byte channelId, int sequence)
        {
            if (message.Length > _configuration.MaxPacketSize)
                throw new Exception();
            var bufferWriter = new SpanBuffer(stackalloc byte[message.Length + ChanneledHeaderSize]);
            new ChanneledHeader
            {
                ChannelId = channelId,
                Sequence = (ushort)sequence
            }.WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message);
            _server.SendSerial(endPoint, bufferWriter.Data);
            return Task.CompletedTask;
        }

        UnreliableHeader unreliableHeader = new UnreliableHeader();
        private Task SendUnreliable(EndPoint endPoint, ReadOnlySpan<byte> message)
        {
            if (message.Length > _configuration.MaxPacketSize)
                throw new Exception();
            var bufferWriter = new SpanBuffer(stackalloc byte[message.Length + 1], false);
            unreliableHeader.WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message);
            _server.SendSerial(endPoint, bufferWriter.Data);
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
