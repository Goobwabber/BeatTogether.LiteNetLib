using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers.Abstractions;
using BeatTogether.LiteNetLib.Util;

namespace BeatTogether.LiteNetLib.Headers
{
    public class ConnectAcceptHeader : BaseLiteNetHeader
    {
        public override PacketProperty Property { get; set; } = PacketProperty.ConnectAccept;
        public long ConnectTime { get; set; }
        public byte RequestConnectionNumber { get; set; }
        public bool IsReusedPeer { get; set; }

        public override void ReadFrom(ref SpanBuffer bufferReader)
        {
            base.ReadFrom(ref bufferReader);
            ConnectTime = bufferReader.ReadInt64();
            RequestConnectionNumber = bufferReader.ReadByte();
            IsReusedPeer = bufferReader.ReadByte() == 1;
        }

        public override void WriteTo(ref SpanBuffer bufferWriter)
        {
            base.WriteTo(ref bufferWriter);
            bufferWriter.WriteInt64(ConnectTime);
            bufferWriter.WriteUInt8(RequestConnectionNumber);
            bufferWriter.WriteUInt8((byte)(IsReusedPeer ? 1 : 0));
        }
    }
}
