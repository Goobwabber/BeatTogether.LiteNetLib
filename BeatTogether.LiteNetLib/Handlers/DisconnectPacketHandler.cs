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

        public DisconnectPacketHandler(
            LiteNetServer server)
        {
            _server = server;
        }

        public override Task Handle(EndPoint endPoint, DisconnectHeader packet, ref SpanBufferReader reader)
        {
            _server.SendRaw(endPoint, new ShutdownOkHeader());
            return Task.CompletedTask;
        }
    }
}
