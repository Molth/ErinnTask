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
    ///                 Cancellation does not mutate the token. Repeated calls return <see langword="false" /> after
    ///                 the timer is no longer pending.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Tokens are value copies that refer to one scheduler entry and are not thread-safe. Use them
    ///                 only on the owning scheduler's thread.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>An empty or default token is invalid and cannot cancel a timer.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ErinnTimerCancelTokenSource : IEquatable<ErinnTimerCancelTokenSource>
    {
        /// <summary>
        ///     The scheduler that owns the timer, or <see langword="null" /> when the token is empty.
        /// </summary>
        private readonly ErinnTimerScheduler? _scheduler;

        /// <summary>
        ///     The bucket that owns the timer, or <see langword="null" /> when the token is empty.
        /// </summary>
        private readonly IErinnTimerBucket? _bucket;

        /// <summary>
        ///     The index of the entry inside its bucket; meaningful only together with the scheduler and unique id.
        /// </summary>
        private readonly int _bucketId;

        /// <summary>
        ///     The unique id of the timer entry; <c>0</c> when the token is empty.
        /// </summary>
        private readonly ulong _uid;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ErinnTimerCancelTokenSource" /> structure.
        /// </summary>
        /// <param name="scheduler">The scheduler that owns the timer.</param>
        /// <param name="bucket">The bucket that owns the timer.</param>
        /// <param name="bucketId">The index of the entry inside its bucket.</param>
        /// <param name="uid">The unique id of the timer entry.</param>
        internal ErinnTimerCancelTokenSource(ErinnTimerScheduler scheduler, IErinnTimerBucket bucket, int bucketId, ulong uid)
        {
            _scheduler = scheduler;
            _bucket = bucket;
            _bucketId = bucketId;
            _uid = uid;
        }

        /// <summary>
        ///     Gets <see langword="true" /> if this token contains a scheduler reference; otherwise
        ///     <see langword="false" />. This reports whether the token was created for a timer, not whether that timer
        ///     is still pending.
        /// </summary>
        /// <remarks>
        ///     The token is immutable. This property does not query timer liveness; <see cref="Cancel" />
        ///     reports whether a pending timer was actually canceled.
        /// </remarks>
        public bool IsValid => _scheduler != null;

        /// <summary>
        ///     Cancels the timer associated with this token and immediately invokes its callback on the calling thread with
        ///     <see cref="ErinnTimerTag.Canceled" />, if it has not fired yet.
        /// </summary>
        /// <returns>
        ///     <see langword="true" /> if a pending timer was canceled and its callback was invoked with
        ///     <see cref="ErinnTimerTag.Canceled" />; otherwise <see langword="false" /> if the token is empty, the
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
        public bool Cancel() => _scheduler != null && _scheduler.Cancel(_bucket!, _bucketId, _uid);

        /// <summary>
        ///     Indicates whether this token refers to the same scheduler and timer id as another token. Bucket indices
        ///     are not compared; default tokens compare equal to one another.
        /// </summary>
        /// <param name="other">The token to compare with this token.</param>
        /// <returns>
        ///     <see langword="true" /> if both tokens have the same scheduler reference and timer id; otherwise
        ///     <see langword="false" />.
        /// </returns>
        public bool Equals(ErinnTimerCancelTokenSource other) => ReferenceEquals(_scheduler, other._scheduler) && _uid == other._uid;

        /// <summary>
        ///     Indicates whether this token is equal to an object of the same token type.
        /// </summary>
        /// <param name="obj">The object to compare with this token.</param>
        /// <returns>
        ///     <see langword="true" /> if <paramref name="obj" /> is an equal timer token; otherwise,
        ///     <see langword="false" />.
        /// </returns>
        public override bool Equals(object? obj) => obj is ErinnTimerCancelTokenSource other && Equals(other);

        /// <summary>
        ///     Returns a hash code based on the owning scheduler reference and timer id. Equal tokens return equal hash codes.
        /// </summary>
        /// <returns>The hash code for this token.</returns>
        public override int GetHashCode() => HashCode.Combine(_scheduler, _uid);

        /// <summary>
        ///     Indicates whether two tokens refer to the same scheduler and timer id.
        /// </summary>
        /// <param name="left">The first token to compare.</param>
        /// <param name="right">The second token to compare.</param>
        /// <returns>
        ///     <see langword="true" /> if both tokens refer to the same scheduler and timer id; otherwise
        ///     <see langword="false" />.
        /// </returns>
        public static bool operator ==(ErinnTimerCancelTokenSource left, ErinnTimerCancelTokenSource right) => left.Equals(right);

        /// <summary>
        ///     Indicates whether two tokens refer to different schedulers or timer ids.
        /// </summary>
        /// <param name="left">The first token to compare.</param>
        /// <param name="right">The second token to compare.</param>
        /// <returns>
        ///     <see langword="true" /> if the tokens differ in scheduler reference or timer id; otherwise
        ///     <see langword="false" />.
        /// </returns>
        public static bool operator !=(ErinnTimerCancelTokenSource left, ErinnTimerCancelTokenSource right) => !left.Equals(right);
    }
}