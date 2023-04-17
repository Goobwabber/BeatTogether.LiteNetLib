using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public abstract class BasePacketHandler<TNetSerializable> : IPacketHandler<TNetSerializable> where TNetSerializable : class, INetSerializable
    {
        public abstract Task Handle(EndPoint endPoint, TNetSerializable packet, ref SpanBufferReader reader);

        public Task Handle(EndPoint endPoint, INetSerializable packet, ref SpanBufferReader reader)
            => Handle(endPoint, (TNetSerializable)packet, ref reader);
    }
}
