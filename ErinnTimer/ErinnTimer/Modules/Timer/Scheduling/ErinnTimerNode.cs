using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Holds a timer's scheduling key and the bucket and slot that own its callback.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ErinnTimerNode
    {
        /// <summary>
        ///     The bucket that owns the callback.
        /// </summary>
        public readonly IErinnTimerBucket Bucket;

        /// <summary>
        ///     The index of the entry inside its bucket.
        /// </summary>
        public readonly int SlotIndex;

        /// <summary>
        ///     The timestamp at which the timer fires.
        /// </summary>
        public readonly ulong Timestamp;

        /// <summary>
        ///     The timer uid that identifies the entry and breaks timestamp ties.
        /// </summary>
        public readonly ulong Uid;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ErinnTimerNode" /> structure.
        /// </summary>
        /// <param name="bucket">The non-null bucket that owns the callback.</param>
        /// <param name="slotIndex">
        ///     The non-negative allocated slot index inside
        ///     <paramref name="bucket" />.
        /// </param>
        /// <param name="timestamp">
        ///     The absolute timestamp at which the timer fires, in the
        ///     scheduler's time domain.
        /// </param>
        /// <param name="uid">The non-zero timer uid of the entry.</param>
        public ErinnTimerNode(IErinnTimerBucket bucket, int slotIndex, ulong timestamp, ulong uid)
        {
            Bucket = bucket;
            SlotIndex = slotIndex;
            Timestamp = timestamp;
            Uid = uid;
        }

        /// <summary>
        ///     Determines scheduling order by timestamp, then by uid for equal timestamps.
        /// </summary>
        /// <param name="other">The node to compare with.</param>
        /// <returns>
        ///     <see langword="true" /> if this node has an earlier timestamp or the same
        ///     timestamp and a lower uid;
        ///     otherwise <see langword="false" />.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Precedes(in ErinnTimerNode other) => Timestamp < other.Timestamp || (Timestamp == other.Timestamp && Uid < other.Uid);
    }
}