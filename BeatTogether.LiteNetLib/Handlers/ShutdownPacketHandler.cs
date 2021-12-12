using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class ShutdownPacketHandler : BasePacketHandler<ShutdownOkHeader>
    {
        private readonly ILiteNetListener _listener;

        public ShutdownPacketHandler(
            ILiteNetListener listener)
        {
            _listener = listener;
        }

        public override Task Handle(EndPoint endPoint, ShutdownOkHeader packet, ref SpanBufferReader reader)
        {
            // TODO: don't assume disconnect reason
            _listener.OnPeerDisconnected(endPoint, Enums.DisconnectReason.DisconnectPeerCalled, ref reader);
            return Task.CompletedTask;
        }
    }
}
