using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers.Abstractions;
using BeatTogether.LiteNetLib.Util;
using Krypton.Buffers;

namespace BeatTogether.LiteNetLib.Headers
{
    public class ConnectRequestHeader : BaseLiteNetHeader
    {
        public override PacketProperty Property { get; set; } = PacketProperty.ConnectRequest;
        public int ProtocolId { get; set; }
        public long ConnectionTime { get; set; }
        public byte[] Address { get; set; }

        public override void ReadFrom(ref SpanBuffer bufferReader)
        {
            base.ReadFrom(ref bufferReader);
            ProtocolId = bufferReader.ReadInt32();
            ConnectionTime = bufferReader.ReadInt64();
            int addressSize = bufferReader.ReadByte();
            Address = bufferReader.ReadBytes(addressSize).ToArray();
        }

        public override void WriteTo(ref SpanBuffer bufferWriter)
        {
            base.WriteTo(ref bufferWriter);
            bufferWriter.WriteInt32(ProtocolId);
            bufferWriter.WriteInt64(ConnectionTime);
            bufferWriter.WriteUInt8((byte)(Address.Length - 1));
            bufferWriter.WriteBytes(Address);
        }        
        public override void ReadFrom(ref MemoryBuffer bufferReader)
        {
            base.ReadFrom(ref bufferReader);
            ProtocolId = bufferReader.ReadInt32();
            ConnectionTime = bufferReader.ReadInt64();
            int addressSize = bufferReader.ReadByte();
            Address = bufferReader.ReadBytes(addressSize).ToArray();
        }

        public override void WriteTo(ref MemoryBuffer bufferWriter)
        {
            base.WriteTo(ref bufferWriter);
            bufferWriter.WriteInt32(ProtocolId);
            bufferWriter.WriteInt64(ConnectionTime);
            bufferWriter.WriteUInt8((byte)(Address.Length - 1));
            bufferWriter.WriteBytes(Address);
        }
    }
}
