using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BeatTogether.LiteNetLib.Models
{
    public class AckWindow
    {
        private readonly ConcurrentHashSet<int> _acknowledgements = new();
        private readonly int _queueSize;
        private readonly int _windowSize;
        private object _windowPositionLock = new();
        private int _windowPosition = 0;

        /// <summary>
        /// Creates a window array.
        /// </summary>
        /// <param name="windowSize">Size of the window</param>
        /// <param name="queueSize">Size of the array to iterate along</param>
        public AckWindow(int windowSize, int queueSize)
        {
            _queueSize = queueSize;
            _windowSize = windowSize;
        }

        /// <summary>
        /// Adds a new value to window and advances to it
        /// </summary>
        /// <param name="index">index to add to window</param>
        /// <returns>True if index was successfully added</returns>
        public bool Add(int index)
        {
            if (!_acknowledgements.TryAdd(index))
                return false;
            AdvanceTo(index);
            return true;
        }

        public List<int> GetWindow(out int windowPosition)
        {
            windowPosition = _windowPosition;
            return _acknowledgements.Values
                .Where(x => 
                {
                    var rel = (x - _windowPosition + _queueSize * 1.5) % _queueSize - _queueSize / 2;
                    return rel < _windowSize && rel >= 0;
                })
                .Select(x => (x % _windowSize))
                .ToList();
            // x % _windowSize should be (x - _windowPosition) % _queueSize but litenetlib bad
            // refer to AckPacketHandler comment for more information
        }

        private void AdvanceTo(int position)
        {
            lock(_windowPositionLock)
            {
                var posRelative = (position - _windowPosition + _queueSize * 1.5) % _queueSize - _queueSize / 2;
                if (posRelative < _windowSize)
                    return; // Do not need to advance
                var targetPosition = (position - _windowSize + 1) % _queueSize;
                while (_windowPosition != targetPosition)
                {
                    _acknowledgements.TryRemove((_windowPosition + _windowSize) % _queueSize);
                    _windowPosition = (_windowPosition + 1) % _queueSize;
                }
            }
        }
    }
}
