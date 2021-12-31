using NetCoreServer;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib
{
    public class ConcurrentUdpServer : UdpServer
    {
        private object _sendLock = new();
        private readonly ConcurrentQueue<TaskCompletionSource> _sendQueue = new();

        public ConcurrentUdpServer(
            IPEndPoint endPoint)
            : base(endPoint)
        {
        }

        protected override void OnSent(EndPoint endpoint, long sent)
        {
            base.OnSent(endpoint, sent);
            if (_sendQueue.TryPeek(out var tcs) && !tcs.Task.IsCompleted)
                throw new Exception();
            lock (_sendLock)
            {
                _sendQueue.TryDequeue(out _); // dequeue send, complete
            }
            if (_sendQueue.TryPeek(out var nextTcs)) // trigger next send
                nextTcs.SetResult();
        }

        public override bool SendAsync(EndPoint endpoint, ReadOnlySpan<byte> buffer)
        {
            _ = SendAsync(endpoint, new ReadOnlyMemory<byte>(buffer.ToArray()));
            return true;
        }

        public virtual async Task SendAsync(EndPoint endpoint, ReadOnlyMemory<byte> buffer)
        {
            var tcs = new TaskCompletionSource();
            lock(_sendLock)
            {
                _sendQueue.Enqueue(tcs);
                if (_sendQueue.Count == 1)
                    tcs.SetResult();
            }
            await tcs.Task;
            base.SendAsync(endpoint, buffer.Span);
        }
    }
}
