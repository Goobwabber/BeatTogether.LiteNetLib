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
        private readonly LiteNetReliableDispatcher _dispatcher;
        private readonly LiteNetAcknowledger _acknowledger;
        private readonly ILiteNetListener _listener;

        public ShutdownPacketHandler(
            LiteNetReliableDispatcher dispatcher,
            LiteNetAcknowledger acknowledger,
            ILiteNetListener listener)
        {
            _dispatcher = dispatcher;
            _acknowledger = acknowledger;
            _listener = listener;
        }

        public override Task Handle(EndPoint endPoint, ShutdownOkHeader packet, ref SpanBufferReader reader)
        {
            _dispatcher.Cleanup(endPoint);
            _acknowledger.Cleanup(endPoint);
            // TODO: don't assume disconnect reason
            _listener.OnPeerDisconnected(endPoint, Enums.DisconnectReason.DisconnectPeerCalled, ref reader);
            return Task.CompletedTask;
        }
    }
}
