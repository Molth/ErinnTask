using System;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Provides awaitable timers backed by the existing <see cref="ErinnTimerScheduler" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Each timestamp is an absolute unsigned value in the caller's selected time domain. These
    ///                 methods do not read a clock or calculate a delay.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 All scheduler interactions for a returned timer, including scheduling, update, cancellation,
    ///                 and disposal, must use the scheduler's owning thread. The resulting task may be consumed after
    ///                 completion on another thread.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Cancellation completes the awaitable with a tag and does not create a cancellation exception.
    ///                 Scheduler disposal completes pending timers with <see cref="ErinnTimerTag.Disposed" />. Exceptions
    ///                 thrown by an await continuation follow the scheduler's callback exception policy.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    public static class ErinnTimerSchedulerExtensions
    {
        /// <summary>
        ///     Schedules an awaitable timer to complete at the specified microsecond timestamp.
        /// </summary>
        /// <param name="scheduler">
        ///     The scheduler that owns the timer. It must not be <see langword="null" /> or disposed;
        ///     operations on it must remain on its owning thread.
        /// </param>
        /// <param name="timestamp">
        ///     The absolute timestamp in microseconds. It must use the same time origin and unit as timestamps passed
        ///     to <see cref="ErinnTimerScheduler.Update" />; it is not a delay. Any <see cref="ulong" /> value is valid.
        /// </param>
        /// <param name="cancelHandle">On successful return, receives a handle that can cancel this timer before it completes.</param>
        /// <returns>
        ///     An operation that completes with one of the following tags:
        ///     <list type="bullet">
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Ok" /> when the timer fires.</description>
        ///         </item>
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Canceled" /> when the timer is canceled.</description>
        ///         </item>
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Disposed" /> when the scheduler is disposed.</description>
        ///         </item>
        ///     </list>
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="scheduler" /> is <see langword="null" />.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the scheduler has already been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the scheduler has exhausted all non-zero timer ids.</exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 The timer completes when <see cref="ErinnTimerScheduler.Update" /> receives a timestamp at or
        ///                 past the scheduled value.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 A value of <c>0</c> is scheduled normally and still waits for an update, cancellation, or
        ///                 scheduler disposal.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Await the operation exactly once. Its continuation runs inline on the thread calling
        ///                 <see cref="ErinnTimerScheduler.Update" />, <see cref="ErinnTimerHandle.Cancel" />, or
        ///                 <see cref="ErinnTimerScheduler.Dispose" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Cancellation completes with <see cref="ErinnTimerTag.Canceled" /> and disposal completes with
        ///                 <see cref="ErinnTimerTag.Disposed" />; neither outcome is represented by a thrown cancellation
        ///                 exception.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Once the cached callbacks, routed buckets, per-thread completion-source pool, and scheduler
        ///                 arrays are initialized and have enough capacity, scheduling and successful completion can
        ///                 avoid managed allocations. First-use initialization, pool or array growth, and exception
        ///                 paths can allocate.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public static ErinnTask<ErinnTimerTag> WaitUntilMicroseconds(this ErinnTimerScheduler scheduler, ulong timestamp, out ErinnTimerHandle cancelHandle)
        {
            ThrowHelpers.ThrowIfNull(scheduler, ExceptionArgument.scheduler);

            cancelHandle = default;
            var source = ErinnTaskCompletionSource<ErinnTimerTag>.Rent();
            try
            {
                cancelHandle = scheduler.Create(timestamp, CallbackCache.TimerCompleted, source);
            }
            catch (Exception exception)
            {
                source.ReleaseUnscheduled();
                ThrowHelpers.Throw(exception);
            }

            return new ErinnTask<ErinnTimerTag>(source);
        }

        /// <summary>
        ///     Schedules an awaitable timer at the specified absolute timestamp measured in milliseconds.
        /// </summary>
        /// <param name="scheduler">
        ///     The scheduler that owns the timer. It must not be <see langword="null" /> or disposed;
        ///     operations on it must remain on its owning thread.
        /// </param>
        /// <param name="timestamp">
        ///     The absolute timestamp in milliseconds.
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 It must use the same time origin as timestamps passed to
        ///                 <see cref="ErinnTimerScheduler.Update" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 It must be between <c>0</c> and <c>ulong.MaxValue / 1000</c>, inclusive, so conversion does
        ///                 not overflow.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>The method multiplies it by 1,000 to obtain the microsecond timestamp.</description>
        ///         </item>
        ///     </list>
        /// </param>
        /// <param name="cancelHandle">On successful return, receives a handle that can cancel this timer before it completes.</param>
        /// <returns>
        ///     An operation that completes with one of these tags:
        ///     <list type="bullet">
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Ok" /> when an update reaches the timestamp.</description>
        ///         </item>
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Canceled" /> when the timer is canceled.</description>
        ///         </item>
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Disposed" /> when the scheduler is disposed first.</description>
        ///         </item>
        ///     </list>
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="scheduler" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the converted timestamp is too large.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the scheduler has already been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the scheduler has exhausted all non-zero timer ids.</exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 This is an absolute timestamp, not a delay. The timer completes only when
        ///                 <see cref="ErinnTimerScheduler.Update" /> receives a timestamp equal to or greater than the converted
        ///                 microsecond value.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 A value of <c>0</c> is scheduled normally and still waits for an update, cancellation, or
        ///                 scheduler disposal.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 After validation and conversion, this method directly calls
        ///                 <see cref="WaitUntilMicroseconds(ErinnTimerScheduler, ulong, out ErinnTimerHandle)" /> to create
        ///                 the awaitable timer.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public static ErinnTask<ErinnTimerTag> WaitUntilMilliseconds(this ErinnTimerScheduler scheduler, ulong timestamp, out ErinnTimerHandle cancelHandle)
        {
            ThrowHelpers.ThrowIfNull(scheduler, ExceptionArgument.scheduler);
            ThrowHelpers.ThrowIfTimerMillisecondsOutOfRange(timestamp, ExceptionArgument.timestamp);

            return scheduler.WaitUntilMicroseconds(timestamp * 1000UL, out cancelHandle);
        }

        /// <summary>
        ///     Schedules an awaitable timer at the specified absolute timestamp measured in seconds.
        /// </summary>
        /// <param name="scheduler">
        ///     The scheduler that owns the timer. It must not be <see langword="null" /> or disposed;
        ///     operations on it must remain on its owning thread.
        /// </param>
        /// <param name="timestamp">
        ///     The absolute timestamp in seconds.
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 It must be finite and non-negative, and use the same time origin as timestamps passed to
        ///                 <see cref="ErinnTimerScheduler.Update" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 After conversion, it must be less than <c>2^64</c> microseconds to fit the scheduler's
        ///                 unsigned timestamp.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>Fractional precision below one microsecond is truncated toward zero.</description>
        ///         </item>
        ///     </list>
        /// </param>
        /// <param name="cancelHandle">On successful return, receives a handle that can cancel this timer before it completes.</param>
        /// <returns>
        ///     An operation that completes with one of these tags:
        ///     <list type="bullet">
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Ok" /> when an update reaches the timestamp.</description>
        ///         </item>
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Canceled" /> when the timer is canceled.</description>
        ///         </item>
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Disposed" /> when the scheduler is disposed first.</description>
        ///         </item>
        ///     </list>
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="scheduler" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the timestamp is negative, non-finite, or too large.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the scheduler has already been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the scheduler has exhausted all non-zero timer ids.</exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 This is an absolute timestamp, not a delay. The converted microsecond value is compared
        ///                 directly with timestamps passed to <see cref="ErinnTimerScheduler.Update" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 A value of <c>0</c> is scheduled normally and still waits for an update, cancellation, or
        ///                 scheduler disposal.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 After validation and conversion, this method directly calls
        ///                 <see cref="WaitUntilMicroseconds(ErinnTimerScheduler, ulong, out ErinnTimerHandle)" /> to create
        ///                 the awaitable timer.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public static ErinnTask<ErinnTimerTag> WaitUntilSeconds(this ErinnTimerScheduler scheduler, double timestamp, out ErinnTimerHandle cancelHandle)
        {
            ThrowHelpers.ThrowIfNull(scheduler, ExceptionArgument.scheduler);
            ThrowHelpers.ThrowIfTimerSecondsOutOfRange(timestamp, ExceptionArgument.timestamp);

            return scheduler.WaitUntilMicroseconds((ulong)(timestamp * 1000000d), out cancelHandle);
        }

        /// <summary>
        ///     Holds the timer completion callback shared by all awaitable timers created by this extension type.
        /// </summary>
        private static class CallbackCache
        {
            /// <summary>
            ///     The cached callback that completes the awaitable with its terminal timer tag.
            /// </summary>
            public static readonly Action<ErinnTimerTag, ErinnTaskCompletionSource<ErinnTimerTag>?> TimerCompleted;

            /// <summary>
            ///     Initializes the callback shared by every awaitable timer.
            /// </summary>
            static CallbackCache() => TimerCompleted = CompleteTask;

            /// <summary>
            ///     Completes the timer's task source with the timer completion tag.
            /// </summary>
            /// <param name="tag">The timer completion tag.</param>
            /// <param name="source">The task source associated with the timer.</param>
            private static void CompleteTask(ErinnTimerTag tag, ErinnTaskCompletionSource<ErinnTimerTag>? source) => source!.SetResult(tag);
        }
    }
}