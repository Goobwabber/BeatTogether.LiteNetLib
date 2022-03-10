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
        private readonly SemaphoreSlim _sendSemaphore = new(1);

        public ConcurrentUdpServer(
            IPEndPoint endPoint)
            : base(endPoint)
        {
        }

        protected override void OnSent(EndPoint endpoint, long sent)
        {
            base.OnSent(endpoint, sent);
            _sendSemaphore.Release();
        }

        public override bool SendAsync(EndPoint endpoint, ReadOnlySpan<byte> buffer)
        {
            _ = SendAsync(endpoint, new ReadOnlyMemory<byte>(buffer.ToArray()), CancellationToken.None);
            return true;
        }

        public virtual async Task SendAsync(EndPoint endpoint, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            await _sendSemaphore.WaitAsync();
            if (cancellationToken.IsCancellationRequested)
            {
                _sendSemaphore.Release();
                return;
            }
            SendImmediate(endpoint, buffer.Span);
        }

        protected virtual void SendImmediate(EndPoint endpoint, ReadOnlySpan<byte> buffer)
            => base.SendAsync(endpoint, buffer);
    }
}
