using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers.Abstractions;
using Krypton.Buffers;

namespace BeatTogether.LiteNetLib.Headers
{
    public class ConnectRequestHeader : BaseLiteNetHeader
    {
        public override PacketProperty Property { get; set; } = PacketProperty.ConnectRequest;
        public int ProtocolId { get; set; }
        public long ConnectionTime { get; set; }
        public byte[] Address { get; set; }

        public override void ReadFrom(ref SpanBufferReader bufferReader)
        {
                base.ReadFrom(ref bufferReader);            //1
                ProtocolId = bufferReader.ReadInt32();      //4
                ConnectionTime = bufferReader.ReadInt64();  //8
                int addressSize = bufferReader.ReadByte();  //1 = 14 bytes
                Address = bufferReader.ReadBytes(addressSize).ToArray(); //and misreads this 
        }

        public override void WriteTo(ref SpanBufferWriter bufferWriter)
        {
            base.WriteTo(ref bufferWriter);
            bufferWriter.WriteInt32(ProtocolId);
            bufferWriter.WriteInt64(ConnectionTime);
            bufferWriter.WriteUInt8((byte)(Address.Length - 1));
            bufferWriter.WriteBytes(Address);
        }
    }
}
