using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Util;
using System.Net;

namespace BeatTogether.LiteNetLib.Sources
{
    public abstract class UnconnectedMessageSource
    {
        public void Signal(EndPoint remoteEndPoint, ref SpanBuffer reader, UnconnectedMessageType type)
            => OnReceive(remoteEndPoint, ref reader, type);

        public abstract void OnReceive(EndPoint remoteEndPoint, ref SpanBuffer reader, UnconnectedMessageType type);
    }
}
