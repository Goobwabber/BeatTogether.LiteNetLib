using System;
using System.Net;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Dispatchers.Abstractions
{
    public interface IMessageDispatcher
    {
        public void Send(EndPoint endPoint, ReadOnlySpan<byte> message);
    }
}
