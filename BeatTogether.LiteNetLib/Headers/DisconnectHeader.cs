using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers.Abstractions;
using Krypton.Buffers;

namespace BeatTogether.LiteNetLib.Headers
{
    public class DisconnectHeader : BaseLiteNetHeader
    {
        public override PacketProperty Property { get; set; } = PacketProperty.Disconnect;
        public long ConnectTime { get; set; }

        public override void ReadFrom(ref SpanBufferReader bufferReader)
        {
            base.ReadFrom(ref bufferReader);
            ConnectTime = bufferReader.ReadInt64();
        }

        public override void WriteTo(ref SpanBufferWriter bufferWriter)
        {
            base.WriteTo(ref bufferWriter);
            bufferWriter.WriteInt64(ConnectTime);
        }
    }
}
