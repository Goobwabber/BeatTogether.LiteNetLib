using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Handlers;
using BeatTogether.LiteNetLib.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace BeatTogether.LiteNetLib.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLiteNetMessaging(this IServiceCollection services) =>
            services
                .AddSingleton<LiteNetPacketRegistry>()
                .AddSingleton<IPacketHandler<AckHeader>, AckPacketHandler>()
                .AddSingleton<IPacketHandler<BroadcastHeader>, BroadcastPacketHandler>()
                .AddSingleton<IPacketHandler<ChanneledHeader>, ChanneledPacketHandler>()
                .AddSingleton<IPacketHandler<ConnectRequestHeader>, ConnectRequestHandler>()
                .AddSingleton<IPacketHandler<DisconnectHeader>, DisconnectPacketHandler>()
                .AddSingleton<IPacketHandler<MergedHeader>, MergedPacketHandler>()
                .AddSingleton<IPacketHandler<MtuCheckHeader>, MtuCheckPacketHandler>()
                .AddSingleton<IPacketHandler<PingHeader>, PingHandler>()
                .AddSingleton<IPacketHandler<PongHeader>, PongHandler>()
                .AddSingleton<IPacketHandler<UnconnectedHeader>, UnconnectedPacketHandler>()
                .AddSingleton<IPacketHandler<UnreliableHeader>, UnreliablePacketHandler>();
    }
}
