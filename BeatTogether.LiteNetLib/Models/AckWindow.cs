using System.Collections.Generic;

namespace BeatTogether.LiteNetLib.Models
{
    public class AckWindow
    {
        private readonly HashSet<int> _acknowledgements = new();
        private readonly object _AccessLock = new();
        private readonly int _queueSize;
        private readonly int _windowSize;
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
            lock (_AccessLock)
            {
                if (!_acknowledgements.Add(index))
                    return false;

                //Advances to the next position
                var posRelative = (index - _windowPosition + _queueSize * 1.5) % _queueSize - _queueSize / 2;
                if (posRelative < _windowSize)
                    return true; // Do not need to advance
                var targetPosition = (index - _windowSize + 1 + _queueSize) % _queueSize;
                while (_windowPosition != targetPosition)
                {
                    _acknowledgements.Remove(_windowPosition);
                    _windowPosition = (_windowPosition + 1) % _queueSize;
                }
            }
            return true;
        }

        public List<int> GetWindow(out int windowPosition)
        {
            List<int> result = new();
            lock (_AccessLock)
            {
                windowPosition = _windowPosition;
                foreach (var item in _acknowledgements)
                {
                    var rel = (item - windowPosition + _queueSize * 1.5) % _queueSize - _queueSize / 2;
                    if (rel < _windowSize && rel >= 0)
                        result.Add(item % _windowSize);
                }
            }
            return result;

            // x % _windowSize should be (x - _windowPosition) % _queueSize but litenetlib bad
            // refer to AckPacketHandler comment for more information
        }
    }
}
