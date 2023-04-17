using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Util;
using Krypton.Buffers;
using System.Net;

namespace BeatTogether.LiteNetLib.Sources
{
    public abstract class UnconnectedMessageSource
    {
        public void Signal(EndPoint remoteEndPoint, ref MemoryBuffer reader, UnconnectedMessageType type)
            => OnReceive(remoteEndPoint, ref reader, type);

        public abstract void OnReceive(EndPoint remoteEndPoint, ref MemoryBuffer reader, UnconnectedMessageType type);
    }
}
