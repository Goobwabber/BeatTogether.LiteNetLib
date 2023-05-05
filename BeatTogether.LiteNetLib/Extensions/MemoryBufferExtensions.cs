﻿using BeatTogether.LiteNetLib.Util;
using System;
using System.Drawing;
using System.Net;
using System.Text;

namespace BeatTogether.LiteNetLib.Extensions
{
    public static class MemoryBufferExtensions
    {
        #region Read
        public static ulong ReadVarULong(this MemoryBuffer bufferReader)
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

        public static long ReadVarLong(this MemoryBuffer bufferReader)
        {
            var varULong = (long)bufferReader.ReadVarULong();
            if ((varULong & 1L) != 1L)
                return varULong >> 1;
            return -(varULong >> 1) + 1L;
        }

        public static uint ReadVarUInt(this MemoryBuffer bufferReader)
            => (uint)bufferReader.ReadVarULong();

        public static int ReadVarInt(this MemoryBuffer bufferReader)
            => (int)bufferReader.ReadVarLong();

        public static bool TryReadVarULong(this MemoryBuffer bufferReader, out ulong value)
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

        public static bool TryReadVarUInt(this MemoryBuffer bufferReader, out uint value)
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

        public static ReadOnlySpan<byte> ReadVarBytes(this MemoryBuffer bufferReader)
        {
            var length = bufferReader.ReadVarUInt();
            return bufferReader.ReadBytes((int)length).Span;
        }

        public static string ReadString(this MemoryBuffer bufferReader, int maxLength = 65535)
        {
            var length = bufferReader.ReadInt32();
            if (length <= 0 | length > maxLength)
                return string.Empty;
            var bytes = bufferReader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes.Span);
        }

        public static IPEndPoint ReadIPEndPoint(this MemoryBuffer bufferReader)
        {
            if (!IPAddress.TryParse(bufferReader.ReadString(512), out var address))
                throw new ArgumentException("Failed to parse IP address");
            var port = bufferReader.ReadInt32();
            return new IPEndPoint(address, port);
        }

        public static Color ReadColor(this MemoryBuffer reader)
        {
            var r = reader.ReadByte();
            var g = reader.ReadByte();
            var b = reader.ReadByte();
            var a = reader.ReadByte();
            return Color.FromArgb(a, r, g, b);
        }
        #endregion

        #region Write
        public static void WriteVarULong(this MemoryBuffer bufferWriter, ulong value)
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

        public static void WriteVarLong(this MemoryBuffer bufferWriter, long value)
            => bufferWriter.WriteVarULong((value < 0L ? (ulong)((-value << 1) - 1L) : (ulong)(value << 1)));

        public static void WriteVarUInt(this MemoryBuffer buffer, uint value)
            => buffer.WriteVarULong(value);

        public static void WriteVarInt(this MemoryBuffer bufferWriter, int value)
            => bufferWriter.WriteVarLong(value);

        public static void WriteVarBytes(this MemoryBuffer bufferWriter, ReadOnlySpan<byte> bytes)
        {
            bufferWriter.WriteVarUInt((uint)bytes.Length);
            bufferWriter.WriteBytes(bytes);
        }

        public static void WriteString(this MemoryBuffer bufferWriter, string value)
        {
            bufferWriter.WriteInt32(Encoding.UTF8.GetByteCount(value));
            bufferWriter.WriteBytes(Encoding.UTF8.GetBytes(value));
        }

        public static void WriteIPEndPoint(this MemoryBuffer bufferWriter, IPEndPoint ipEndPoint)
        {
            bufferWriter.WriteString(ipEndPoint.Address.ToString());
            bufferWriter.WriteInt32(ipEndPoint.Port);
        }

        public static void WriteColor(this MemoryBuffer writer, Color value)
        {
            writer.WriteUInt8(value.R);
            writer.WriteUInt8(value.G);
            writer.WriteUInt8(value.B);
            writer.WriteUInt8(value.A);
        }
        #endregion
    }
}