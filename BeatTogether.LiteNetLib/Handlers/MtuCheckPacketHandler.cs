using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class MtuCheckPacketHandler : IPacketHandler<MtuCheckHeader>
    {
        private readonly LiteNetServer _server;

        public MtuCheckPacketHandler(
            LiteNetServer server)
        {
            _server = server;
        }

        public Task Handle(IPEndPoint endPoint, MtuCheckHeader packet, ref SpanBufferReader reader)
        {
            // Normally would check mtu - dont care lol, send back 'ok'
            _server.SendRaw(endPoint, new MtuOkHeader
            {
                Mtu = packet.Mtu,
                PadSize = packet.PadSize,
                CheckEnd = packet.CheckEnd
            });
            return Task.CompletedTask;
        }
    }
}
