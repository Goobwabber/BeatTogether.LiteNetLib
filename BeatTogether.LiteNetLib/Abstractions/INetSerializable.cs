using BeatTogether.LiteNetLib.Util;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public interface INetSerializable
    {
        public void WriteTo(ref SpanBuffer bufferWriter);
        public void ReadFrom(ref SpanBuffer bufferReader);
    }
}
