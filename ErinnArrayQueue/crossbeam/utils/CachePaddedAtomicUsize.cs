using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Erinn.PaddingHelpers;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Stores a native-sized unsigned integer in a cache-line-sized structure.
    /// </summary>
    /// <remarks>
    ///     Cache padding is intended to keep frequently accessed atomic values from sharing cache
    ///     lines. This structure reserves <see cref="CACHE_LINE_SIZE" /> bytes but does not guarantee
    ///     cache-line address alignment.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Size = CACHE_LINE_SIZE)]
    internal struct CachePaddedAtomicUsize
    {
        /// <summary>
        ///     The atomic value stored in this structure.
        /// </summary>
        private UnsafeAtomicUsize _data;

        /// <summary>
        ///     Loads the value using the specified memory ordering.
        /// </summary>
        /// <param name="order">The memory ordering applied to the load.</param>
        /// <returns>The loaded value.</returns>
        /// <exception cref="System.NotSupportedException">
        ///     Thrown when <paramref name="order" /> is <see cref="Ordering.Release" /> or an unsupported value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public nuint Load(Ordering order) => _data.Load(order);

        /// <summary>
        ///     Stores the value using the specified memory ordering.
        /// </summary>
        /// <param name="value">The value to store.</param>
        /// <param name="order">The memory ordering applied to the store.</param>
        /// <exception cref="System.NotSupportedException">
        ///     Thrown when <paramref name="order" /> is <see cref="Ordering.Acquire" /> or an unsupported value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Store(nuint value, Ordering order) => _data.Store(value, order);

        /// <summary>
        ///     Compares the current value with the comparand and, if they are equal, replaces it with the specified value.
        /// </summary>
        /// <param name="value">The value to store if the comparison succeeds.</param>
        /// <param name="comparand">The value to compare with the current value.</param>
        /// <returns>The value held before the comparison.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public nuint CompareExchange(nuint value, nuint comparand) => _data.CompareExchange(value, comparand);
    }
}