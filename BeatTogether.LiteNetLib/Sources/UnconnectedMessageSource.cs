using BeatTogether.LiteNetLib.Enums;
using Krypton.Buffers;
using System.Net;

namespace BeatTogether.LiteNetLib.Sources
{
    public abstract class UnconnectedMessageSource
    {
        public void Signal(EndPoint remoteEndPoint, ref SpanBufferReader reader, UnconnectedMessageType type)
            => OnReceive(remoteEndPoint, ref reader, type);

        public abstract void OnReceive(EndPoint remoteEndPoint, ref SpanBufferReader reader, UnconnectedMessageType type);
    }
}
