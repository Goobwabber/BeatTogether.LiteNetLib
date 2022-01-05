using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BeatTogether.LiteNetLib.Models
{
    public class ConcurrentHashSet<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> _hashset = new();

        /// <summary>
        /// Gets a collection containing the values in the <see cref="ConcurrentHashSet{T}"/>.
        /// </summary>
        /// <returns>A collection of values in the <see cref="ConcurrentHashSet{T}"/></returns>
        public ICollection<T> Values => _hashset.Keys;

        /// <summary>
        /// Attempts to add the specified value to the <see cref="ConcurrentHashSet{T}"/>.
        /// </summary>
        /// <param name="value">The value of the element to add.</param>
        /// <returns>true if the value was added to the <see cref="ConcurrentHashSet{T}"/> successfully; false if the key already exists</returns>
        /// <exception cref="System.ArgumentNullException">value is null.</exception>
        /// <exception cref="System.OverflowException">The hashet contains too many elements.</exception>
        public bool TryAdd(T value)
            => _hashset.TryAdd(value, 0);

        /// <summary>
        /// Attempts to remove the specified value from the <see cref="ConcurrentHashSet{T}"/>.
        /// </summary>
        /// <param name="value">The value of the element to remove.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">value is null.</exception>
        public bool TryRemove(T value)
            => _hashset.TryRemove(value, out _);
    }
}
