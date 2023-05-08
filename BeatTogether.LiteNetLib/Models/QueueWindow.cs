using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace BeatTogether.LiteNetLib.Models
{
    /// <summary>
    /// Designed for handling a "window" of packets inside a larger queue of them.
    /// </summary>
    public class QueueWindow
    {
        //private readonly Dictionary<int, TaskCompletionSource> _taskQueue = new();
        private readonly HashSet<int> _queue = new HashSet<int>();
        private readonly object _taskQueueLock = new();
        private readonly Dictionary<int, TaskCompletionSource> _dequeueTasks = new();
        private readonly object _taskDeQueueLock = new();
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
            {
                TaskCompletionSource task = new();
                task.SetResult();
                lock (_taskQueueLock)
                {
                    _queue.Add(i);
                }
            }
        }

        /// <summary>
        /// Enqueues a task at the next available index.
        /// </summary>
        /// <param name="index">Index task was queued at</param>
        public void Enqueue(out int index)
        {
            lock (_queuePositionLock)
            {
                _queuePosition = (_queuePosition + 1) % _queueSize;
                index = _queuePosition;
            }
            //return EnqueueAtIndex(index);
            EnqueueAtIndex(index);
        }

        /// <summary>
        /// Enqueues a task at a specified index.
        /// </summary>
        /// <param name="index">Index to queue task at</param>
        public void EnqueueAtIndex(int index)
        {
            lock (_taskQueueLock)
            {
                _queue.Add(index);
            }
        }

        /// <summary>
        /// Provides a task that will be completed when the task at an index is dequeued.
        /// </summary>
        /// <param name="index"></param>
        /// <returns>Task that is completed on dequeue</returns>
        public Task WaitForDequeue(int index)
        {
            lock (_windowPositionLock)
            {
                if ((index - _windowPosition + _queueSize + _queueSize / 2) % _queueSize - _queueSize / 2 < 0)
                    return Task.CompletedTask;
            }
            lock (_taskDeQueueLock)
            {
                if (!_dequeueTasks.TryGetValue(index, out var task))
                {
                    _dequeueTasks.Add(index, task = new());
                }
                return task.Task;
            }
        }

        /// <summary>
        /// Dequeues a task at a specified index.
        /// </summary>
        /// <param name="index">Index of task to dequeue</param>
        /// <returns>Whether task was successfully dequeued</returns>
        public bool Dequeue(int index)
        {
            bool dequeued;
            lock (_taskQueueLock)
            {
                dequeued = _queue.Remove(index);
            }
            lock (_taskDeQueueLock)
            {
                if (_dequeueTasks.Remove(index, out var dequeueTask))
                    dequeueTask.SetResult();
            }
            Advance();
            return dequeued;
        }

        /// <summary>
        /// Checks if there is a task queued at the specified index.
        /// </summary>
        /// <param name="index">The index to check at</param>
        /// <returns>Whether a task is queued at index</returns>
        public bool IsIndexQueued(int index)
        {
            lock (_taskQueueLock)
            {
                return _queue.Contains(index);
            }
        }

        /// <summary>
        /// Advances the window to the next available index in the queue.
        /// </summary>
        private void Advance()
        {
            lock (_windowPositionLock)
            {
                if (IsIndexQueued(_windowPosition))
                    return; // Should not advance
                _windowPosition = (_windowPosition + 1) % _queueSize;

                // Complete tasks that have entered the window
                var newestIndex = (_windowPosition + _windowSize - 1) % _queueSize;
                lock (_taskQueueLock)
                {
                    _queue.Add(newestIndex);
                }
                // Advance window again if current index has been handled
                if (!IsIndexQueued(_windowPosition))
                    Advance();
            }
        }
    }
}
