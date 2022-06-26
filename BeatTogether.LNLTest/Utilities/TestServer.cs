using System.Net;
using BeatTogether.LiteNetLib;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using Krypton.Buffers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BeatTogether.LNLTest.Utilities;

public class TestServer : LiteNetServer, IHostedService
{
    public const int _Port = 1234;

    private readonly ILogger _logger;

    public TestServer(LiteNetConfiguration configuration, LiteNetPacketRegistry packetRegistry,
        IServiceProvider serviceProvider, ILogger<TestServer> logger) : base(
        new IPEndPoint(IPAddress.Loopback, _Port), configuration, packetRegistry, serviceProvider, 4, true)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting server...");
        if (!Start())
        {
            throw new Exception("shit couldn't listen ???");
        }

        return Task.CompletedTask;
    }

    public override bool ShouldAcceptConnection(EndPoint endPoint, ref SpanBufferReader additionalData)
    {
        _logger.LogInformation("ShouldAcceptConnection");
        return true;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Stop();
        return Task.CompletedTask;
    }

    protected override void OnStarted()
    {
        _logger.LogInformation("OnStarted");
        base.OnStarted();
    }

    protected override void OnReceived(EndPoint endPoint, ReadOnlySpan<byte> buffer)
    {
        base.OnReceived(endPoint, buffer);
    }

    public override void OnConnect(EndPoint endPoint)
    {
        _logger.LogInformation("OnConnect");
        base.OnConnect(endPoint);
    }

    public override void OnDisconnect(EndPoint endPoint, DisconnectReason reason)
    {
        _logger.LogInformation($"OnDisconnect: {reason}");
        // if (reason == DisconnectReason.Timeout)
        // {
            // Debugger.Break();
        // }
        // base.OnDisconnect(endPoint, reason);
        // Environment.Exit(0);
    }

    public override void OnLatencyUpdate(EndPoint endPoint, int latency)
    {
        if (latency > 5000)
        {
            _logger.LogInformation($"Latency is slow lol {latency}");
        }
        // _logger.LogInformation("OnLatencyUpdate");
        base.OnLatencyUpdate(endPoint, latency);
    }

    protected override void OnSent(EndPoint endpoint, long sent)
    {
        base.OnSent(endpoint, sent);
    }

    protected override void OnStopped()
    {
        _logger.LogInformation("OnStopped");
        base.OnStopped();
    }

    /*
    protected override void OnError(SocketError error)
    {
        _logger.LogInformation("OnError");
        base.OnError(error);
        Environment.Exit(0);
    }
    */
    protected override void HandlePacket(EndPoint endPoint, ReadOnlySpan<byte> buffer)
    {
        // _logger.LogInformation($"HANDLE PACKET SIZE {buffer.Length}");
        base.HandlePacket(endPoint, buffer);
    }
}