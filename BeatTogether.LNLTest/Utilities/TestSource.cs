using System.Diagnostics;
using System.Net;
using BeatTogether.LiteNetLib;
using BeatTogether.LiteNetLib.Configuration;
using BeatTogether.LiteNetLib.Enums;
using BeatTogether.LiteNetLib.Sources;
using BeatTogether.LiteNetLib.Util;
using Krypton.Buffers;
using Microsoft.Extensions.Logging;

namespace BeatTogether.LNLTest.Utilities;

public class TestSource : ConnectedMessageSource
{
    private ILogger<TestSource> _logger;
    
    public TestSource(LiteNetConfiguration configuration, LiteNetServer server, ILogger<TestSource> logger) : base(configuration, server)
    {
        _logger = logger;
    }

    public override void OnReceive(EndPoint remoteEndPoint, ref MemoryBuffer reader, DeliveryMethod method)
    {
        // _logger.LogInformation("Got Packet");
    }
}