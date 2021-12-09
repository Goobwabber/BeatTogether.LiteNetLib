using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers.Abstractions;

namespace BeatTogether.LiteNetLib.Headers
{
    public class UnconnectedHeader : BaseLiteNetHeader
    {
        public override PacketProperty Property { get; set; } = PacketProperty.UnconnectedMessage;
    }
}
