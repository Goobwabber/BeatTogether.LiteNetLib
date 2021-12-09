using Krypton.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace BeatTogether.LiteNetLib.Extensions
{
    public static class SpanBufferReaderExtensions
    {
        public static ulong ReadVarULong(this ref SpanBufferReader reader)
        {
            var value = 0UL;
            var shift = 0;
            var b = reader.ReadByte();
            while ((b & 128UL) != 0UL)
            {
                value |= (b & 127UL) << shift;
                shift += 7;
                b = reader.ReadByte();
            }
            return value | (ulong)b << shift;
        }

        public static long ReadVarLong(this ref SpanBufferReader reader)
        {
            var varULong = (long)reader.ReadVarULong();
            if ((varULong & 1L) != 1L)
                return varULong >> 1;
            return -(varULong >> 1) + 1L;
        }

        public static uint ReadVarUInt(this ref SpanBufferReader reader) =>
            (uint)reader.ReadVarULong();

        public static int ReadVarInt(this ref SpanBufferReader reader) =>
            (int)reader.ReadVarLong();

        public static bool TryReadVarULong(this ref SpanBufferReader reader, [MaybeNullWhen(false)] out ulong value)
        {
            value = 0UL;
            var shift = 0;
            while (shift <= 63 && reader.RemainingSize >= 1)
            {
                var b = reader.ReadByte();
                value |= (ulong)(b & 127) << shift;
                shift += 7;
                if ((b & 128) == 0)
                    return true;
            }

            value = 0UL;
            return false;
        }

        public static bool TryReadVarUInt(this ref SpanBufferReader reader, [MaybeNullWhen(false)] out uint value)
        {
            ulong num;
            if (reader.TryReadVarULong(out num) && (num >> 32) == 0UL)
            {
                value = (uint)num;
                return true;
            }

            value = 0U;
            return false;
        }

        public static Color ReadColor(this ref SpanBufferReader reader)
        {
            var r = reader.ReadByte();
            var g = reader.ReadByte();
            var b = reader.ReadByte();
            var a = reader.ReadByte();
            return Color.FromArgb(a, r, g, b);
        }
    }
}
