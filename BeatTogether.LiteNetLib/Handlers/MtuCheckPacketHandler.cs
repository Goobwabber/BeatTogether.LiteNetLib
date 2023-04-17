using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Util;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class MtuCheckPacketHandler : BasePacketHandler<MtuCheckHeader>
    {
        private readonly LiteNetServer _server;

        public MtuCheckPacketHandler(
            LiteNetServer server)
        {
            _server = server;
        }

        public override Task Handle(EndPoint endPoint, MtuCheckHeader packet, ref MemoryBuffer reader)
        {
            // Normally would check mtu - dont care lol, send back 'ok'
            _server.Send(endPoint, new MtuOkHeader
            {
                Mtu = packet.Mtu,
                PadSize = packet.PadSize,
                CheckEnd = packet.CheckEnd
            });
            return Task.CompletedTask;
        }
    }
}
