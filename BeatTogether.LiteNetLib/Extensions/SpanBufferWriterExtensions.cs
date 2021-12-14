using Krypton.Buffers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Extensions
{
    public static class SpanBufferWriterExtensions
    {
        public static void WriteVarULong(this ref SpanBufferWriter writer, ulong value)
        {
            do
            {
                var b = (byte)(value & 127UL);
                value >>= 7;
                if (value != 0UL)
                    b |= 128;
                writer.WriteUInt8(b);
            } while (value != 0UL);
        }

        public static void WriteVarLong(this ref SpanBufferWriter writer, long value) =>
            writer.WriteVarULong((value < 0L ? (ulong)((-value << 1) - 1L) : (ulong)(value << 1)));

        public static void PutVarUInt(this ref SpanBufferWriter writer, uint value) =>
            writer.WriteVarULong(value);

        public static void PutVarInt(this ref SpanBufferWriter writer, int value) =>
            writer.WriteVarLong(value);

        public static void Put(this ref SpanBufferWriter writer, Color value)
        {
            writer.WriteUInt8(value.R);
            writer.WriteUInt8(value.G);
            writer.WriteUInt8(value.B);
            writer.WriteUInt8(value.A);
        }
    }
}
