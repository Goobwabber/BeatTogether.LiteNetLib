using Krypton.Buffers;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public interface INetSerializable
    {
        public void WriteTo(ref SpanBufferWriter bufferWriter);
        public void ReadFrom(ref SpanBufferReader bufferReader);
    }
}
