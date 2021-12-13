using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Models
{
    /// <summary>
    /// Designed for handling a "window" of packets inside a larger queue of them.
    /// </summary>
    public class QueueWindow
    {
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _taskQueue = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _dequeueTasks = new();
        private readonly int _queueSize;
        private readonly int _windowSize;
        private object _windowPositionLock = new();
        private int _windowPosition = 0;
        private object _queuePositionLock = new();
        private int _queuePosition = -1;

        /// <summary>
        /// Creates a window queue.
        /// </summary>
        /// <param name="windowSize">Size of the window</param>
        /// <param name="queueSize">Size of the queue to iterate along</param>
        public QueueWindow(int windowSize, int queueSize)
        {
            _queueSize = queueSize;
            _windowSize = windowSize;

            // Complete tasks inside window
            for (int i = 0; i < windowSize; i++)
                _taskQueue.GetOrAdd(i, _ => new()).SetResult();
        }

        /// <summary>
        /// Enqueues a task at the next available index.
        /// </summary>
        /// <param name="index">Index task was queued at</param>
        /// <returns>Queued task</returns>
        public Task Enqueue(out int index)
        {
            lock (_queuePositionLock)
            {
                _queuePosition = _queuePosition + 1 % _queueSize;
                index = _queuePosition;
            }
            return EnqueueAtIndex(index);
        }

        /// <summary>
        /// Enqueues a task at a specified index.
        /// </summary>
        /// <param name="index">Index to queue task at</param>
        /// <returns>Queued task</returns>
        public Task EnqueueAtIndex(int index)
            => _taskQueue.GetOrAdd(index, _ => new()).Task;

        /// <summary>
        /// Provides a task that will be completed when the task at an index is dequeued.
        /// </summary>
        /// <param name="index"></param>
        /// <returns>Task that is completed on dequeue</returns>
        public Task WaitForDequeue(int index)
            => _dequeueTasks.GetOrAdd(index, _ => new()).Task;

        /// <summary>
        /// Dequeues a task at a specified index.
        /// </summary>
        /// <param name="index">Index of task to dequeue</param>
        /// <returns>Whether task was successfully dequeued</returns>
        public bool Dequeue(int index)
        {
            bool dequeued = _taskQueue.TryRemove(index, out _);
            if (_dequeueTasks.TryRemove(index, out TaskCompletionSource dequeueTask))
                dequeueTask.SetResult();
            Advance();
            return dequeued;
        }

        /// <summary>
        /// Checks if there is a task queued at the specified index.
        /// </summary>
        /// <param name="index">The index to check at</param>
        /// <returns>Whether a task is queued at index</returns>
        public bool IsIndexQueued(int index)
            => _taskQueue.ContainsKey(index);

        /// <summary>
        /// Advances the window to the next available index in the queue.
        /// </summary>
        private void Advance()
        {
            lock (_windowPositionLock)
            {
                if (_taskQueue.ContainsKey(_windowPosition))
                    return; // Should not advance
                _windowPosition = (_windowPosition + 1) % _queueSize;

                // Complete tasks that have entered the window
                var newestIndex = (_windowPosition + _windowSize - 1) % _queueSize;
                _taskQueue.GetOrAdd(newestIndex, _ => new()).SetResult();

                // Advance window again if current index has been handled
                if (!_taskQueue.ContainsKey(_windowPosition))
                    Advance();
            }
        }
    }
}
