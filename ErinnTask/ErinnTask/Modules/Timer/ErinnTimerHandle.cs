using System;
using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Represents a handle returned by <see cref="ErinnTimerScheduler.Create{T}" /> that allows a scheduled timer
    ///     to be canceled before it fires. Canceling still invokes the callback, reported with
    ///     <see cref="ErinnTimerTag.Canceled" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Cancellation does not mutate the handle. Repeated calls return <see langword="false" /> after
    ///                 the timer is no longer pending.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Handles are value copies that refer to one scheduler entry and are not thread-safe. Use them
    ///                 only on the owning scheduler's thread.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>An empty or default handle is invalid and cannot cancel a timer.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ErinnTimerHandle : IIsCreated, IEquatable<ErinnTimerHandle>
    {
        /// <summary>
        ///     The scheduler that owns the timer, or <see langword="null" /> when the handle is empty.
        /// </summary>
        private readonly ErinnTimerScheduler? _scheduler;

        /// <summary>
        ///     The bucket that owns the timer, or <see langword="null" /> when the handle is empty.
        /// </summary>
        private readonly IErinnTimerBucket? _bucket;

        /// <summary>
        ///     The index of the entry inside its bucket; meaningful only together with the scheduler and unique id.
        /// </summary>
        private readonly int _slotIndex;

        /// <summary>
        ///     The unique id of the timer entry; <c>0</c> when the handle is empty.
        /// </summary>
        private readonly ulong _uid;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ErinnTimerHandle" /> structure.
        /// </summary>
        /// <param name="scheduler">The scheduler that owns the timer.</param>
        /// <param name="bucket">The bucket that owns the timer.</param>
        /// <param name="slotIndex">The index of the entry inside its bucket.</param>
        /// <param name="uid">The unique id of the timer entry.</param>
        internal ErinnTimerHandle(ErinnTimerScheduler scheduler, IErinnTimerBucket bucket, int slotIndex, ulong uid)
        {
            _scheduler = scheduler;
            _bucket = bucket;
            _slotIndex = slotIndex;
            _uid = uid;
        }

        /// <summary>
        ///     Gets <see langword="true" /> if the scheduler issued this handle for a timer; otherwise
        ///     <see langword="false" />. This remains <see langword="true" /> after the timer completes or its
        ///     scheduler is disposed.
        /// </summary>
        /// <remarks>
        ///     The handle is immutable. This property does not query timer liveness; <see cref="Cancel" />
        ///     reports whether a pending timer was actually canceled.
        /// </remarks>
        public bool IsCreated => _scheduler != null;

        /// <summary>
        ///     Cancels the timer associated with this handle and immediately invokes its callback on the calling thread with
        ///     <see cref="ErinnTimerTag.Canceled" />, if it has not fired yet.
        /// </summary>
        /// <returns>
        ///     <see langword="true" /> if a pending timer was canceled and its callback was invoked with
        ///     <see cref="ErinnTimerTag.Canceled" />; otherwise <see langword="false" /> if the handle is empty, the
        ///     scheduler is disposed, or the timer is no longer pending.
        /// </returns>
        /// <exception cref="Exception">
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 The callback exception is rethrown when <see cref="ErinnTimerScheduler.OnException" /> is
        ///                 <see langword="null" />.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>An exception thrown by that handler is also propagated.</description>
        ///         </item>
        ///     </list>
        /// </exception>
        /// <remarks>
        ///     The timer entry is released before the callback runs, so a throwing callback cannot leave the timer pending.
        /// </remarks>
        [ThreadAffine]
        public bool Cancel() => _scheduler != null && _scheduler.Cancel(_bucket!, _slotIndex, _uid);

        /// <summary>
        ///     Indicates whether this handle refers to the same scheduler and timer id as another handle. Bucket indices
        ///     are not compared; default handles compare equal to one another.
        /// </summary>
        /// <param name="other">The handle to compare with this handle.</param>
        /// <returns>
        ///     <see langword="true" /> if both handles have the same scheduler reference and timer id; otherwise
        ///     <see langword="false" />.
        /// </returns>
        public bool Equals(ErinnTimerHandle other) => ReferenceEquals(_scheduler, other._scheduler) && _uid == other._uid;

        /// <summary>
        ///     Indicates whether this handle is equal to an object of the same handle type.
        /// </summary>
        /// <param name="obj">The object to compare with this handle.</param>
        /// <returns>
        ///     <see langword="true" /> if <paramref name="obj" /> is an equal timer handle; otherwise,
        ///     <see langword="false" />.
        /// </returns>
        public override bool Equals(object? obj) => obj is ErinnTimerHandle other && Equals(other);

        /// <summary>
        ///     Returns a hash code based on the owning scheduler reference and timer id. Equal handles return equal hash codes.
        /// </summary>
        /// <returns>The hash code for this handle.</returns>
        public override int GetHashCode() => HashCode.Combine(_scheduler, _uid);

        /// <summary>
        ///     Indicates whether two handles refer to the same scheduler and timer id.
        /// </summary>
        /// <param name="left">The first handle to compare.</param>
        /// <param name="right">The second handle to compare.</param>
        /// <returns>
        ///     <see langword="true" /> if both handles refer to the same scheduler and timer id; otherwise
        ///     <see langword="false" />.
        /// </returns>
        public static bool operator ==(ErinnTimerHandle left, ErinnTimerHandle right) => left.Equals(right);

        /// <summary>
        ///     Indicates whether two handles refer to different schedulers or timer ids.
        /// </summary>
        /// <param name="left">The first handle to compare.</param>
        /// <param name="right">The second handle to compare.</param>
        /// <returns>
        ///     <see langword="true" /> if the handles differ in scheduler reference or timer id; otherwise
        ///     <see langword="false" />.
        /// </returns>
        public static bool operator !=(ErinnTimerHandle left, ErinnTimerHandle right) => !left.Equals(right);
    }
}