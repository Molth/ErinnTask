using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
#if NET7_0_OR_GREATER
using System.Numerics;
#endif

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Provides helper methods for validating arguments and
    ///     throwing standard exceptions with consistent messaging.
    /// </summary>
    internal static class ThrowHelpers
    {
        /// <summary>
        ///     Throws an <see cref="ArgumentOutOfRangeException" /> if <paramref name="value" /> compares as negative
        ///     under the target framework's signed-number or comparison contract.
        /// </summary>
        /// <param name="value">The signed numeric value to validate; zero and positive values are accepted.</param>
        /// <param name="paramName">
        ///     The enum value mapped to the exception's <c>ParamName</c>; unrecognized values produce a null
        ///     parameter name.
        /// </param>
        /// <typeparam name="T">An unmanaged type that satisfies the target framework's signed-number or comparison constraint.</typeparam>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value" /> is negative.</exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 On .NET 7 and later, the generic constraint is <c>ISignedNumber</c>; on older targets, it is
        ///                 <see cref="IComparable{T}" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>On older targets, the value is compared with its default zero value.</description>
        ///         </item>
        ///         <item>
        ///             <description>Only value types that satisfy the target-specific constraint are supported.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNegative<T>(T value, ExceptionArgument paramName) where T : unmanaged,
#if NET7_0_OR_GREATER
            ISignedNumber<T>
#else
            IComparable<T>
#endif
        {
#if NET7_0_OR_GREATER
            if (T.IsNegative(value))
#else
            if (value.CompareTo(default) < 0)
#endif
                throw new ArgumentOutOfRangeException(GetArgumentName(paramName), value, SR.ArgumentOutOfRange_MustBeNonNegative);
        }

        /// <summary>
        ///     Throws an <see cref="ArgumentOutOfRangeException" /> if a millisecond timestamp cannot be
        ///     represented as an unsigned microsecond timestamp.
        /// </summary>
        /// <param name="milliseconds">
        ///     The unsigned absolute timestamp in milliseconds to validate.
        /// </param>
        /// <param name="paramName">
        ///     The enum value mapped to the exception's <c>ParamName</c>. An unrecognized value produces a null
        ///     parameter name.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="milliseconds" /> is greater than
        ///     <c>ulong.MaxValue / 1000</c>.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>Values from zero through <c>ulong.MaxValue / 1000</c>, inclusive, are accepted.</description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 This bound ensures that a subsequent multiplication by 1,000 to produce microseconds does not overflow
        ///                 <see cref="ulong" />.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfTimerMillisecondsOutOfRange(ulong milliseconds, ExceptionArgument paramName)
        {
            if (milliseconds > ulong.MaxValue / 1000UL)
                throw new ArgumentOutOfRangeException(GetArgumentName(paramName), milliseconds, SR.ArgumentOutOfRange_TimerTimestampOutOfRange);
        }

        /// <summary>
        ///     Throws an <see cref="ArgumentOutOfRangeException" /> if a seconds timestamp cannot be represented
        ///     as an unsigned microsecond timestamp.
        /// </summary>
        /// <param name="seconds">
        ///     The absolute timestamp in seconds to validate.
        /// </param>
        /// <param name="paramName">
        ///     The enum value mapped to the exception's <c>ParamName</c>. An unrecognized value produces a null
        ///     parameter name.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="seconds" /> is negative or non-finite, or when its converted microsecond
        ///     value is greater than or equal to 2^64.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>The value must be finite and non-negative.</description>
        ///         </item>
        ///         <item>
        ///             <description>Multiplying by 1,000,000 as a <see cref="double" /> must produce a value less than 2^64.</description>
        ///         </item>
        ///         <item>
        ///             <description>Conversion to microseconds truncates any fractional microsecond.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfTimerSecondsOutOfRange(double seconds, ExceptionArgument paramName)
        {
            var microseconds = seconds * 1000000d;
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d || microseconds >= 18446744073709551616d)
                throw new ArgumentOutOfRangeException(GetArgumentName(paramName), seconds, SR.ArgumentOutOfRange_TimerTimestampOutOfRange);
        }

        /// <summary>
        ///     Throws an <see cref="InvalidOperationException" /> if the scheduler has used every non-zero timer uid.
        /// </summary>
        /// <param name="lastUid">
        ///     The most recently assigned timer uid;
        ///     zero is the initial value and all non-maximum values permit another uid.
        /// </param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="lastUid" /> is <see cref="ulong.MaxValue" />.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfTimerUidExhausted(ulong lastUid)
        {
            if (lastUid == ulong.MaxValue)
                ThrowInvalidOperation(SR.InvalidOperation_TimerUidExhausted);
        }

        /// <summary>
        ///     Throws an <see cref="InvalidOperationException" /> if a pooled task source has used every non-zero version.
        /// </summary>
        /// <param name="lastVersion">
        ///     The most recently assigned source version; zero is the initial value and all non-maximum
        ///     values permit another version.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when <paramref name="lastVersion" /> is
        ///     <see cref="ulong.MaxValue" />.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfTaskVersionExhausted(ulong lastVersion)
        {
            if (lastVersion == ulong.MaxValue)
                ThrowInvalidOperation(SR.InvalidOperation_TaskVersionExhausted);
        }

        /// <summary>
        ///     Throws an <see cref="ArgumentNullException" /> if <paramref name="argument" /> is null.
        /// </summary>
        /// <param name="argument">The reference type argument to validate as non-null.</param>
        /// <param name="paramName">
        ///     The enum value mapped to the exception's <c>ParamName</c>; unrecognized values produce a null
        ///     parameter name.
        /// </param>
        /// <typeparam name="T">A reference type; value types are not accepted.</typeparam>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="argument" /> is null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNull<T>(T? argument, ExceptionArgument paramName) where T : class
        {
            if (argument == null)
                throw new ArgumentNullException(GetArgumentName(paramName), SR.ArgumentNull_MustBeNotNull);
        }

        /// <summary>
        ///     Throws an <see cref="ObjectDisposedException" /> if <paramref name="disposed" /> is
        ///     <see langword="true" />.
        /// </summary>
        /// <param name="disposed">A value that indicates whether the object has already been disposed.</param>
        /// <param name="objectName">The disposed object's name; may be <see langword="null" />.</param>
        /// <exception cref="ObjectDisposedException">Thrown when <paramref name="disposed" /> is <see langword="true" />.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfDisposed(bool disposed, string? objectName)
        {
            if (disposed)
                throw new ObjectDisposedException(objectName);
        }

        /// <summary>
        ///     Rethrows the specified exception through <see cref="ExceptionDispatchInfo" /> to preserve its existing
        ///     stack trace. The call does not return.
        /// </summary>
        /// <param name="exception">The non-null exception instance to rethrow.</param>
        /// <exception cref="Exception">Rethrows the supplied exception instance.</exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Throw(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();

        /// <summary>
        ///     Throws an <see cref="InvalidOperationException" /> with the specified resource message.
        /// </summary>
        /// <param name="message">The message used to construct the exception; null is permitted by the framework constructor.</param>
        /// <exception cref="InvalidOperationException">Always thrown with the supplied message.</exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperation(string message) => throw new InvalidOperationException(message);

        /// <summary>
        ///     Rethrows an already captured exception through its stored dispatch information. The call does not return.
        /// </summary>
        /// <param name="exception">The non-null captured exception dispatch information.</param>
        /// <exception cref="Exception">Rethrows the exception stored in the dispatch information.</exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Throw(ExceptionDispatchInfo exception) => exception.Throw();

        /// <summary>
        ///     Throws an <see cref="AggregateException" /> containing the specified exceptions.
        /// </summary>
        /// <param name="exceptions">
        ///     The non-null sequence of callback or exception-handler failures to include. An empty sequence
        ///     produces an empty <see cref="AggregateException" />.
        /// </param>
        /// <exception cref="AggregateException">Always thrown with the supplied exceptions as its inner exceptions.</exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowAggregate(IEnumerable<Exception> exceptions) => throw new AggregateException(exceptions);

        /// <summary>
        ///     Maps a recognized <see cref="ExceptionArgument" /> value to its C# parameter name; unrecognized values
        ///     map to <see langword="null" />.
        /// </summary>
        /// <param name="argument">The <see cref="ExceptionArgument" /> value to convert.</param>
        /// <returns>The matching parameter name, or <see langword="null" /> when <paramref name="argument" /> is unrecognized.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string? GetArgumentName(ExceptionArgument argument) => argument switch
        {
            ExceptionArgument.callback => "callback",
            ExceptionArgument.capacity => "capacity",
            ExceptionArgument.continuation => "continuation",
            ExceptionArgument.scheduler => "scheduler",
            ExceptionArgument.timestamp => "timestamp",
            _ => null
        };
    }
}