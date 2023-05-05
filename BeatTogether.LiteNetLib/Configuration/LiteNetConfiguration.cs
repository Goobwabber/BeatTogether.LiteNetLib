namespace BeatTogether.LiteNetLib.Configuration
{
    public class LiteNetConfiguration
    {
        public ushort MaxSequence { get; set; } = 32768;
        public int WindowSize { get; set; } = 256;
        public int ReliableRetryDelay { get; set; } = 80;
        public int ReliableRetries { get; set; } = 15;
        public int ReliableRetryDelayAfterRetrys { get; set; } = 1000;
        public int MaxPacketSize { get; set; } = 1432;
        public int PingDelay { get; set; } = 1000;
        public int TimeoutSeconds { get; set; } = 5;
        public int TimeoutRefreshDelay { get; set; } = 10;
        public bool RecieveAsync { get; set; } = false;
        public int MaxConcurrentSends { get; set; } = 10;
    }
}
