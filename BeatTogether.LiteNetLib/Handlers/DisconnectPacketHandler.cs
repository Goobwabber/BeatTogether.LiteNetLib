using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Util;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class DisconnectPacketHandler : BasePacketHandler<DisconnectHeader>
    {
        private readonly LiteNetServer _server;

        public DisconnectPacketHandler(
            LiteNetServer server)
        {
            _server = server;
        }

        public override Task Handle(EndPoint endPoint, DisconnectHeader packet, ref MemoryBuffer reader)
        {
            _server.Send(endPoint, new ShutdownOkHeader());
            _server.HandleDisconnect(endPoint, DisconnectReason.RemoteConnectionClose);
            return Task.CompletedTask;
        }
    }
}
