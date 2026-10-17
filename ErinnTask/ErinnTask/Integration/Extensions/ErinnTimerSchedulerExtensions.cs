using System;
using System.Runtime.CompilerServices;

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
    ///                 Timestamps are absolute unsigned values in the caller's chosen time domain. This method does not
    ///                 read a clock, convert units, or calculate a delay.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Scheduling, updating, cancellation, and disposal must use the scheduler's owning thread. A
    ///                 continuation registered before completion runs inline on the thread that completes the timer. Once
    ///                 completion has occurred, consuming the result does not access the scheduler and may happen on
    ///                 another thread.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Cancellation and scheduler disposal complete the awaitable with result tags; they do not create
    ///                 cancellation exceptions. Exceptions thrown by a continuation invoked during completion follow
    ///                 the scheduler's callback exception policy.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    public static class ErinnTimerSchedulerExtensions
    {
        /// <summary>
        ///     Returns a non-owning view that expresses awaitable timer timestamps in microseconds.
        /// </summary>
        /// <param name="scheduler">The scheduler that owns the timers.</param>
        /// <returns>A view over <paramref name="scheduler" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="scheduler" /> is <see langword="null" />.</exception>
        /// <remarks>
        ///     The view does not create, own, or dispose a scheduler. Its timer operations must remain on the scheduler's
        ///     owning thread.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ErinnTimerSchedulerMicrosecondsView InMicroseconds(this ErinnTimerScheduler scheduler)
        {
            ThrowHelpers.ThrowIfNull(scheduler, ExceptionArgument.scheduler);

            return new ErinnTimerSchedulerMicrosecondsView(scheduler);
        }

        /// <summary>
        ///     Schedules an awaitable timer at the specified absolute timestamp.
        /// </summary>
        /// <param name="scheduler">The scheduler that owns the timer.</param>
        /// <param name="timestamp">
        ///     The absolute timestamp. Use the same time domain and unit as later calls to
        ///     <see cref="ErinnTimerScheduler.Update" />. Every <see cref="ulong" /> value is accepted; this value is
        ///     not interpreted as a delay.
        /// </param>
        /// <param name="handle">
        ///     On successful return, receives a handle that can cancel this timer while it is pending.
        /// </param>
        /// <returns>
        ///     An operation that completes with <see cref="ErinnTimerTag.Ok" /> when the timer fires,
        ///     <see cref="ErinnTimerTag.Canceled" /> when it is canceled, or
        ///     <see cref="ErinnTimerTag.Disposed" /> if the scheduler is disposed while it is pending.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="scheduler" /> is <see langword="null" />.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the scheduler has already been disposed.</exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the scheduler has exhausted its timer uids or the pooled completion source cannot be reused.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 A timestamp of <c>0</c> is queued normally and completes only after an update reaches it,
        ///                 cancellation, or scheduler disposal.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>Await the returned operation exactly once.</description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Once callbacks, routed buckets, the per-thread completion-source pool, and scheduler arrays
        ///                 are initialized and have sufficient capacity, scheduling and successful completion can avoid
        ///                 managed allocations. First-use initialization, pool or array growth, and exception paths can
        ///                 allocate.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public static ErinnTask<ErinnTimerTag> WaitUntil(this ErinnTimerScheduler scheduler, ulong timestamp, out ErinnTimerHandle handle)
        {
            ThrowHelpers.ThrowIfNull(scheduler, ExceptionArgument.scheduler);

            handle = default;
            var source = ErinnTaskCompletionSource<ErinnTimerTag>.Rent();
            try
            {
                handle = scheduler.Schedule(timestamp, CallbackCache.TimerCompleted, source);
            }
            catch (Exception exception)
            {
                source.ReleaseUnscheduled();
                ThrowHelpers.Throw(exception);
            }

            return new ErinnTask<ErinnTimerTag>(source);
        }

        /// <summary>
        ///     Holds the timer completion callback shared by all awaitable timers created by this extension type.
        /// </summary>
        private static class CallbackCache
        {
            /// <summary>
            ///     The cached callback that completes the awaitable with the timer's terminal tag.
            /// </summary>
            public static readonly Action<ErinnTimerTag, ErinnTaskCompletionSource<ErinnTimerTag>?> TimerCompleted;

            /// <summary>
            ///     Initializes the callback shared by every awaitable timer.
            /// </summary>
            static CallbackCache() => TimerCompleted = CompleteTask;

            /// <summary>
            ///     Completes the timer's task source with its completion tag.
            /// </summary>
            /// <param name="tag">The timer completion tag.</param>
            /// <param name="source">The task source associated with the timer.</param>
            private static void CompleteTask(ErinnTimerTag tag, ErinnTaskCompletionSource<ErinnTimerTag>? source) => source!.SetResult(tag);
        }
    }
}