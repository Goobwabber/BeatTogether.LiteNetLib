using BeatTogether.LiteNetLib.Abstractions;
using Krypton.Buffers;
using Microsoft.Extensions.Logging;

namespace BeatTogether.LiteNetLib.Tests.Utilities
{
    public class DebugReader : LiteNetPacketReader
    {
        private readonly ILogger _logger;

        public DebugReader(
            LiteNetPacketRegistry packetRegistry, 
            ILogger<LiteNetPacketReader> logger) 
            : base(packetRegistry)
        {
            _logger = logger;
        }

        public override INetSerializable ReadFrom(ref SpanBufferReader reader)
        {
            _logger.LogTrace($"Reading packet [{string.Join(", ", reader.RemainingData.ToArray())}]");
            return base.ReadFrom(ref reader);
        }
    }
}
