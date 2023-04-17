using BeatTogether.LiteNetLib.Util;
using Krypton.Buffers;
using System;
using System.Drawing;
using System.Net;
using System.Text;

namespace BeatTogether.LiteNetLib.Extensions
{
    public static class SpanBufferWriterExtensions
    {
        public static void WriteVarULong(this ref SpanBuffer bufferWriter, ulong value)
        {
            do
            {
                var b = (byte)(value & 127UL);
                value >>= 7;
                if (value != 0UL)
                    b |= 128;
                bufferWriter.WriteUInt8(b);
            } while (value != 0UL);
        }

        public static void WriteVarLong(this ref SpanBuffer bufferWriter, long value)
            => bufferWriter.WriteVarULong((value < 0L ? (ulong)((-value << 1) - 1L) : (ulong)(value << 1)));

        public static void WriteVarUInt(this ref SpanBuffer buffer, uint value)
            => buffer.WriteVarULong(value);

        public static void WriteVarInt(this ref SpanBuffer bufferWriter, int value)
            => bufferWriter.WriteVarLong(value);

        public static void WriteVarBytes(this ref SpanBuffer bufferWriter, ReadOnlySpan<byte> bytes)
        {
            bufferWriter.WriteVarUInt((uint)bytes.Length);
            bufferWriter.WriteBytes(bytes);
        }

        public static void WriteString(this ref SpanBuffer bufferWriter, string value)
        {
            bufferWriter.WriteInt32(Encoding.UTF8.GetByteCount(value));
            bufferWriter.WriteBytes(Encoding.UTF8.GetBytes(value));
        }

        public static void WriteIPEndPoint(this ref SpanBuffer bufferWriter, IPEndPoint ipEndPoint)
        {
            bufferWriter.WriteString(ipEndPoint.Address.ToString());
            bufferWriter.WriteInt32(ipEndPoint.Port);
        }

        public static void WriteColor(this ref SpanBuffer writer, Color value)
        {
            writer.WriteUInt8(value.R);
            writer.WriteUInt8(value.G);
            writer.WriteUInt8(value.B);
            writer.WriteUInt8(value.A);
        }
    }
}
