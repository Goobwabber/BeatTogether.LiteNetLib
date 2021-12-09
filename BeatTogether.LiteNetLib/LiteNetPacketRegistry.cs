using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Headers;

namespace BeatTogether.LiteNetLib
{
    public class LiteNetPacketRegistry : BasePacketRegistry
    {
        public override void Register()
        {
            AddPacket<UnreliableHeader>(PacketProperty.Unreliable);
            AddPacket<ChanneledHeader>(PacketProperty.Channeled);
            AddPacket<AckHeader>(PacketProperty.Ack);
            AddPacket<PingHeader>(PacketProperty.Ping);
            AddPacket<PongHeader>(PacketProperty.Pong);
            AddPacket<ConnectRequestHeader>(PacketProperty.ConnectRequest);
            AddPacket<ConnectAcceptHeader>(PacketProperty.ConnectAccept);
            AddPacket<DisconnectHeader>(PacketProperty.Disconnect);
            AddPacket<UnconnectedHeader>(PacketProperty.UnconnectedMessage);
            AddPacket<MtuCheckHeader>(PacketProperty.MtuCheck);
            AddPacket<MtuOkHeader>(PacketProperty.MtuOk);
            AddPacket<BroadcastHeader>(PacketProperty.Broadcast);
            AddPacket<MergedHeader>(PacketProperty.Merged);
            AddPacket<ShutdownOkHeader>(PacketProperty.ShutdownOk);
            AddPacket<PeerNotFoundHeader>(PacketProperty.PeerNotFound);
            AddPacket<InvalidProtocolHeader>(PacketProperty.InvalidProtocol);
            AddPacket<NatMessageHeader>(PacketProperty.NatMessage);
            AddPacket<EmptyHeader>(PacketProperty.Empty);
        }
    }
}
