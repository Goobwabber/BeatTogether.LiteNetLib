using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers.Abstractions;

namespace BeatTogether.LiteNetLib.Headers
{
    public class InvalidProtocolHeader : BaseLiteNetHeader
    {
        public override PacketProperty Property { get; set; } = PacketProperty.InvalidProtocol;
    }
}
