using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class ConnectRequestHandler : IPacketHandler<ConnectRequestHeader>
    {
        public const int ProtocolId = 11;

        private readonly LiteNetServer _server;
        private readonly ILiteNetListener _listener;

        public ConnectRequestHandler(
            LiteNetServer server,
            ILiteNetListener listener)
        {
            _server = server;
            _listener = listener;
        }

        public Task Handle(IPEndPoint endPoint, ConnectRequestHeader packet, ref SpanBufferReader reader)
        {
            if (packet.ProtocolId != ProtocolId)
            {
                _server.SendRaw(endPoint, new InvalidProtocolHeader());
                return Task.CompletedTask;
            }

            // TODO: There is some extra logic here in litenetlib that may be needed (NetManager.ProcessConnectRequest)

            if (_listener.ShouldAcceptConnectionRequest(endPoint, ref reader))
            {
                _server.SendRaw(endPoint, new ConnectAcceptHeader
                {
                    ConnectTime = packet.ConnectionTime,
                    RequestConnectionNumber = packet.ConnectionNumber,
                    IsReusedPeer = false // TODO: implement 'peer' reusing (probably not necessary)
                });
                _listener.OnPeerConnected(endPoint);
                return Task.CompletedTask;
            }
            _server.SendRaw(endPoint, new DisconnectHeader
            {
                ConnectTime = packet.ConnectionTime
            });
            return Task.CompletedTask;
        }
    }
}
