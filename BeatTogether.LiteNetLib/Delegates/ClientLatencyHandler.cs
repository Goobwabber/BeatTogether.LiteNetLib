using System.Net;

namespace BeatTogether.LiteNetLib.Delegates
{
    public delegate void ClientLatencyHandler(EndPoint endPoint, long latency);
}
