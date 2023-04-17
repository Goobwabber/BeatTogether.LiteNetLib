using BeatTogether.LiteNetLib.Util;
using Krypton.Buffers;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public interface INetSerializable
    {
        public void WriteTo(ref SpanBuffer bufferWriter);
        public void ReadFrom(ref SpanBuffer bufferReader);
        public void WriteTo(ref MemoryBuffer bufferWriter);
        public void ReadFrom(ref MemoryBuffer bufferReader);
    }
}
