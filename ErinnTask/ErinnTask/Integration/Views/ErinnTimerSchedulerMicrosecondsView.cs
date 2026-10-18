using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Provides timer scheduling, updating, and awaitable operations using
    ///     microsecond timestamps for an existing scheduler.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 <see cref="Schedule{T}" />, <see cref="WaitUntil" />, and
    ///                 <see cref="Update" /> use absolute microsecond timestamps.
    ///                 <see cref="WaitUntilMilliseconds" /> and
    ///                 <see cref="WaitUntilSeconds" /> convert their absolute timestamps to microseconds
    ///                 before scheduling.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 This stack-only view does not own or dispose its scheduler.
    ///                 Use <c>InMicroseconds()</c> to create an initialized view.
    ///                 A default-initialized view has no scheduler and must not be used.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Keep scheduling, updates, cancellation, and disposal on the scheduler's owning thread.
    ///                 Pending await continuations run inline on the thread that completes their timer.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Cancellation completes the awaitable with a tag, not a cancellation exception.
    ///                 Disposal completes pending timers with <see cref="ErinnTimerTag.Disposed" />.
    ///                 Continuation exceptions follow the scheduler's callback exception policy.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Consume each awaitable operation once.
    ///                 A completed operation may be consumed from another thread.
    ///                 A registered continuation runs on the completion thread.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public readonly ref struct ErinnTimerSchedulerMicrosecondsView
    {
        /// <summary>
        ///     The scheduler that owns the timers exposed through this view.
        /// </summary>
        private readonly ErinnTimerScheduler _scheduler;

        /// <summary>
        ///     Initializes a non-owning view over the specified scheduler.
        /// </summary>
        /// <param name="scheduler">
        ///     The non-null scheduler exposed by this view.
        ///     The public factory validates it before calling this constructor.
        /// </param>
        /// <remarks>
        ///     The view retains the scheduler reference but does not create, own, or dispose the scheduler.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ErinnTimerSchedulerMicrosecondsView(ErinnTimerScheduler scheduler) => _scheduler = scheduler;

        /// <summary>
        ///     Schedules <paramref name="callback" /> to run at the specified absolute
        ///     timestamp in microseconds.
        /// </summary>
        /// <param name="timestamp">
        ///     The absolute timestamp at which the callback should run. Any
        ///     <see cref="ulong" /> value is accepted;
        ///     use the same time origin and microsecond unit as calls to <see cref="Update" />.
        /// </param>
        /// <param name="callback">
        ///     The non-null callback invoked once after scheduling succeeds. It receives
        ///     <see cref="ErinnTimerTag.Ok" /> when fired, <see cref="ErinnTimerTag.Canceled" />
        ///     when canceled, or <see cref="ErinnTimerTag.Disposed" /> if disposed while pending.
        /// </param>
        /// <param name="data">
        ///     The payload passed unchanged to <paramref name="callback" />.
        ///     Reference-type payloads may be null.
        /// </param>
        /// <typeparam name="T">The type of the payload passed to <paramref name="callback" />.</typeparam>
        /// <returns>A handle that can cancel the timer while it is pending.</returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if this is a default-initialized view without a scheduler,
        ///     or if <paramref name="callback" /> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///     Thrown if the scheduler has already
        ///     been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if all non-zero timer uids have already been assigned.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 This method does not read a clock or convert units.
        ///                 It treats the timestamp as absolute; a timer becomes eligible when
        ///                 <see cref="Update" /> receives an equal or greater timestamp.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Callbacks run inline on the thread that updates, cancels,
        ///                 or disposes the scheduler.
        ///                 When configured, <see cref="ErinnTimerScheduler.OnException" /> receives callback
        ///                 exceptions. Without a handler, they propagate from <see cref="Update" /> or
        ///                 <see cref="ErinnTimerHandle.Cancel" />. Handler exceptions also propagate.
        ///                 During disposal, failures are collected and thrown as an
        ///                 <see cref="AggregateException" /> after pending timers are drained.
        ///                 Removing a timer is not undone when its callback throws.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Timers with the same timestamp run in scheduling order.
        ///                 A callback may schedule another
        ///                 timer, and that timer may run in the same update if it is already due.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>The payload is passed without boxing.</description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 The scheduler must not be accessed concurrently from
        ///                 multiple threads.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public ErinnTimerHandle Schedule<T>(ulong timestamp, Action<ErinnTimerTag, T?> callback, T? data)
        {
            ThrowHelpers.ThrowIfNull(_scheduler, ExceptionArgument.scheduler);

            return _scheduler.Schedule(timestamp, callback, data);
        }

        /// <summary>
        ///     Processes pending timers whose absolute timestamp is less
        ///     than or equal to the specified microsecond timestamp, in queue order.
        /// </summary>
        /// <param name="timestamp">
        ///     The absolute timestamp in microseconds. Any <see cref="ulong" />
        ///     value is accepted; timestamps do not have to increase between calls.
        /// </param>
        /// <exception cref="Exception">
        ///     Thrown if a callback has no configured handler, or if the configured handler itself throws.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if this is a default-initialized view without
        ///     a scheduler.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 Due timers run by timestamp, then by scheduling order for ties.
        ///                 A callback can schedule a timer that runs during this call if it is already due.
        ///                 A reentrant <see cref="Update" /> runs immediately with its own timestamp.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Callbacks run inline on the calling thread. When
        ///                 <see cref="ErinnTimerScheduler.OnException" />
        ///                 is <see langword="null" />, a callback exception
        ///                 propagates and stops this update. When a handler is configured,
        ///                 callback exceptions are reported to it and later due timers continue;
        ///                 an exception thrown by the handler propagates and stops processing.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Calls made after the scheduler has been disposed are ignored.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 The scheduler must not be accessed concurrently from
        ///                 multiple threads.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public void Update(ulong timestamp)
        {
            ThrowHelpers.ThrowIfNull(_scheduler, ExceptionArgument.scheduler);

            _scheduler.Update(timestamp);
        }

        /// <summary>
        ///     Schedules an awaitable timer to complete at the specified microsecond timestamp.
        /// </summary>
        /// <param name="timestamp">
        ///     The absolute timestamp in microseconds. It must use the same time origin and unit as
        ///     timestamps passed to <see cref="ErinnTimerScheduler.Update" />; it is not a delay. Any
        ///     <see cref="ulong" /> value is valid.
        /// </param>
        /// <param name="handle">
        ///     On successful return, receives a handle that can cancel this timer
        ///     before it completes.
        /// </param>
        /// <returns>
        ///     An operation that completes with one of the following tags:
        ///     <list type="bullet">
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Ok" /> when the timer fires.</description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 <see cref="ErinnTimerTag.Canceled" /> when the timer is
        ///                 canceled.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 <see cref="ErinnTimerTag.Disposed" /> when the scheduler is
        ///                 disposed.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if this is a default-initialized view without
        ///     a scheduler.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///     Thrown if the scheduler has already
        ///     been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the scheduler cannot assign another
        ///     timer uid or its completion source cannot be reused.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 The timer completes when <see cref="ErinnTimerScheduler.Update" />
        ///                 receives a timestamp at or past the scheduled value.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 A value of <c>0</c> is scheduled normally and still
        ///                 waits for an update, cancellation, or scheduler disposal.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Await the operation exactly once. Its continuation runs
        ///                 inline on the thread calling
        ///                 <see cref="ErinnTimerScheduler.Update" />,
        ///                 <see cref="ErinnTimerHandle.Cancel" />, or
        ///                 <see cref="ErinnTimerScheduler.Dispose" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Cancellation completes with
        ///                 <see cref="ErinnTimerTag.Canceled" /> and disposal completes with
        ///                 <see cref="ErinnTimerTag.Disposed" />; neither outcome is represented
        ///                 by a thrown cancellation
        ///                 exception.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Once the cached callbacks, routed buckets, and the shared bounded completion-source queue are
        ///                 initialized with sufficient capacity, and the scheduler arrays have enough
        ///                 capacity, scheduling and successful completion can avoid managed allocations.
        ///                 First-use initialization, a source allocation when the queue is empty, scheduler-array
        ///                 growth, and exception paths can allocate.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public ErinnTask<ErinnTimerTag> WaitUntil(ulong timestamp, out ErinnTimerHandle handle)
        {
            ThrowHelpers.ThrowIfNull(_scheduler, ExceptionArgument.scheduler);

            return _scheduler.WaitUntil(timestamp, out handle);
        }

        /// <summary>
        ///     Schedules an awaitable timer at the specified absolute timestamp measured in milliseconds.
        /// </summary>
        /// <param name="timestamp">
        ///     The absolute timestamp in milliseconds.
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 It must use the same time origin as timestamps passed to <see cref="Update" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 The accepted range is <c>0</c> through <c>18446744073709551UL</c>, inclusive.
        ///                 Larger values are rejected because conversion would exceed
        ///                 <see cref="ulong.MaxValue" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 The method multiplies it by 1,000 to obtain the microsecond
        ///                 timestamp.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </param>
        /// <param name="handle">
        ///     On successful return, receives a handle that can cancel this timer
        ///     before it completes.
        /// </param>
        /// <returns>
        ///     An operation that completes with one of these tags:
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 <see cref="ErinnTimerTag.Ok" /> when an update reaches the
        ///                 timestamp.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 <see cref="ErinnTimerTag.Canceled" /> when the timer is
        ///                 canceled.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 <see cref="ErinnTimerTag.Disposed" /> when the scheduler is
        ///                 disposed first.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if this is a default-initialized view without
        ///     a scheduler.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="timestamp" /> is greater than
        ///     <c>18446744073709551UL</c>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///     Thrown if the scheduler has already
        ///     been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the scheduler cannot assign another
        ///     timer uid or its completion source cannot be reused.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 This is an absolute timestamp, not a delay. The timer completes only when
        ///                 <see cref="ErinnTimerScheduler.Update" /> receives a timestamp equal to or
        ///                 greater than the converted
        ///                 microsecond value.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 A value of <c>0</c> is scheduled normally and still
        ///                 waits for an update, cancellation, or scheduler disposal.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 The method validates and converts the input, then passes the resulting
        ///                 microsecond timestamp unchanged to the awaitable timer api.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public ErinnTask<ErinnTimerTag> WaitUntilMilliseconds(ulong timestamp, out ErinnTimerHandle handle)
        {
            ThrowHelpers.ThrowIfNull(_scheduler, ExceptionArgument.scheduler);
            ThrowHelpers.ThrowIfTimerMillisecondsOutOfRange(timestamp, ExceptionArgument.timestamp);

            return _scheduler.WaitUntil(timestamp * 1000UL, out handle);
        }

        /// <summary>
        ///     Schedules an awaitable timer at the specified absolute timestamp measured in seconds.
        /// </summary>
        /// <param name="timestamp">
        ///     The absolute timestamp in seconds.
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 It must be finite and numerically non-negative; negative zero is accepted as zero.
        ///                 It must use the same time origin as timestamps passed to <see cref="Update" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 It must be less than <c>18446744073709.55d</c>. This is the first representable
        ///                 <see cref="double" /> seconds value whose product by 1,000,000, evaluated as a
        ///                 <see cref="double" />, is at least <c>2^64</c>, so the boundary
        ///                 itself is rejected.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Fractional precision below one microsecond is truncated
        ///                 toward zero.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </param>
        /// <param name="handle">
        ///     On successful return, receives a handle that can cancel this timer
        ///     before it completes.
        /// </param>
        /// <returns>
        ///     An operation that completes with one of these tags:
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 <see cref="ErinnTimerTag.Ok" /> when an update reaches the
        ///                 timestamp.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 <see cref="ErinnTimerTag.Canceled" /> when the timer is
        ///                 canceled.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 <see cref="ErinnTimerTag.Disposed" /> when the scheduler is
        ///                 disposed first.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if this is a default-initialized view without
        ///     a scheduler.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="timestamp" /> is negative, non-finite, or greater than or equal to
        ///     <c>18446744073709.55d</c>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///     Thrown if the scheduler has already
        ///     been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the scheduler cannot assign another
        ///     timer uid or its completion source cannot be reused.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 This is an absolute timestamp, not a delay. The converted
        ///                 microsecond value is compared
        ///                 directly with timestamps passed to <see cref="ErinnTimerScheduler.Update" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 A value of <c>0</c> is scheduled normally and still
        ///                 waits for an update, cancellation, or scheduler disposal.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 The method validates and converts the input, then passes the resulting
        ///                 microsecond timestamp unchanged to the awaitable timer api.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public ErinnTask<ErinnTimerTag> WaitUntilSeconds(double timestamp, out ErinnTimerHandle handle)
        {
            ThrowHelpers.ThrowIfNull(_scheduler, ExceptionArgument.scheduler);
            ThrowHelpers.ThrowIfTimerSecondsOutOfRange(timestamp, ExceptionArgument.timestamp);

            return _scheduler.WaitUntil((ulong)(timestamp * 1000000d), out handle);
        }
    }
}