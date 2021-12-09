using Krypton.Buffers;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public interface IPacketReader
    {
        /// <summary>
        /// Reads a message from the given buffer.
        /// It must include packet headers.
        /// </summary>
        /// <param name="bufferReader">The buffer to read from.</param>
        /// <returns>The deserialized packet.</returns>
        INetSerializable ReadFrom(ref SpanBufferReader bufferReader);
    }
}
