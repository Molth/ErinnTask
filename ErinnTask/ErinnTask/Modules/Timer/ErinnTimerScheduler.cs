using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Schedules callbacks to run at caller-supplied absolute timestamps and drives them through <see cref="Update" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>The timer queue orders timestamps and unique ids.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Typed buckets own callback storage and slot reuse, while queue nodes and cancellation tokens
    ///                 address those buckets directly.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Reference-type payloads use the shared object bucket. Reference-free value payloads of at most
    ///                 four bytes use the <see cref="int" /> bucket; payloads larger than four and at most eight bytes
    ///                 use the <see cref="long" /> bucket. Other value payloads use per-scheduler buckets keyed by
    ///                 their routed slot type. Payloads are stored without boxing.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Timestamps are caller-supplied absolute unsigned values; the scheduler has no clock and does not
    ///                 calculate delays. Call <see cref="Update" /> with values from the same time domain.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Callbacks execute inline on the thread that calls <see cref="Update" />,
    ///                 <see cref="ErinnTimerCancelTokenSource.Cancel" />, or <see cref="Dispose" />. Timers with equal
    ///                 timestamps run in scheduling order.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>Callback exceptions are reported through <see cref="OnException" />.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Concurrent operations from multiple threads on the same scheduler instance are not allowed;
    ///                 its queue, buckets, and disposal state are unsynchronized.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    public sealed class ErinnTimerScheduler : IDisposable
    {
        /// <summary>
        ///     Indicates whether disposal has started. Set before pending callbacks are invoked so reentrant scheduling
        ///     and cancellation cannot add work during disposal.
        /// </summary>
        private bool _disposed;

        /// <summary>
        ///     The last timer id assigned.
        ///     Ids are never reused; scheduling stops when this reaches <see cref="ulong.MaxValue" />.
        /// </summary>
        private ulong _uid;

        /// <summary>
        ///     The pending timers, ordered by absolute timestamp and then insertion id for equal timestamps.
        /// </summary>
        private readonly ErinnTimerQueue _timers;

        /// <summary>
        ///     The per-scheduler bucket shared by every reference-type payload, routed through an object reference slot.
        /// </summary>
        private readonly ErinnTimerBucket<object> _referenceBucket;

        /// <summary>
        ///     The per-scheduler bucket shared by reference-free value payloads whose size is at most four bytes.
        /// </summary>
        private readonly ErinnTimerBucket<int> _smallBucket;

        /// <summary>
        ///     The per-scheduler bucket shared by reference-free value payloads larger than four and at most eight bytes.
        /// </summary>
        private readonly ErinnTimerBucket<long> _wideBucket;

        /// <summary>
        ///     Per-scheduler fallback buckets keyed only by routed storage slot type, created on first use for that slot.
        /// </summary>
        private readonly Dictionary<Type, IErinnTimerBucket> _buckets;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ErinnTimerScheduler" /> class.
        /// </summary>
        private ErinnTimerScheduler()
        {
            _timers = new ErinnTimerQueue(16);
            _referenceBucket = new ErinnTimerBucket<object>(4);
            _smallBucket = new ErinnTimerBucket<int>(4);
            _wideBucket = new ErinnTimerBucket<long>(4);
            _buckets = new Dictionary<Type, IErinnTimerBucket>(4);
        }

        /// <summary>
        ///     Gets or sets the callback invoked when a timer callback throws. The handler is read when the exception is
        ///     reported, so replacing it affects later reports; setting it to <see langword="null" /> makes the original
        ///     callback exception propagate from the triggering operation.
        /// </summary>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 When this is <see langword="null" />, a callback exception is thrown to the caller of
        ///                 <see cref="Update" /> and <see cref="ErinnTimerCancelTokenSource.Cancel" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 When this callback itself throws, that exception is propagated from <see cref="Update" /> and
        ///                 <see cref="ErinnTimerCancelTokenSource.Cancel" />, and is collected into the
        ///                 <see cref="AggregateException" /> thrown by <see cref="Dispose" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>Disposal releases the handler reference after all pending callbacks have been processed.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public Action<Exception>? OnException { get; set; }

        /// <summary>
        ///     Marks the scheduler disposed, executes every pending timer with <see cref="ErinnTimerTag.Fault" />, and
        ///     releases its queue and bucket storage. Repeated calls after the first are no-ops, including after an
        ///     <see cref="AggregateException" /> is thrown.
        /// </summary>
        /// <exception cref="AggregateException">
        ///     Thrown after cleanup if one or more callbacks or exception handlers throw while
        ///     pending timers are drained.
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 Every pending callback is invoked with <see cref="ErinnTimerTag.Fault" /> before the scheduler is
        ///                 cleared.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Reentrant updates are ignored and cancellation returns <see langword="false" /> once disposal starts.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Scheduling from a callback that runs during the drain throws <see cref="ObjectDisposedException" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>A callback that throws is reported to <see cref="OnException" />.</description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Callback exceptions without an <see cref="OnException" /> handler, and exceptions thrown by
        ///                 that handler, are collected and rethrown together as a single <see cref="AggregateException" />
        ///                 after the scheduler has been cleared.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            List<Exception>? aggregated = null;

            while (_timers.TryDequeue(out var node))
            {
                node.Bucket.Trigger(node.Uid, node.BucketId, ErinnTimerTag.Fault, out var exception);
                if (exception == null)
                    continue;

                var handler = OnException;
                if (handler != null)
                {
                    try
                    {
                        handler(exception);
                    }
                    catch (Exception ex)
                    {
                        (aggregated ??= new List<Exception>()).Add(ex);
                    }
                }
                else
                {
                    (aggregated ??= new List<Exception>()).Add(exception);
                }
            }

            _timers.ReleaseStorage();
            _referenceBucket.ReleaseStorage();
            _smallBucket.ReleaseStorage();
            _wideBucket.ReleaseStorage();

            foreach (var pair in _buckets)
                pair.Value.ReleaseStorage();

            OnException = null;

            if (aggregated != null)
                ThrowHelpers.ThrowAggregate(aggregated);
        }

        /// <summary>
        ///     Creates a new, independent <see cref="ErinnTimerScheduler" /> with empty timer and callback storage.
        /// </summary>
        /// <returns>A new, empty scheduler.</returns>
        public static ErinnTimerScheduler Create() => new();

        /// <summary>
        ///     Schedules <paramref name="callback" /> to run when the timer is updated at or past
        ///     <paramref name="timestamp" />. The timestamp is an absolute unsigned value; this method does not read a
        ///     clock, convert units, or interpret it as a duration.
        /// </summary>
        /// <param name="timestamp">
        ///     The absolute timestamp at which the callback should run.
        ///     <list type="bullet">
        ///         <item>
        ///             <description>Every value from <c>0</c> through <see cref="ulong.MaxValue" /> is accepted.</description>
        ///         </item>
        ///         <item>
        ///             <description>Use the same time domain and unit as subsequent <see cref="Update" /> calls.</description>
        ///         </item>
        ///     </list>
        /// </param>
        /// <param name="callback">
        ///     The non-null callback to invoke exactly once after scheduling succeeds. It receives:
        ///     <list type="bullet">
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Success" /> when the timer fires.</description>
        ///         </item>
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Canceled" /> when the timer is canceled.</description>
        ///         </item>
        ///         <item>
        ///             <description><see cref="ErinnTimerTag.Fault" /> when the scheduler is disposed.</description>
        ///         </item>
        ///     </list>
        /// </param>
        /// <param name="data">The payload passed unchanged to the callback; reference payloads may be null.</param>
        /// <typeparam name="T">The type of the data passed to the callback.</typeparam>
        /// <returns>A token that can be used to cancel the timer before it fires.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the scheduler has already been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="callback" /> is <see langword="null" />.</exception>
        /// <exception cref="InvalidOperationException">Thrown if all non-zero timer ids have already been assigned.</exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>The callback is invoked inline on the thread that calls update, cancellation, or disposal.</description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 A callback exception does not restore the removed timer; it is reported through
        ///                 <see cref="OnException" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>The scheduler must not be accessed concurrently from multiple threads.</description>
        ///         </item>
        ///         <item>
        ///             <description>Reference types are routed through the <see cref="object" /> bucket.</description>
        ///         </item>
        ///         <item>
        ///             <description>Unmanaged payloads of at most 4 bytes are routed through the <see cref="int" /> bucket.</description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Reference-free value payloads larger than 4 and at most 8 bytes are routed through the
        ///                 <see cref="long" /> bucket.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Other value payloads, including reference-containing structs, use a typed per-scheduler
        ///                 fallback bucket.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>The payload is never boxed.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public ErinnTimerCancelTokenSource Create<T>(ulong timestamp, Action<ErinnTimerTag, T?> callback, T? data)
        {
            ThrowHelpers.ThrowIfDisposed(_disposed, nameof(ErinnTimerScheduler));
            ThrowHelpers.ThrowIfNull(callback, ExceptionArgument.callback);
            ThrowHelpers.ThrowIfTimerIdExhausted(_uid);

            if (!typeof(T).IsValueType)
                return Schedule(_referenceBucket, timestamp, callback, data);

            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                if (Unsafe.SizeOf<T>() <= 4)
                    return Schedule(_smallBucket, timestamp, callback, data);

                if (Unsafe.SizeOf<T>() <= 8)
                    return Schedule(_wideBucket, timestamp, callback, data);
            }

            return Schedule(GetBucket<T>(), timestamp, callback, data);
        }

        /// <summary>
        ///     Allocates a callback entry in its routed bucket and queues the timer.
        /// </summary>
        /// <param name="bucket">The bucket that owns the callback and its routed payload.</param>
        /// <param name="timestamp">The timestamp at which the callback should run.</param>
        /// <param name="callback">The validated user callback.</param>
        /// <param name="data">The payload to route through the storage slot.</param>
        /// <typeparam name="TSlot">The type of the storage slot.</typeparam>
        /// <typeparam name="T">The callback parameter type.</typeparam>
        /// <returns>The token that cancels the queued timer.</returns>
        [ThreadAffine]
        private ErinnTimerCancelTokenSource Schedule<TSlot, T>(ErinnTimerBucket<TSlot> bucket, ulong timestamp, Action<ErinnTimerTag, T?> callback, T? data)
        {
            var uid = ++_uid;
            var bucketId = bucket.Allocate(uid, callback, data);
            _timers.Enqueue(new ErinnTimerNode(bucket, bucketId, timestamp, uid));
            return new ErinnTimerCancelTokenSource(this, bucket, bucketId, uid);
        }

        /// <summary>
        ///     Runs every timer whose timestamp is less than or equal to <paramref name="timestamp" />, in timestamp
        ///     order and then scheduling order for ties. A timer scheduled by a callback can also run during this call
        ///     if its timestamp is due.
        /// </summary>
        /// <param name="timestamp">
        ///     The caller-supplied absolute timestamp in the same unit and time domain used when scheduling
        ///     timers. Any <see cref="ulong" /> value is accepted.
        /// </param>
        /// <exception cref="Exception">
        ///     <list type="bullet">
        ///         <item>
        ///             <description>The callback exception propagates when <see cref="OnException" /> is <see langword="null" />.</description>
        ///         </item>
        ///         <item>
        ///             <description>An exception thrown by the configured handler also propagates.</description>
        ///         </item>
        ///     </list>
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 Every queued timer due at the supplied timestamp is removed before its callback is invoked.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 A callback that throws is reported to <see cref="OnException" />,
        ///                 so remaining due timers continue to run.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 If the handler is <see langword="null" />, the callback exception propagates and stops this update;
        ///                 if the handler itself throws, that exception propagates and also stops processing.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Timestamps need not be monotonic: each call fires timers
        ///                 whose absolute timestamp is at most its argument.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>Calls made after disposal are ignored.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        public void Update(ulong timestamp)
        {
            if (_disposed)
                return;

            while (_timers.TryDequeue(timestamp, out var node))
            {
                node.Bucket.Trigger(node.Uid, node.BucketId, ErinnTimerTag.Success, out var exception);
                ReportException(exception);
            }
        }

        /// <summary>
        ///     Cancels the timer identified by the specified bucket and slot and immediately invokes its callback.
        ///     The callback is invoked with <see cref="ErinnTimerTag.Canceled" />.
        /// </summary>
        /// <param name="bucket">The bucket that owns the callback; it must belong to this scheduler.</param>
        /// <param name="bucketId">The allocated slot index inside <paramref name="bucket" />.</param>
        /// <param name="uid">The non-zero unique id of the timer entry.</param>
        /// <returns>
        ///     <see langword="true" /> if the timer was pending and its callback was invoked with
        ///     <see cref="ErinnTimerTag.Canceled" />; otherwise <see langword="false" /> if disposal has started or the
        ///     bucket entry is no longer live.
        /// </returns>
        /// <exception cref="Exception">
        ///     <list type="bullet">
        ///         <item>
        ///             <description>The callback exception propagates when <see cref="OnException" /> is <see langword="null" />.</description>
        ///         </item>
        ///         <item>
        ///             <description>An exception thrown by the configured handler also propagates.</description>
        ///         </item>
        ///     </list>
        /// </exception>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>A callback that throws is reported to <see cref="OnException" />.</description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 If <see cref="OnException" /> is <see langword="null" />
        ///                 the callback exception is thrown to the caller;
        ///                 if <see cref="OnException" /> itself throws, that exception is propagated.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        [ThreadAffine]
        internal bool Cancel(IErinnTimerBucket bucket, int bucketId, ulong uid)
        {
            if (_disposed)
                return false;

            if (!bucket.TryGetQueueIndex(uid, bucketId, out var queueIndex))
                return false;

            _timers.RemoveAt(queueIndex);
            if (!bucket.Trigger(uid, bucketId, ErinnTimerTag.Canceled, out var exception))
                ThrowHelpers.ThrowInvalidOperation(SR.InvalidOperation_TimerEntryChangedDuringCancellation);

            ReportException(exception);

            return true;
        }

        /// <summary>
        ///     Gets or creates the bucket that owns entries of the specified storage slot type.
        /// </summary>
        /// <typeparam name="TSlot">The type of the storage slot the payload is routed through.</typeparam>
        /// <returns>The typed bucket for the storage slot type.</returns>
        [ThreadAffine]
        private ErinnTimerBucket<TSlot> GetBucket<TSlot>()
        {
            var slotType = typeof(TSlot);
            var buckets = _buckets;
            if (buckets.TryGetValue(slotType, out var bucket))
                return (ErinnTimerBucket<TSlot>)bucket;

            var typed = new ErinnTimerBucket<TSlot>(4);
            buckets.Add(slotType, typed);
            return typed;
        }

        /// <summary>
        ///     Returns immediately for a null exception; otherwise reports it to the current handler or rethrows it
        ///     when no handler is installed.
        /// </summary>
        /// <param name="exception">The callback exception, or <see langword="null" /> when the callback succeeded.</param>
        /// <exception cref="Exception">
        ///     Rethrows the callback exception when no <see cref="OnException" /> handler is configured,
        ///     or propagates an exception thrown by that handler.
        /// </exception>
        [ThreadAffine]
        private void ReportException(Exception? exception)
        {
            if (exception == null)
                return;

            var handler = OnException;
            if (handler == null)
                ThrowHelpers.Throw(exception);

            handler(exception);
        }
    }
}