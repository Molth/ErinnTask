using System;
using System.Runtime.CompilerServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Provides helper methods for common collection operations.
    /// </summary>
    internal static class CollectionHelpers
    {
        /// <summary>
        ///     Computes a capacity at least as large as the requested size, normally doubling the current capacity and
        ///     growing by at least four elements. The result is capped at the runtime's single-dimensional array limit
        ///     unless the requested size itself exceeds that limit.
        /// </summary>
        /// <param name="capacity">The current array capacity. It must be a non-negative array length.</param>
        /// <param name="newCapacity">
        ///     The minimum required capacity. This method does not validate it; callers must provide a
        ///     non-negative value. Requests above <see cref="ArrayHelpers.MaxLength" /> are returned unchanged after
        ///     capacity calculation, although such a value cannot be used to allocate an SZ array.
        /// </param>
        /// <returns>
        ///     The greater of the doubled capacity, the current capacity plus four, and
        ///     <paramref name="newCapacity" />, with the first two candidates capped at
        ///     <see cref="ArrayHelpers.MaxLength" />.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GrowCapacity(int capacity, int newCapacity)
        {
            var result = 2 * capacity;
            if ((uint)result > ArrayHelpers.MaxLength)
                result = ArrayHelpers.MaxLength;
            var expected = Math.Min(capacity + 4, ArrayHelpers.MaxLength);
            result = Math.Max(result, expected);
            result = Math.Max(result, newCapacity);
            return result;
        }
    }
}