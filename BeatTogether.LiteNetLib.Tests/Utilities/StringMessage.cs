using BeatTogether.LiteNetLib.Abstractions;
using Krypton.Buffers;

namespace BeatTogether.LiteNetLib.Tests.Utilities
{
    public class StringMessage : INetSerializable
    {
        public string Value { get; set; }

        public void ReadFrom(ref SpanBufferReader bufferReader)
        {
            Value = bufferReader.ReadUTF8String();
        }

        public void WriteTo(ref SpanBufferWriter bufferWriter)
        {
            bufferWriter.WriteUTF8String(Value);
        }
    }
}
