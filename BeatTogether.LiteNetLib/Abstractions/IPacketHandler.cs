using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public interface IPacketHandler
    {
        public Task Handle(EndPoint endPoint, INetSerializable packet, ref SpanBufferReader reader);
    }

    public interface IPacketHandler<TNetSerializable> : IPacketHandler where TNetSerializable : class, INetSerializable
    {
        public Task Handle(EndPoint endPoint, TNetSerializable packet, ref SpanBufferReader reader);
    }
}
