using System;
using System.Runtime.CompilerServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Stores typed callback entries, reusable slot indices,
    ///     and timer-queue positions for one routed payload slot type.
    /// </summary>
    /// <typeparam name="TSlot">The type of the storage slot the payload is routed through.</typeparam>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 The bucket is owned by one scheduler and must only be accessed on that
    ///                 scheduler's thread.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>Concurrent operations from multiple threads are unsupported.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 The scheduler removes heap nodes; this bucket
    ///                 maintains the slot-to-index mapping used for cancellation.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    internal sealed class ErinnTimerBucket<TSlot> : IErinnTimerBucket
    {
        /// <summary>
        ///     The typed callback and payload entries indexed by reusable slot index.
        /// </summary>
        private ErinnTimerEntry<TSlot>[] _entries;

        /// <summary>
        ///     The heap position of each allocated entry, indexed by bucket slot index.
        /// </summary>
        private int[] _queueIndices;

        /// <summary>
        ///     The array-backed stack of released slot indices.
        /// </summary>
        private int[] _freeSlotIndices;

        /// <summary>
        ///     The number of allocated slot indices, including released slot indices available for reuse.
        /// </summary>
        private int _count;

        /// <summary>
        ///     The number of released slot indices currently stored in <see cref="_freeSlotIndices" />.
        /// </summary>
        private int _freeCount;

        /// <summary>
        ///     Initializes callback and slot metadata arrays with the specified capacity.
        /// </summary>
        /// <param name="capacity">
        ///     The non-negative initial length of all three backing arrays. Zero is accepted and grows on the
        ///     first allocation; a negative value fails during array allocation.
        /// </param>
        /// <exception cref="OverflowException">
        ///     Thrown if <paramref name="capacity" /> is
        ///     negative and the runtime rejects the array length.
        /// </exception>
        public ErinnTimerBucket(int capacity)
        {
            _entries = new ErinnTimerEntry<TSlot>[capacity];
            _queueIndices = new int[capacity];
            _freeSlotIndices = new int[capacity];
            _count = 0;
            _freeCount = 0;
        }

        /// <summary>
        ///     Drops callback and slot metadata so retained cancellation handles do not keep the
        ///     backing arrays alive after disposal. The bucket cannot be used again after this call.
        /// </summary>
        [ThreadAffine]
        public void Dispose()
        {
            _entries = null!;
            _queueIndices = null!;
            _freeSlotIndices = null!;
        }

        /// <summary>
        ///     Gets the queue position for a live entry whose slot and timer uid match. Returns
        ///     false for a zero uid, an out-of-range slot, or a slot containing another timer uid.
        /// </summary>
        /// <param name="uid">The non-zero timer uid expected for the entry.</param>
        /// <param name="slotIndex">
        ///     The slot index to validate against <paramref name="uid" />;
        ///     it must identify an allocated slot to match.
        /// </param>
        /// <param name="queueIndex">
        ///     When this method returns <see langword="true" />,
        ///     contains the entry's current heap index; otherwise, contains zero.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if the entry is live and its uid matches
        ///     <paramref name="uid" />; otherwise
        ///     <see langword="false" />.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ThreadAffine]
        public bool TryGetQueueIndex(ulong uid, int slotIndex, out int queueIndex)
        {
            if (uid == 0 || (uint)slotIndex >= (uint)_count || _entries[slotIndex].Uid != uid)
            {
                queueIndex = 0;
                return false;
            }

            queueIndex = _queueIndices[slotIndex];
            return true;
        }

        /// <summary>
        ///     Records the heap position of an active slot after a queue insertion or move.
        /// </summary>
        /// <param name="slotIndex">The active, allocated bucket slot index.</param>
        /// <param name="index">
        ///     The current heap position of that slot, in the range from zero to the
        ///     number of queued timers minus one. The index is stored without validation.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ThreadAffine]
        public void SetQueueIndex(int slotIndex, int index) => _queueIndices[slotIndex] = index;

        /// <summary>
        ///     Attempts to complete a matching live entry, releasing its slot before invoking the callback.
        /// </summary>
        /// <param name="uid">
        ///     The non-zero timer uid expected for the entry currently occupying
        ///     <paramref name="slotIndex" />.
        /// </param>
        /// <param name="slotIndex">
        ///     The slot index to validate against <paramref name="uid" />;
        ///     it must identify an allocated slot to match.
        /// </param>
        /// <param name="tag">The defined completion reason to pass to the callback.</param>
        /// <param name="exception">
        ///     When the matching callback throws, contains that exception;
        ///     otherwise contains null.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if the slot and uid matched and the callback
        ///     was invoked, even if it threw;
        ///     otherwise <see langword="false" />.
        /// </returns>
        [ThreadAffine]
        public bool TryComplete(ulong uid, int slotIndex, ErinnTimerTag tag, out Exception? exception)
        {
            exception = null;

            if (uid == 0 || (uint)slotIndex >= (uint)_count)
                return false;

            var entry = _entries[slotIndex];
            if (entry.Uid != uid)
                return false;

            _entries[slotIndex] = default;
            _freeSlotIndices[_freeCount++] = slotIndex;

            try
            {
                entry.Invoke(tag);
            }
            catch (Exception error)
            {
                exception = error;
            }

            return true;
        }

        /// <summary>
        ///     Allocates or reuses a slot and stores the routed payload and its typed invoker, growing
        ///     the backing arrays when no reusable slot is available and the allocated range is full.
        /// </summary>
        /// <param name="uid">
        ///     The non-zero timer uid assigned by the owning scheduler. It must not be in use by
        ///     another live entry.
        /// </param>
        /// <param name="callback">
        ///     The non-null user callback to retain until the timer completes; null is
        ///     not checked here.
        /// </param>
        /// <param name="data">
        ///     The payload whose representation is compatible with <typeparamref name="TSlot" />;
        ///     compatibility is a caller precondition and is not checked here.
        /// </param>
        /// <typeparam name="T">The callback parameter type.</typeparam>
        /// <returns>
        ///     The allocated slot index, which is non-negative and remains associated with this entry until
        ///     it is completed.
        /// </returns>
        [ThreadAffine]
        public int Allocate<T>(ulong uid, Action<ErinnTimerTag, T?> callback, T? data)
        {
            var entry = ErinnTimerEntry<TSlot>.Create(uid, callback, data);
            var freeCount = _freeCount;
            int slotIndex;
            if (freeCount != 0)
            {
                slotIndex = _freeSlotIndices[freeCount - 1];
                _freeCount = freeCount - 1;
            }
            else
            {
                slotIndex = _count;
                if (slotIndex == _entries.Length)
                    Grow(slotIndex + 1);

                _count = slotIndex + 1;
            }

            _entries[slotIndex] = entry;
            return slotIndex;
        }

        /// <summary>
        ///     Grows callback storage and slot metadata while preserving active
        ///     entries and heap positions. This is called only when no free
        ///     slot is available, so the new free-slot-index array starts empty.
        /// </summary>
        /// <param name="minimumCapacity">The capacity required to allocate the next slot.</param>
        [ThreadAffine]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int minimumCapacity)
        {
            var oldCount = _count;
            var capacity = CollectionHelpers.GrowCapacity(_entries.Length, minimumCapacity);
            var entries = new ErinnTimerEntry<TSlot>[capacity];
            var queueIndices = new int[capacity];
            var freeSlotIndices = new int[capacity];
            Array.Copy(_entries, entries, oldCount);
            Array.Copy(_queueIndices, queueIndices, oldCount);
            _entries = entries;
            _queueIndices = queueIndices;
            _freeSlotIndices = freeSlotIndices;
        }
    }
}