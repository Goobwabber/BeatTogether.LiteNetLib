using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using Krypton.Buffers;
using System.Runtime.Serialization;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetPacketReader : IPacketReader
    {
        private readonly LiteNetPacketRegistry _packetRegistry;

        public LiteNetPacketReader(
            LiteNetPacketRegistry packetRegistry)
        {
            _packetRegistry = packetRegistry;
        }

        public INetSerializable ReadFrom(ref SpanBufferReader reader)
        {
            byte b = reader.RemainingData[0];
            PacketProperty property = (PacketProperty)(b & 0x1f);   // 0x1f 00011111
            byte connectionNumber = (byte)((b & 0x60) >> 5);        // 0x60 01100000
            bool isFragmented = (b & 0x80) != 0;                    // 0x80 10000000

            if (_packetRegistry.TryCreatePacket(property, out var packet))
                return packet;
            throw new InvalidDataContractException(
                $"Packet property not registered with '{_packetRegistry.GetType().Name}' " +
                $"(PacketProperty={property})."
            );
        }
    }
}
