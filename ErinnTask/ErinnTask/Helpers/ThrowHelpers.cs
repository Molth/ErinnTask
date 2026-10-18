using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

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
        ///     Throws an <see cref="ArgumentNullException" /> if <paramref name="argument" /> is null.
        /// </summary>
        /// <param name="argument">The reference type argument to validate as non-null.</param>
        /// <param name="paramName">
        ///     The enum value selecting the exception's <c>ParamName</c>.
        ///     Unrecognized values produce a null parameter name.
        /// </param>
        /// <typeparam name="T">A reference type; value types are not accepted.</typeparam>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when <paramref name="argument" /> is null.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNull<T>(T? argument, ExceptionArgument paramName) where T : class
        {
            if (argument == null)
                throw new ArgumentNullException(GetArgumentName(paramName), SR.ArgumentNull_MustBeNotNull);
        }

        /// <summary>
        ///     Throws an <see cref="ArgumentOutOfRangeException" /> if a millisecond
        ///     timestamp cannot be represented as an unsigned microsecond timestamp.
        /// </summary>
        /// <param name="milliseconds">
        ///     The unsigned absolute timestamp in milliseconds to validate.
        /// </param>
        /// <param name="paramName">
        ///     The enum value selecting the exception's <c>ParamName</c>.
        ///     An unrecognized value produces a null parameter name.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="milliseconds" /> is greater than
        ///     <c>18446744073709551UL</c>.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 Values from zero through <c>18446744073709551UL</c>, inclusive, are
        ///                 accepted.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 The upper bound times 1,000 is <c>18446744073709551000UL</c> and fits in
        ///                 <see cref="ulong" />. The next millisecond value would exceed
        ///                 <see cref="ulong.MaxValue" /> after conversion to microseconds.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfTimerMillisecondsOutOfRange(ulong milliseconds, ExceptionArgument paramName)
        {
            if (milliseconds > 18446744073709551UL)
                throw new ArgumentOutOfRangeException(GetArgumentName(paramName), milliseconds, SR.ArgumentOutOfRange_TimerTimestampOutOfRange);
        }

        /// <summary>
        ///     Throws when a seconds timestamp cannot be converted to an unsigned microsecond timestamp.
        /// </summary>
        /// <param name="seconds">
        ///     The absolute timestamp in seconds to validate.
        /// </param>
        /// <param name="paramName">
        ///     The enum value selecting the exception's <c>ParamName</c>.
        ///     An unrecognized value produces a null parameter name.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="seconds" /> is negative or non-finite, or when
        ///     conversion to a microsecond timestamp would exceed <see cref="ulong.MaxValue" />.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 The value must be finite and numerically non-negative.
        ///                 Negative zero is accepted as zero.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Values below <c>18446744073709.55d</c> seconds are accepted. At that boundary,
        ///                 multiplying by 1,000,000 as a <see cref="double" /> reaches 2^64.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Conversion to microseconds truncates any fractional microsecond.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfTimerSecondsOutOfRange(double seconds, ExceptionArgument paramName)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d || seconds >= 18446744073709.55d)
                throw new ArgumentOutOfRangeException(GetArgumentName(paramName), seconds, SR.ArgumentOutOfRange_TimerTimestampOutOfRange);
        }

        /// <summary>
        ///     Throws an <see cref="InvalidOperationException" /> if a pooled
        ///     task source has used every non-zero version.
        /// </summary>
        /// <param name="lastVersion">
        ///     The most recently assigned source version. Zero is the initial value;
        ///     any value below <see cref="ulong.MaxValue" /> permits another version.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when <paramref name="lastVersion" /> is
        ///     <see cref="ulong.MaxValue" />.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfTaskVersionExhausted(ulong lastVersion)
        {
            if (lastVersion == ulong.MaxValue)
                ThrowInvalidOperationException(ExceptionResource.InvalidOperation_TaskVersionExhausted);
        }

        /// <summary>
        ///     Throws an <see cref="InvalidOperationException" /> with the message identified by
        ///     <paramref name="resource" />.
        /// </summary>
        /// <param name="resource">The <see cref="ExceptionResource" /> value identifying the message.</param>
        /// <exception cref="InvalidOperationException">
        ///     Always thrown. An unrecognized value passes a null message to the constructor,
        ///     so <see cref="Exception.Message" /> uses the framework's default message.
        /// </exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException(ExceptionResource resource) => throw new InvalidOperationException(GetResourceString(resource));

        /// <summary>
        ///     Rethrows the specified exception through <see cref="ExceptionDispatchInfo" />,
        ///     preserving its existing stack trace. The call does not return.
        /// </summary>
        /// <param name="exception">The non-null exception instance to rethrow.</param>
        /// <exception cref="Exception">Rethrows the supplied exception instance.</exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RethrowException(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();

        /// <summary>
        ///     Rethrows an already captured exception through its
        ///     stored dispatch information. The call does not return.
        /// </summary>
        /// <param name="dispatchInfo">The non-null captured exception dispatch information.</param>
        /// <exception cref="Exception">Rethrows the exception stored in the dispatch information.</exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RethrowDispatchInfo(ExceptionDispatchInfo dispatchInfo) => dispatchInfo.Throw();

        /// <summary>
        ///     Maps a recognized <see cref="ExceptionArgument" /> value to its C# parameter
        ///     name. An unrecognized value maps to <see langword="null" />.
        /// </summary>
        /// <param name="argument">The <see cref="ExceptionArgument" /> value to convert.</param>
        /// <returns>
        ///     The mapped C# parameter name, or <see langword="null" />
        ///     when the enum value is unrecognized.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string? GetArgumentName(ExceptionArgument argument) => argument switch
        {
            ExceptionArgument.continuation => "continuation",
            ExceptionArgument.scheduler => "scheduler",
            ExceptionArgument.timestamp => "timestamp",
            _ => null
        };

        /// <summary>
        ///     Returns the resource string associated with the specified
        ///     <see cref="ExceptionResource" /> value.
        /// </summary>
        /// <param name="resource">The <see cref="ExceptionResource" /> value to convert.</param>
        /// <returns>
        ///     The matching message, or <see langword="null" /> for an unrecognized value.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string? GetResourceString(ExceptionResource resource) => resource switch
        {
            ExceptionResource.InvalidOperation_TaskVersionExhausted => SR.InvalidOperation_TaskVersionExhausted,
            ExceptionResource.InvalidOperation_TaskAlreadyHasContinuation => SR.InvalidOperation_TaskAlreadyHasContinuation,
            ExceptionResource.InvalidOperation_TaskConsumedOrReused => SR.InvalidOperation_TaskConsumedOrReused,
            ExceptionResource.InvalidOperation_TaskNoLongerPending => SR.InvalidOperation_TaskNoLongerPending,
            ExceptionResource.InvalidOperation_TaskNotCompleted => SR.InvalidOperation_TaskNotCompleted,
            _ => null
        };
    }
}