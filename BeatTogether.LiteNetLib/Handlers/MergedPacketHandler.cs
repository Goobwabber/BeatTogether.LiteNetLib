using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using BeatTogether.LiteNetLib.Util;
using Krypton.Buffers;
using System;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Handlers
{
    public class MergedPacketHandler : BasePacketHandler<MergedHeader>
    {
        private readonly LiteNetServer _server;

        public MergedPacketHandler(
            LiteNetServer server)
        {
            _server = server;
        }

        public override Task Handle(EndPoint endPoint, MergedHeader packet, ref MemoryBuffer reader)
        {
            while (reader.RemainingSize > 0)
            {
                Memory<byte> newPacket;
                try
                {
                    ushort size = reader.ReadUInt16();
                    newPacket = reader.ReadBytes(size);
                }
                catch(EndOfBufferException) { return Task.CompletedTask; }
                _server.HandlePacket(endPoint, newPacket);
            }
            return Task.CompletedTask;
        }
    }
}
