using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class DisconnectPacketHandler : BasePacketHandler<DisconnectHeader>
    {
        private readonly LiteNetServer _server;
        private readonly LiteNetReliableDispatcher _dispatcher;
        private readonly LiteNetAcknowledger _acknowledger;
        private readonly ILiteNetListener _listener;

        public DisconnectPacketHandler(
            LiteNetServer server,
            LiteNetReliableDispatcher dispatcher,
            LiteNetAcknowledger acknowledger,
            ILiteNetListener listener)
        {
            _server = server;
            _dispatcher = dispatcher;
            _acknowledger = acknowledger;
            _listener = listener;
        }

        public override Task Handle(EndPoint endPoint, DisconnectHeader packet, ref SpanBufferReader reader)
        {
            _server.SendAsync(endPoint, new ShutdownOkHeader());
            _dispatcher.Cleanup(endPoint);
            _acknowledger.Cleanup(endPoint);
            _listener.OnPeerDisconnected(endPoint, Enums.DisconnectReason.RemoteConnectionClose, ref reader);
            return Task.CompletedTask;
        }
    }
}
