using Krypton.Buffers;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public interface IPacketHandler<TSendable> where TSendable : INetSerializable
    {
        public Task Handle(IPEndPoint endPoint, TSendable packet, ref SpanBufferReader reader);
    }
}
