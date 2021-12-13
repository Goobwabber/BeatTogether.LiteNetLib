using BeatTogether.LiteNetLib.Enums;
using Krypton.Buffers;
using System.Net;

namespace BeatTogether.LiteNetLib.Delegates
{
    public delegate void ClientDisconnectHandler(EndPoint peer, DisconnectReason reason, ref SpanBufferReader additionalData);
}
