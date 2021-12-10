using System;
using System.Net;

namespace BeatTogether.LiteNetLib.Delegates
{
    public delegate void PacketDispatchHandler(EndPoint endPoint, ReadOnlySpan<byte> buffer);
}
