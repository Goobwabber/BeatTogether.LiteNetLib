using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BeatTogether.LiteNetLib.Models
{
    public class ArrayWindow
    {
        private readonly ConcurrentDictionary<int, int> _array = new();
        private readonly int _queueSize;
        private readonly int _windowSize;
        private object _windowPositionLock = new();
        private int _windowPosition = 0;
        private object _queuePositionLock = new();
        private int _queuePosition = 0;

        /// <summary>
        /// Creates a window array.
        /// </summary>
        /// <param name="windowSize">Size of the window</param>
        /// <param name="queueSize">Size of the array to iterate along</param>
        public ArrayWindow(int windowSize, int queueSize)
        {
            _queueSize = queueSize;
            _windowSize = windowSize;
        }

        /// <summary>
        /// Adds a new value to window and advances to it
        /// </summary>
        /// <param name="index">index to add to window</param>
        public void Add(int index)
        {
            lock (_windowPositionLock)
            {
                lock (_queuePositionLock)
                {
                    var queueRelative = (index - _windowPosition + _queueSize * 1.5) % _queueSize - _queueSize / 2;
                    if (index < 0)
                        return; // Too old
                    if (index >= _windowSize * 2)
                        return; // Too new

                    _queuePosition = index;
                }
            }
            _array.TryAdd(index, index);
            Advance();
        }

        public int GetWindowPosition()
        {
            lock (_windowPositionLock)
            {
                return _windowPosition;
            }
        }

        public List<int> GetWindow()
        {
            lock (_windowPositionLock)
            {
                return _array.Values.ToList();
            }
        }

        /// <summary>
        /// Advances the window to the next available index in the array.
        /// </summary>
        private void Advance()
        {
            lock (_windowPositionLock)
            {
                lock (_queuePositionLock)
                {
                    var queueRelative = (_queuePosition - _windowPosition + _queueSize * 1.5) % _queueSize - _queueSize / 2;
                    if (queueRelative <= _windowSize)
                        return; // Do not advance

                    _windowPosition = (_windowPosition + 1) % _queueSize;

                    // Drop items behind window
                    _array.TryRemove((_windowPosition - 1) % _queueSize, out _);

                    // Advance window again if needed
                    queueRelative = (_queuePosition - _windowPosition + _queueSize * 1.5) % _queueSize - _queueSize / 2;
                    if (queueRelative > _windowSize)
                        Advance();
                }
            }
        }
    }
}
