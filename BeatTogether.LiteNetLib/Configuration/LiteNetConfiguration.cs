namespace BeatTogether.LiteNetLib.Configuration
{
    public class LiteNetConfiguration
    {
        public ushort MaxSequence { get; set; } = 32768;
        public int WindowSize { get; set; } = 64;
        public int ReliableRetryDelay { get; set; } = 27;
        public int MaximumReliableRetries { get; set; } = -1;
        public int MaxPacketSize { get; set; } = 1432;
        public int PingDelay { get; set; } = 1000;
        public int TimeoutDelay { get; set; } = 5000;
        public int TimeoutRefreshDelay { get; set; } = 15;
        public int MaxAsyncSocketOperations { get; set; } = 4;
    }
}
