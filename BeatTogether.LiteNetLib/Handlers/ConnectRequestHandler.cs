using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Util;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class ConnectRequestHandler : BasePacketHandler<ConnectRequestHeader>
    {
        public const int ProtocolId = 11;

        private readonly LiteNetServer _server;
        private readonly ILogger _logger;

        public ConnectRequestHandler(
            LiteNetServer server,
            ILogger<ConnectRequestHandler> logger)
        {
            _server = server;
            _logger = logger;
        }

        public override Task Handle(EndPoint endPoint, ConnectRequestHeader packet, ref SpanBuffer reader)
        {
            _logger.LogDebug($"Received connection request from {endPoint}");
            if (packet.ProtocolId != ProtocolId)
            {
                _logger.LogWarning($"Invalid protocol from '{endPoint}'. (ProtocolId={packet.ProtocolId} Expected={ProtocolId})");
                _server.SendAsync(endPoint, new InvalidProtocolHeader());
                return Task.CompletedTask;
            }

            // TODO: There is some extra logic here in litenetlib that may be needed (NetManager.ProcessConnectRequest)

            bool alreadyExists = _server.HasConnected(endPoint);
            if (alreadyExists)
            {
                _logger.LogTrace($"Already connected endpoint connecting again{endPoint}");
                _server.SendAsync(endPoint, new ConnectAcceptHeader
                {
                    ConnectTime = packet.ConnectionTime,
                    RequestConnectionNumber = packet.ConnectionNumber,
                    IsReusedPeer = false // TODO: implement 'peer' reusing (probably not necessary)
                });
                return Task.CompletedTask;
            }
            MemoryBuffer MemoryReader = new(reader.RemainingData.ToArray());
            NewConnection(endPoint, packet, MemoryReader);
            return Task.CompletedTask;
        }

        private async void NewConnection(EndPoint endPoint, ConnectRequestHeader packet, MemoryBuffer reader)
        {
            if(await _server.ShouldAcceptConnection(endPoint, reader))
            {
                _logger.LogTrace($"Accepting request from {endPoint}");
                _server.SendAsync(endPoint, new ConnectAcceptHeader
                {
                    ConnectTime = packet.ConnectionTime,
                    RequestConnectionNumber = packet.ConnectionNumber,
                    IsReusedPeer = false
                });
                _server.HandleConnect(endPoint, packet.ConnectionTime);
                return;
            }
            _logger.LogTrace($"Rejecting request from {endPoint}");
            _server.SendAsync(endPoint, new DisconnectHeader
            {
                ConnectionTime = packet.ConnectionTime
            });
        }
    }
}
