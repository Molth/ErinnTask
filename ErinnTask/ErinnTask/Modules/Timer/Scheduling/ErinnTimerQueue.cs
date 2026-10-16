using System;
using System.Runtime.CompilerServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Orders timer nodes in a four-way heap and records their heap positions in their owning slots for direct removal.
    /// </summary>
    /// <remarks>
    ///     This queue is owned by one scheduler and is unsynchronized. All access must occur on that scheduler's thread;
    ///     concurrent calls are unsupported.
    /// </remarks>
    internal sealed class ErinnTimerQueue
    {
        /// <summary>
        ///     The nodes stored in scheduling order.
        /// </summary>
        private ErinnTimerNode[] _nodes;

        /// <summary>
        ///     The number of occupied nodes.
        /// </summary>
        private int _count;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ErinnTimerQueue" /> class.
        /// </summary>
        /// <param name="capacity">
        ///     The requested initial capacity. It must be non-negative; values below four allocate storage for
        ///     four nodes.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity" /> is negative.</exception>
        public ErinnTimerQueue(int capacity)
        {
            ThrowHelpers.ThrowIfNegative(capacity, ExceptionArgument.capacity);
            _nodes = new ErinnTimerNode[Math.Max(capacity, 4)];
        }

        /// <summary>
        ///     Gets the number of queued timers.
        /// </summary>
        [ThreadAffine]
        public int Count => _count;

        /// <summary>
        ///     Adds a timer node in scheduling order.
        /// </summary>
        /// <param name="node">
        ///     The timer node to insert. Its bucket must be non-null, its slot must already be allocated, and that slot
        ///     must remain active until the node is removed. This method updates the bucket's queue-index metadata.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ThreadAffine]
        public void Enqueue(in ErinnTimerNode node)
        {
            var index = _count;
            if (index == _nodes.Length)
            {
                var nodes = new ErinnTimerNode[CollectionHelpers.GrowCapacity(index, index + 1)];
                Array.Copy(_nodes, nodes, index);
                _nodes = nodes;
            }

            _count = index + 1;
            MoveUp(node, index);
        }

        /// <summary>
        ///     Removes the earliest timer, if one is present.
        /// </summary>
        /// <param name="node">When this method returns, contains the removed timer node.</param>
        /// <returns>
        ///     <see langword="true" /> if a node was removed; otherwise <see langword="false" /> and <paramref name="node" />
        ///     is default.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ThreadAffine]
        public bool TryDequeue(out ErinnTimerNode node) => TryDequeue(ulong.MaxValue, out node);

        /// <summary>
        ///     Removes the earliest timer when its timestamp is at or before the specified timestamp.
        /// </summary>
        /// <param name="timestamp">
        ///     The inclusive upper bound; a node is due when its timestamp is less than or equal to this
        ///     value.
        /// </param>
        /// <param name="node">
        ///     When this method returns <see langword="true" />, contains the removed timer node; otherwise,
        ///     contains default.
        /// </param>
        /// <returns><see langword="true" /> if the earliest node was due and removed; otherwise <see langword="false" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ThreadAffine]
        public bool TryDequeue(ulong timestamp, out ErinnTimerNode node)
        {
            if (_count == 0 || _nodes[0].Timestamp > timestamp)
            {
                node = default;
                return false;
            }

            node = _nodes[0];
            RemoveAt(0);
            return true;
        }

        /// <summary>
        ///     Removes a timer at its stored heap position and restores scheduling order. The caller must supply a valid
        ///     occupied index; this method does not validate the index.
        /// </summary>
        /// <param name="index">
        ///     An occupied heap index in the range [0, <see cref="Count" />). Callers obtain it from the owning
        ///     bucket's queue-index metadata; this method assumes the index is valid.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ThreadAffine]
        public void RemoveAt(int index)
        {
            var lastIndex = --_count;
            if (index < lastIndex)
            {
                var last = _nodes[lastIndex];
                if (index > 0 && last.Precedes(_nodes[(index - 1) >> 2]))
                    MoveUp(last, index);
                else
                    MoveDown(last, index);
            }

            _nodes[lastIndex] = default;
        }

        /// <summary>
        ///     Releases the node array once every timer has been drained during disposal. The queue cannot be used again
        ///     after this call.
        /// </summary>
        [ThreadAffine]
        public void ReleaseStorage() => _nodes = null!;

        /// <summary>
        ///     Moves a newly added node upward until its parents precede it.
        /// </summary>
        /// <param name="node">The node to move.</param>
        /// <param name="index">The initial node index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ThreadAffine]
        private void MoveUp(in ErinnTimerNode node, int index)
        {
            var nodes = _nodes;
            while (index > 0)
            {
                var parentIndex = (index - 1) >> 2;
                ref var parent = ref nodes[parentIndex];
                if (!node.Precedes(parent))
                    break;

                nodes[index] = parent;
                parent.Bucket.SetQueueIndex(parent.BucketId, index);
                index = parentIndex;
            }

            nodes[index] = node;
            node.Bucket.SetQueueIndex(node.BucketId, index);
        }

        /// <summary>
        ///     Moves a node downward until it precedes its children.
        /// </summary>
        /// <param name="node">The node to move.</param>
        /// <param name="index">The initial node index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ThreadAffine]
        private void MoveDown(in ErinnTimerNode node, int index)
        {
            var nodes = _nodes;
            var count = _count;
            var lastParentIndex = (count - 2) >> 2;
            while (index <= lastParentIndex)
            {
                var firstChildIndex = (index << 2) + 1;
                var minChildIndex = firstChildIndex;
                ref var minChild = ref nodes[firstChildIndex];
                var childEnd = Math.Min(firstChildIndex + 4, count);
                for (var childIndex = firstChildIndex + 1; childIndex < childEnd; ++childIndex)
                {
                    ref var child = ref nodes[childIndex];
                    if (child.Precedes(minChild))
                    {
                        minChildIndex = childIndex;
                        minChild = ref child;
                    }
                }

                if (!minChild.Precedes(node))
                    break;

                nodes[index] = minChild;
                minChild.Bucket.SetQueueIndex(minChild.BucketId, index);
                index = minChildIndex;
            }

            nodes[index] = node;
            node.Bucket.SetQueueIndex(node.BucketId, index);
        }
    }
}