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
                .AddTransient<IPacketHandler<AckHeader>, AckPacketHandler>()
                .AddTransient<IPacketHandler<BroadcastHeader>, BroadcastPacketHandler>()
                .AddTransient<IPacketHandler<ChanneledHeader>, ChanneledPacketHandler>()
                .AddTransient<IPacketHandler<ConnectRequestHeader>, ConnectRequestHandler>()
                .AddTransient<IPacketHandler<DisconnectHeader>, DisconnectPacketHandler>()
                .AddTransient<IPacketHandler<MtuCheckHeader>, MtuCheckPacketHandler>()
                .AddTransient<IPacketHandler<PingHeader>, PingHandler>()
                .AddTransient<IPacketHandler<PongHeader>, PongHandler>()
                .AddTransient<IPacketHandler<UnconnectedHeader>, UnconnectedPacketHandler>()
                .AddTransient<IPacketHandler<UnreliableHeader>, UnreliablePacketHandler>();
    }
}
