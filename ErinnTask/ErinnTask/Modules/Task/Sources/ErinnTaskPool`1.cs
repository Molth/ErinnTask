using System;
using System.Runtime.CompilerServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Stores available sources in a per-thread array-backed stack without enumeration state.
    /// </summary>
    /// <typeparam name="T">The type of the pooled source.</typeparam>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Each stack instance is accessed only by its owning thread; pooled items may move between
    ///                 threads.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>An empty pop returns <see langword="false" /> without changing the count.</description>
    ///         </item>
    ///         <item>
    ///             <description>Capacity grows only on push when all slots are occupied.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Popped references are cleared immediately so the pool does not keep a second reference to a
    ///                 rented object.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    internal sealed class ErinnTaskPool<T> where T : class
    {
        /// <summary>
        ///     The available sources, stored in return order.
        /// </summary>
        private T?[] _items;

        /// <summary>
        ///     The number of available sources.
        /// </summary>
        private int _count;

        /// <summary>
        ///     Initializes storage for the sources owned by one thread.
        /// </summary>
        public ErinnTaskPool() => _items = new T?[4];

        /// <summary>
        ///     Removes the most recently returned source, if one is available.
        /// </summary>
        /// <param name="item">When this method returns, contains the removed source if available; otherwise it is null.</param>
        /// <returns><see langword="true" /> if a source was removed; otherwise <see langword="false" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ThreadAffine]
        public bool TryPop(out T item)
        {
            var count = _count;
            if (count == 0)
            {
                item = null!;
                return false;
            }

            var items = _items;
            var index = count - 1;
            item = items[index]!;
            items[index] = null;
            _count = index;
            return true;
        }

        /// <summary>
        ///     Adds a released source to the stack, growing its storage only when it is full. The same item must not be
        ///     pushed more than once while it is available for renting.
        /// </summary>
        /// <param name="item">The non-null released source to make available.</param>
        /// <remarks>This pool is unsynchronized and must be accessed only by its owning thread.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ThreadAffine]
        public void Push(T item)
        {
            var count = _count;
            if (count == _items.Length)
                Grow();

            _items[count] = item;
            _count = count + 1;
        }

        /// <summary>
        ///     Grows storage while preserving the available sources' return order.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [ThreadAffine]
        private void Grow()
        {
            var items = new T?[CollectionHelpers.GrowCapacity(_items.Length, _count + 1)];
            Array.Copy(_items, items, _count);
            _items = items;
        }
    }
}