using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    class MergedPacketHandler : BasePacketHandler<MergedHeader>
    {
        private readonly LiteNetServer _server;

        public MergedPacketHandler(
            LiteNetServer server)
        {
            _server = server;
        }

        public override Task Handle(EndPoint endPoint, MergedHeader packet, ref SpanBufferReader reader)
        {
            while (reader.RemainingSize > 0)
            {
                ushort size = reader.ReadUInt16();
                var newPacket = reader.ReadBytes(size);
                _server.HandlePacket(endPoint, newPacket);
            }
            return Task.CompletedTask;
        }
    }
}
