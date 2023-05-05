using BeatTogether.LiteNetLib.Util;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public interface IPacketHandler
    {
        public Task Handle(EndPoint endPoint, INetSerializable packet, ref SpanBuffer reader);
    }

    public interface IPacketHandler<TNetSerializable> : IPacketHandler where TNetSerializable : class, INetSerializable
    {
        public Task Handle(EndPoint endPoint, TNetSerializable packet, ref SpanBuffer reader);
    }
}
