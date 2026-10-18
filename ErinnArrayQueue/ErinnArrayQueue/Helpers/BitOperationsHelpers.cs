using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using System.Numerics;
#endif

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Utility methods for intrinsic bit-twiddling operations.
    /// </summary>
    internal static class BitOperationsHelpers
    {
        /// <summary>
        ///     Round the given integral value up to a power of 2.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        ///     The smallest power of 2 which is greater than or equal to <paramref name="value" />.
        ///     If <paramref name="value" /> is 0 or the result overflows, returns 0.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RoundUpToPowerOf2(uint value)
        {
#if NET6_0_OR_GREATER
            return BitOperations.RoundUpToPowerOf2(value);
#else
            --value;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1U;
#endif
        }
    }
}