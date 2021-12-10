using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class ConnectRequestHandler : BasePacketHandler<ConnectRequestHeader>
    {
        public const int ProtocolId = 11;

        private readonly LiteNetServer _server;
        private readonly ILiteNetListener _listener;
        private readonly ILogger _logger;

        public ConnectRequestHandler(
            LiteNetServer server,
            ILiteNetListener listener,
            ILogger<ConnectRequestHandler> logger)
        {
            _server = server;
            _listener = listener;
            _logger = logger;
        }

        public override Task Handle(EndPoint endPoint, ConnectRequestHeader packet, ref SpanBufferReader reader)
        {
            _logger.LogDebug($"Received connection request from {endPoint}");
            if (packet.ProtocolId != ProtocolId)
            {
                _logger.LogWarning($"Invalid protocol from '{endPoint}'. (ProtocolId={packet.ProtocolId} Expected={ProtocolId})");
                _server.SendAsync(endPoint, new InvalidProtocolHeader());
                return Task.CompletedTask;
            }

            // TODO: There is some extra logic here in litenetlib that may be needed (NetManager.ProcessConnectRequest)

            if (_listener.ShouldAcceptConnectionRequest(endPoint, ref reader))
            {
                _logger.LogTrace($"Accepting request from {endPoint}");
                _server.SendAsync(endPoint, new ConnectAcceptHeader
                {
                    ConnectTime = packet.ConnectionTime,
                    RequestConnectionNumber = packet.ConnectionNumber,
                    IsReusedPeer = false // TODO: implement 'peer' reusing (probably not necessary)
                });
                _listener.OnPeerConnected(endPoint);
                return Task.CompletedTask;
            }
            _logger.LogTrace($"Rejecting request from {endPoint}");
            _server.SendAsync(endPoint, new DisconnectHeader
            {
                ConnectTime = packet.ConnectionTime
            });
            return Task.CompletedTask;
        }
    }
}
