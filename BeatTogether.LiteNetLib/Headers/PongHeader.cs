using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers.Abstractions;
using BeatTogether.LiteNetLib.Util;
using Krypton.Buffers;

namespace BeatTogether.LiteNetLib.Headers
{
    public class PongHeader : BaseLiteNetHeader
    {
        public override PacketProperty Property { get; set; } = PacketProperty.Pong;
        public ushort Sequence { get; set; }
        public long Time { get; set; }

        public override void ReadFrom(ref SpanBuffer bufferReader)
        {
            base.ReadFrom(ref bufferReader);
            Sequence = bufferReader.ReadUInt16();
            Time = bufferReader.ReadInt64();
        }

        public override void WriteTo(ref SpanBuffer bufferWriter)
        {
            base.WriteTo(ref bufferWriter);
            bufferWriter.WriteUInt16(Sequence);
            bufferWriter.WriteInt64(Time);
        }
    }
}
