using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using Krypton.Buffers;

namespace BeatTogether.LiteNetLib.Headers.Abstractions
{
    public abstract class BaseLiteNetHeader : INetSerializable
    {
        public abstract PacketProperty Property { get; set; }
        public byte ConnectionNumber { get; set; }
        public bool IsFragmented { get; set; }

        public virtual void ReadFrom(ref SpanBufferReader bufferReader)
        {
            byte b = bufferReader.ReadByte();
            Property = (PacketProperty)(b & 0x1f);          // 0x1f 00011111
            ConnectionNumber = (byte)((b & 0x60) >> 5);     // 0x60 01100000
            IsFragmented = (b & 0x80) != 0;                 // 0x80 10000000
        }

        public virtual void WriteTo(ref SpanBufferWriter bufferWriter)
        {
            byte b = (byte)Property;
            b |= (byte)(ConnectionNumber << 5);
            b |= (byte)(IsFragmented ? 0x80 : 0x00);
            bufferWriter.WriteUInt8(b);
        }
    }
}
