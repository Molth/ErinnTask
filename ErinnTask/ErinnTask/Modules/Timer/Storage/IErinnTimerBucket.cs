using System;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Defines the scheduler-facing operations implemented by a typed timer bucket.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>Each bucket owns its callback entries and slot metadata.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 All operations must use the owning scheduler's thread; concurrent calls from multiple threads
    ///                 are unsupported.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>The scheduler owns the timer queue; a bucket only reports and updates slot-to-queue positions.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    internal interface IErinnTimerBucket
    {
        /// <summary>
        ///     Finds the queue position for a live entry whose slot and unique id match.
        /// </summary>
        /// <param name="uid">The unique timer id stored in the entry.</param>
        /// <param name="id">The entry's slot index; values outside the allocated slot range are not live.</param>
        /// <param name="queueIndex">
        ///     When this method returns <see langword="true" />, contains the entry's current queue index;
        ///     otherwise, contains zero.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if the slot is live and matches <paramref name="uid" />; otherwise
        ///     <see langword="false" />.
        /// </returns>
        [ThreadAffine]
        bool TryGetQueueIndex(ulong uid, int id, out int queueIndex);

        /// <summary>
        ///     Records the heap position of an active slot after a queue insertion or move.
        /// </summary>
        /// <param name="id">The active, allocated bucket slot index.</param>
        /// <param name="index">
        ///     The current heap position of that slot, in the range from zero to the number of queued timers minus
        ///     one.
        /// </param>
        [ThreadAffine]
        void SetQueueIndex(int id, int index);

        /// <summary>
        ///     Releases the matching entry and invokes its callback with the specified completion tag.
        /// </summary>
        /// <param name="uid">The unique id stored in the entry.</param>
        /// <param name="id">The entry's slot index.</param>
        /// <param name="tag">
        ///     The completion reason to pass to the callback; callers supply a defined <see cref="ErinnTimerTag" />
        ///     value.
        /// </param>
        /// <param name="exception">When the matching entry's callback throws, contains that exception; otherwise contains null.</param>
        /// <returns>
        ///     <see langword="true" /> if the entry was live and matched <paramref name="uid" /> and its callback was
        ///     invoked; otherwise <see langword="false" />. A callback exception does not change a successful return.
        /// </returns>
        [ThreadAffine]
        bool Trigger(ulong uid, int id, ErinnTimerTag tag, out Exception? exception);

        /// <summary>
        ///     Releases callback and slot metadata after the owning scheduler has been disposed.
        /// </summary>
        [ThreadAffine]
        void ReleaseStorage();
    }
}