using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Models
{
    public class Window
    {
        private object _windowLock = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _enterWindowTcs = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _completionTcs = new();
        private readonly int _queueSize;
        private readonly int _size;
        private int _position = 0;
        private object _queuePositionLock = new();
        private int _queuePosition = -1;

        public Window(int size, int queueSize)
        {
            _queueSize = queueSize;
            _size = size;

            // Create and free indices inside window
            for (int i = 0; i < _size; i++)
            {
                _enterWindowTcs[i] = new();
                _enterWindowTcs[i].SetResult();
            }
        }

        public Task WaitForNext(out int nextIndex)
        {
            lock(_queuePositionLock)
            {
                _queuePosition = _queuePosition + 1 % _queueSize;
                nextIndex = _queuePosition;
            }
            return WaitForIndex(nextIndex);
        }

        public Task WaitForIndex(int index)
            => _enterWindowTcs.GetOrAdd(index, _ => new()).Task;

        public Task WaitForCompletion(int index)
            => _completionTcs.GetOrAdd(index, _ => new()).Task;

        public void CompleteIndex(int index)
        {
            // Cleanup task
            _enterWindowTcs.TryRemove(index, out _);

            // Trigger completion listener
            if (_completionTcs.TryRemove(index, out TaskCompletionSource tcs))
                tcs.SetResult();

            // Lock and advance window if needed
            lock (_windowLock)
            {
                if (index == _position)
                    AdvanceWindow();
            }
        }

        private void AdvanceWindow()
        {
            _position = (_position + 1) % _queueSize;

            // Trigger any listeners for index that has entered window
            var enterWindowIndex = (_position + _size - 1) % _queueSize;
            if (_enterWindowTcs.TryGetValue(enterWindowIndex, out TaskCompletionSource tcs))
                tcs.SetResult();

            // Advance window again if current index has been handled
            if (!_enterWindowTcs.ContainsKey(_position))
                AdvanceWindow();
        }
    }
}
