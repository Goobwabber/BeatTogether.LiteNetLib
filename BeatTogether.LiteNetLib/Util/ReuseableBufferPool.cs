using System;

namespace AsyncUdp
{
    public class ReuseableBufferPool
    {
        private readonly byte[] _Buffer;
        private readonly int SizePerBuffer;
        private readonly object _NewBufferLock = new();
        private int BuffersUsed;
        private int MaxBuffers;
        private int BufferAmount = 0;

        public ReuseableBufferPool(int sizePerBuffer, int BufferCount)
        {
            SizePerBuffer = sizePerBuffer;
            BuffersUsed = -sizePerBuffer;
            MaxBuffers = BufferCount;
            _Buffer = GC.AllocateArray<byte>(SizePerBuffer * BufferCount, pinned: true);
            ReleasedBuffers = new int[BufferCount];
        }

        #region BufferIndexStack
        readonly object _BufferLock = new();
        int[] ReleasedBuffers;
        int CurrentReleasedBuffer = -1;
        private bool TryGetReleasedBuffer(out int BufferOffset)
        {
            lock (_BufferLock)
            {
                if (CurrentReleasedBuffer > -1)
                {
                    BufferOffset = ReleasedBuffers[CurrentReleasedBuffer];
                    CurrentReleasedBuffer--;
                    return true;
                }
                BufferOffset = -1;
                return false;
            }
        }
        private void InternalReturnBuffer(in int BufferOffset)
        {
            lock(_BufferLock)
            {
                if (CurrentReleasedBuffer >= ReleasedBuffers.Length)
                    return;
                CurrentReleasedBuffer++;
                ReleasedBuffers[CurrentReleasedBuffer] = BufferOffset;
                return;
            }
        }
        #endregion

        public bool GetBuffer( out Memory<byte> BufferSlice, out int BufferOffset)
        {
            if (TryGetReleasedBuffer(out int ID))
            {
                BufferOffset = ID;
                BufferSlice = _Buffer.AsMemory(BufferOffset, SizePerBuffer);
                return true;
            }
            lock (_NewBufferLock)
            {
                if (BufferAmount == MaxBuffers)
                {
                    BufferSlice = null;
                    BufferOffset = 0;
                    return false;
                }
                BufferAmount++;
                BuffersUsed += SizePerBuffer;
                BufferOffset = BuffersUsed;
                BufferSlice = _Buffer.AsMemory(BuffersUsed, SizePerBuffer);
            }
            return true;
        }

        public void ReturnBuffer(in int BufferOffset)
        {
            InternalReturnBuffer(BufferOffset);
        }
    }

    public class ResizeableBufferPool
    {
        private readonly byte[] _Buffer;
        private readonly int SizePerBuffer;
        private readonly object _NewBufferLock = new();
        private int BuffersUsed;
        private int BufferAmount = 0;
        private ResizeableBufferPool NextBufferPool = null!;

        public ResizeableBufferPool(int sizePerBuffer, int BufferCount)
        {
            SizePerBuffer = sizePerBuffer;
            BuffersUsed = -sizePerBuffer;
            _Buffer = GC.AllocateArray<byte>(SizePerBuffer * BufferCount, pinned: true);
            ReleasedBuffers = new int[BufferCount];
        }

        #region BufferIndexStack
        readonly object _BufferLock = new();
        int[] ReleasedBuffers;
        int CurrentReleasedBuffer = -1;
        private bool TryGetReleasedBuffer(out int BufferOffset)
        {
            lock (_BufferLock)
            {
                if (CurrentReleasedBuffer > -1)
                {
                    BufferOffset = ReleasedBuffers[CurrentReleasedBuffer];
                    CurrentReleasedBuffer--;
                    return true;
                }
                BufferOffset = -1;
                return false;
            }
        }
        private void InternalReturnBuffer(in int BufferOffset)
        {
            lock (_BufferLock)
            {
                if (CurrentReleasedBuffer >= ReleasedBuffers.Length)
                    return;
                CurrentReleasedBuffer++;
                ReleasedBuffers[CurrentReleasedBuffer] = BufferOffset;
                return;
            }
        }
        #endregion

        public void GetMemoryBuffer(out Memory<byte> BufferSlice, out int BufferOffset)
        {
            if (TryGetReleasedBuffer(out int ID))
            {
                BufferOffset = ID;
                BufferSlice = _Buffer.AsMemory(BufferOffset, SizePerBuffer);
                return;
            }
            lock (_NewBufferLock)
            {
                if (BufferAmount == _Buffer.Length)
                {
                    NextBufferPool ??= new(SizePerBuffer, _Buffer.Length * 2);
                    NextBufferPool.GetMemoryBuffer(out var mem, out var offset);
                    BufferSlice = mem;
                    BufferOffset = offset + BuffersUsed + SizePerBuffer;
                    return;
                }
                BufferAmount++;
                BuffersUsed += SizePerBuffer;
                BufferOffset = BuffersUsed;
                BufferSlice = _Buffer.AsMemory(BuffersUsed, SizePerBuffer);
            }
            return;
        }

        public void GetSpanBuffer(out Span<byte> BufferSlice, out int BufferOffset)
        {
            if (TryGetReleasedBuffer(out int ID))
            {
                BufferOffset = ID;
                BufferSlice = _Buffer.AsSpan(BufferOffset, SizePerBuffer);
                return;
            }
            lock (_NewBufferLock)
            {
                if (BufferAmount == _Buffer.Length)
                {
                    NextBufferPool ??= new(SizePerBuffer, _Buffer.Length * 2);
                    NextBufferPool.GetSpanBuffer(out var mem, out var offset);
                    BufferSlice = mem;
                    BufferOffset = offset + BuffersUsed;
                    return;
                }
                BufferAmount++;
                BuffersUsed += SizePerBuffer;
                BufferOffset = BuffersUsed;
                BufferSlice = _Buffer.AsSpan(BuffersUsed, SizePerBuffer);
            }
            return;
        }

        public void ReturnBuffer(in int BufferOffset)
        {
            if(BufferOffset > BuffersUsed)
            {
                NextBufferPool.ReturnBuffer(BufferOffset - BuffersUsed - SizePerBuffer);
            }
            InternalReturnBuffer(BufferOffset);
        }
    }
}