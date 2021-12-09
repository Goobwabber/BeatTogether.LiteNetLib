using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers.Abstractions;
using Krypton.Buffers;

namespace BeatTogether.LiteNetLib.Headers
{
    public class ChanneledHeader : BaseLiteNetHeader
    {
        public override PacketProperty Property { get; set; } = PacketProperty.Channeled;
        public ushort Sequence { get; set; }
        public byte ChannelId { get; set; }

        public override void ReadFrom(ref SpanBufferReader bufferReader)
        {
            base.ReadFrom(ref bufferReader);
            Sequence = bufferReader.ReadUInt16();
            ChannelId = bufferReader.ReadUInt8();
        }

        public override void WriteTo(ref SpanBufferWriter bufferWriter)
        {
            base.WriteTo(ref bufferWriter);
            bufferWriter.WriteUInt16(Sequence);
            bufferWriter.WriteUInt8(ChannelId);
        }
    }
}
