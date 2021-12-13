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
        private readonly LiteNetConnectionPinger _pinger;
        private readonly LiteNetReliableDispatcher _dispatcher;
        private readonly LiteNetAcknowledger _acknowledger;
        private readonly ILiteNetListener _listener;

        public DisconnectPacketHandler(
            LiteNetServer server,
            ILiteNetListener listener)
        {
            _server = server;
            _listener = listener;
        }

        public override Task Handle(EndPoint endPoint, DisconnectHeader packet, ref SpanBufferReader reader)
        {
            _server.SendAsync(endPoint, new ShutdownOkHeader());
            _server.Cleanup(endPoint);
            _listener.OnPeerDisconnected(endPoint, Enums.DisconnectReason.RemoteConnectionClose, ref reader);
            return Task.CompletedTask;
        }
    }
}
