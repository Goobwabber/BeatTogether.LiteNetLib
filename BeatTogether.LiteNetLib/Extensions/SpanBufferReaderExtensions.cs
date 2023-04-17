using BeatTogether.LiteNetLib.Util;
using Krypton.Buffers;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Net;
using System.Text;

namespace BeatTogether.LiteNetLib.Extensions
{
    public static class SpanBufferReaderExtensions
    {
        public static ulong ReadVarULong(this ref SpanBuffer bufferReader)
        {
            var value = 0UL;
            var shift = 0;
            var b = bufferReader.ReadByte();
            while ((b & 128UL) != 0UL)
            {
                value |= (b & 127UL) << shift;
                shift += 7;
                b = bufferReader.ReadByte();
            }
            return value | (ulong)b << shift;
        }

        public static long ReadVarLong(this ref SpanBuffer bufferReader)
        {
            var varULong = (long)bufferReader.ReadVarULong();
            if ((varULong & 1L) != 1L)
                return varULong >> 1;
            return -(varULong >> 1) + 1L;
        }

        public static uint ReadVarUInt(this ref SpanBuffer bufferReader)
            => (uint)bufferReader.ReadVarULong();

        public static int ReadVarInt(this ref SpanBuffer bufferReader)
            => (int)bufferReader.ReadVarLong();

        public static bool TryReadVarULong(this ref SpanBuffer bufferReader, out ulong value)
        {
            value = 0UL;
            var shift = 0;
            while (shift <= 63 && bufferReader.RemainingSize >= 1)
            {
                var b = bufferReader.ReadByte();
                value |= (ulong)(b & 127) << shift;
                shift += 7;
                if ((b & 128) == 0)
                    return true;
            }

            value = 0UL;
            return false;
        }

        public static bool TryReadVarUInt(this ref SpanBuffer bufferReader, out uint value)
        {
            ulong num;
            if (bufferReader.TryReadVarULong(out num) && (num >> 32) == 0UL)
            {
                value = (uint)num;
                return true;
            }

            value = 0U;
            return false;
        }

        public static ReadOnlySpan<byte> ReadVarBytes(this ref SpanBuffer bufferReader)
        {
            var length = bufferReader.ReadVarUInt();
            return bufferReader.ReadBytes((int)length);
        }

        public static string ReadString(this ref SpanBuffer bufferReader, int maxLength = 65535)
        {
            var length = bufferReader.ReadInt32();
            if (length <= 0 | length > maxLength)
                return string.Empty;
            var bytes = bufferReader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        public static IPEndPoint ReadIPEndPoint(this ref SpanBuffer bufferReader)
        {
            if (!IPAddress.TryParse(bufferReader.ReadString(512), out var address))
                throw new ArgumentException("Failed to parse IP address");
            var port = bufferReader.ReadInt32();
            return new IPEndPoint(address, port);
        }

        public static Color ReadColor(this ref SpanBuffer reader)
        {
            var r = reader.ReadByte();
            var g = reader.ReadByte();
            var b = reader.ReadByte();
            var a = reader.ReadByte();
            return Color.FromArgb(a, r, g, b);
        }
    }
}
