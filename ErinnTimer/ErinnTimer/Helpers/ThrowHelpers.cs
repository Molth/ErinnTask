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
        ///     Throws an <see cref="ArgumentOutOfRangeException" /> if
        ///     <paramref name="value" /> is classified as negative by the target
        ///     framework's signed-number or comparison check.
        /// </summary>
        /// <param name="value">
        ///     The unmanaged value to validate.
        /// </param>
        /// <param name="paramName">
        ///     The enum value selecting the exception's <c>ParamName</c>.
        ///     Unrecognized values produce a null parameter name.
        /// </param>
        /// <typeparam name="T">
        ///     An unmanaged type that satisfies the target framework's signed-number or
        ///     comparison constraint.
        /// </typeparam>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when the target-specific check considers
        ///     <paramref name="value" /> negative.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 On .NET 7 and later, the generic constraint is
        ///                 <c>ISignedNumber</c>; on older targets, it is
        ///                 <see cref="IComparable{T}" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 On older targets, the value is compared with its default
        ///                 zero value.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 For floating-point types, signed zero and NaN follow the selected
        ///                 check's classification, which can differ from a less-than-zero comparison.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Only value types that satisfy the target-specific constraint are
        ///                 supported.
        ///             </description>
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
        ///     Throws an <see cref="ObjectDisposedException" /> if <paramref name="disposed" /> is
        ///     <see langword="true" />.
        /// </summary>
        /// <param name="disposed">
        ///     A value that indicates whether the object has already been disposed.
        /// </param>
        /// <param name="objectName">The disposed object's name; may be <see langword="null" />.</param>
        /// <exception cref="ObjectDisposedException">
        ///     Thrown when <paramref name="disposed" /> is
        ///     <see langword="true" />.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfDisposed(bool disposed, string? objectName)
        {
            if (disposed)
                throw new ObjectDisposedException(objectName);
        }

        /// <summary>
        ///     Throws an <see cref="InvalidOperationException" />
        ///     if the scheduler has used every non-zero timer uid.
        /// </summary>
        /// <param name="lastUid">
        ///     The most recently assigned timer uid; zero is the initial
        ///     value and all non-maximum values permit another uid.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when <paramref name="lastUid" /> is
        ///     <see cref="ulong.MaxValue" />.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfTimerUidExhausted(ulong lastUid)
        {
            if (lastUid == ulong.MaxValue)
                ThrowInvalidOperationException(ExceptionResource.InvalidOperation_TimerUidExhausted);
        }

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
        ///     Throws an <see cref="AggregateException" /> containing the specified exceptions.
        /// </summary>
        /// <param name="exceptions">
        ///     The non-null sequence of callback or exception-handler failures to include.
        ///     An empty sequence produces an empty <see cref="AggregateException" />.
        /// </param>
        /// <exception cref="AggregateException">
        ///     Always thrown with the supplied exceptions as its inner
        ///     exceptions.
        /// </exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowAggregateException(IEnumerable<Exception> exceptions) => throw new AggregateException(exceptions);

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
            ExceptionArgument.callback => "callback",
            ExceptionArgument.capacity => "capacity",
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
            ExceptionResource.InvalidOperation_TimerUidExhausted => SR.InvalidOperation_TimerUidExhausted,
            ExceptionResource.InvalidOperation_TimerEntryChangedDuringCancellation => SR.InvalidOperation_TimerEntryChangedDuringCancellation,
            _ => null
        };
    }
}