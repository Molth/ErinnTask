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
    ///                 All operations must use the owning scheduler's thread;
    ///                 concurrent calls from multiple threads are unsupported.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 The scheduler owns the timer queue; a bucket only reports and updates
    ///                 slot-to-queue positions.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    internal interface IErinnTimerBucket : IDisposable
    {
        /// <summary>
        ///     Finds the queue position for a live entry whose slot and timer uid match.
        /// </summary>
        /// <param name="uid">The timer uid stored in the entry.</param>
        /// <param name="slotIndex">
        ///     The entry's slot index; values outside the allocated slot
        ///     range are not live.
        /// </param>
        /// <param name="queueIndex">
        ///     When this method returns <see langword="true" />,
        ///     contains the entry's current queue index; otherwise, contains zero.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if the slot is live and matches <paramref name="uid" />; otherwise
        ///     <see langword="false" />.
        /// </returns>
        bool TryGetQueueIndex(ulong uid, int slotIndex, out int queueIndex);

        /// <summary>
        ///     Records the heap position of an active slot after a queue insertion or move.
        /// </summary>
        /// <param name="slotIndex">The active, allocated bucket slot index.</param>
        /// <param name="index">
        ///     The current heap position of that slot, in the range
        ///     from zero to the number of queued timers minus one.
        /// </param>
        void SetQueueIndex(int slotIndex, int index);

        /// <summary>
        ///     Attempts to complete a matching live entry and invokes its callback
        ///     with the specified result tag.
        /// </summary>
        /// <param name="uid">The timer uid stored in the entry.</param>
        /// <param name="slotIndex">The entry's slot index.</param>
        /// <param name="tag">
        ///     The completion reason to pass to the callback; callers supply a defined
        ///     <see cref="ErinnTimerTag" />
        ///     value.
        /// </param>
        /// <param name="exception">
        ///     When the matching entry's callback throws, contains that exception;
        ///     otherwise contains null.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if the entry was live and matched
        ///     <paramref name="uid" /> and its callback was
        ///     invoked; otherwise <see langword="false" />.
        ///     A callback exception does not change a successful return.
        /// </returns>
        bool TryComplete(ulong uid, int slotIndex, ErinnTimerTag tag, out Exception? exception);
    }
}