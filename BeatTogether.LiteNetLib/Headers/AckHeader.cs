using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers.Abstractions;
using BeatTogether.LiteNetLib.Util;
using System;
using System.Collections.Generic;

namespace BeatTogether.LiteNetLib.Headers
{
    public class AckHeader : BaseLiteNetHeader
    {
        public override PacketProperty Property { get; set; } = PacketProperty.Ack;
        public ushort Sequence { get; set; }
        public byte ChannelId { get; set; }
        public List<int> Acknowledgements { get; set; } = new();
        public int WindowSize { get; set; }

        public override void ReadFrom(ref SpanBuffer bufferReader)
        {
            base.ReadFrom(ref bufferReader);
            Sequence = bufferReader.ReadUInt16();
            ChannelId = bufferReader.ReadUInt8();

            // If bit is 1, add it's index to 'Acknowledgements'
            byte[] bytes = bufferReader.ReadBytes(bufferReader.RemainingSize).ToArray();
            for (int currentByte = 0; currentByte < bytes.Length; currentByte++)
                for (int currentBit = 0; currentBit < 8; currentBit++)
                    if ((bytes[currentByte] & (1 << currentBit)) != 0)
                        Acknowledgements.Add((currentByte * 8) + currentBit);
        }

        public override void WriteTo(ref SpanBuffer bufferWriter)
        {
            base.WriteTo(ref bufferWriter);
            bufferWriter.WriteUInt16(Sequence);
            bufferWriter.WriteUInt8(ChannelId);

            // Set bit at index of each acknowledgement
            byte[] bytes = new byte[(WindowSize - 1) / 8 + 2];
            foreach (int acknowledgement in Acknowledgements)
            {
                int ackByte = acknowledgement / 8;
                int ackBit = acknowledgement % 8;
                bytes[ackByte] |= (byte)(1 << ackBit);
            }
            bufferWriter.WriteBytes(bytes);
        }

        public override void ReadFrom(ref MemoryBuffer bufferReader)
        {
            base.ReadFrom(ref bufferReader);
            Sequence = bufferReader.ReadUInt16();
            ChannelId = bufferReader.ReadUInt8();

            // If bit is 1, add it's index to 'Acknowledgements'
            byte[] bytes = bufferReader.ReadBytes(bufferReader.RemainingSize).ToArray();
            for (int currentByte = 0; currentByte < bytes.Length; currentByte++)
                for (int currentBit = 0; currentBit < 8; currentBit++)
                    if ((bytes[currentByte] & (1 << currentBit)) != 0)
                        Acknowledgements.Add((currentByte * 8) + currentBit);
        }

        public override void WriteTo(ref MemoryBuffer bufferWriter)
        {
            base.WriteTo(ref bufferWriter);
            bufferWriter.WriteUInt16(Sequence);
            bufferWriter.WriteUInt8(ChannelId);

            // Set bit at index of each acknowledgement
            Span<byte> bytes = stackalloc byte[(WindowSize - 1) / 8 + 2];
            foreach (int acknowledgement in Acknowledgements)
            {
                int ackByte = acknowledgement / 8;
                int ackBit = acknowledgement % 8;
                bytes[ackByte] |= (byte)(1 << ackBit);
            }
            bufferWriter.WriteBytes(bytes);
        }
    }
}
