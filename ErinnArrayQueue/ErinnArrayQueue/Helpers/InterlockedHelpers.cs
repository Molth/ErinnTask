using System.Runtime.CompilerServices;
using System.Threading;
#if !NET7_0_OR_GREATER
using System;
#endif

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Provides atomic operations for variables that are shared by multiple threads.
    /// </summary>
    internal static class InterlockedHelpers
    {
        /// <summary>
        ///     Returns a native-sized unsigned value, loaded as an atomic operation.
        /// </summary>
        /// <param name="location">The value to read.</param>
        /// <returns>The loaded value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint Read(ref nuint location) => CompareExchange(ref location, default, default);

        /// <summary>
        ///     Sets a native-sized unsigned integer to a specified value and returns the original value, as an atomic operation.
        /// </summary>
        /// <param name="location">The value to update.</param>
        /// <param name="value">The new value.</param>
        /// <returns>The original value of <paramref name="location" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint Exchange(ref nuint location, nuint value)
        {
#if NET7_0_OR_GREATER
            return Interlocked.Exchange(ref location, value);
#else
            return Environment.Is64BitProcess ? (nuint)Interlocked.Exchange(ref Unsafe.As<nuint, long>(ref location), (long)value) : (nuint)Interlocked.Exchange(ref Unsafe.As<nuint, int>(ref location), (int)value);
#endif
        }

        /// <summary>
        ///     Compares two native-sized unsigned integers for equality and, if they are equal, replaces the first one, as an
        ///     atomic operation.
        /// </summary>
        /// <param name="location">The value to update.</param>
        /// <param name="value">The new value.</param>
        /// <param name="comparand">The value to compare.</param>
        /// <returns>The original value of <paramref name="location" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint CompareExchange(ref nuint location, nuint value, nuint comparand)
        {
#if NET7_0_OR_GREATER
            return Interlocked.CompareExchange(ref location, value, comparand);
#else
            return Environment.Is64BitProcess ? (nuint)Interlocked.CompareExchange(ref Unsafe.As<nuint, long>(ref location), (long)value, (long)comparand) : (nuint)Interlocked.CompareExchange(ref Unsafe.As<nuint, int>(ref location), (int)value, (int)comparand);
#endif
        }
    }
}