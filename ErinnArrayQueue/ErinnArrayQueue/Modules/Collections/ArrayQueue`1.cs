using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static Erinn.PaddingHelpers;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     A bounded multi-producer multi-consumer queue.
    /// </summary>
    /// <typeparam name="T">The type of values stored in the queue.</typeparam>
    /// <remarks>
    ///     This queue allocates a fixed-capacity buffer on construction, which is used to store elements
    ///     added to the queue. The queue cannot hold more elements than the buffer allows. Attempting
    ///     to add an element to a full queue will fail. The <see cref="Enqueue" /> method replaces the
    ///     oldest element when necessary, allowing this queue to be used as a ring buffer. Having a buffer
    ///     allocated up front makes this queue a bit faster than <c>SegQueue</c>.
    ///     <br />
    ///     The implementation is based on Dmitry Vyukov's bounded MPMC queue:
    ///     <c>http://www.1024cores.net/home/lock-free-algorithms/queues/bounded-mpmc-queue</c>.
    ///     <br />
    ///     Where rust implementation:
    ///     <c>https://github.com/crossbeam-rs/crossbeam/blob/main/crossbeam-queue/src/array_queue.rs</c>.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Size = (1 + 3) * CACHE_LINE_SIZE)]
    internal struct ArrayQueue<T>
    {
        /// <summary>
        ///     Padding intended to keep the queue from sharing cache lines with adjacent data.
        /// </summary>
        private readonly CachePadding _padding;

        /// <summary>
        ///     The head of the queue.
        /// </summary>
        /// <remarks>
        ///     This value is a stamp consisting of an index into the buffer and a lap, but packed into a
        ///     single <see cref="System.UIntPtr" />. The lower bits represent the index, while the upper
        ///     bits represent the lap.
        ///     Elements are removed from the beginning of the queue.
        /// </remarks>
        private CachePaddedAtomicUsize _head;

        /// <summary>
        ///     The tail of the queue.
        /// </summary>
        /// <remarks>
        ///     This value is a stamp consisting of an index into the buffer and a lap, but packed into a
        ///     single <see cref="System.UIntPtr" />. The lower bits represent the index, while the upper
        ///     bits represent the lap.
        ///     Elements are added to the end of the queue.
        /// </remarks>
        private CachePaddedAtomicUsize _tail;

        /// <summary>
        ///     The buffer holding slots.
        /// </summary>
        private readonly Slot[] _buffer;

        /// <summary>
        ///     A stamp with the value of <c>{ lap: 1, index: 0 }</c>.
        /// </summary>
        private readonly nuint _oneLap;

        /// <summary>
        ///     Creates a new bounded queue with the given capacity.
        /// </summary>
        /// <param name="capacity">The requested queue capacity.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="capacity" /> is less than or equal to zero.
        /// </exception>
        public ArrayQueue(int capacity)
        {
            ThrowHelpers.ThrowIfNegativeOrZero(capacity, ExceptionArgument.capacity);

            _padding = new CachePadding();

            _head = new CachePaddedAtomicUsize();
            _tail = new CachePaddedAtomicUsize();

            var buffer = new Slot[capacity];
            for (var i = 0; i < buffer.Length; ++i)
                buffer[i].Stamp = new UnsafeAtomicUsize((nuint)i);

            _buffer = buffer;
            _oneLap = BitOperationsHelpers.RoundUpToPowerOf2((uint)capacity + 1U);
        }

        /// <summary>
        ///     Attempts to add an element to the end of the queue.
        /// </summary>
        /// <param name="item">
        ///     The element to add to the queue.
        ///     The value can be a null reference.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if the element was enqueued;
        ///     otherwise, <see langword="false" /> if the queue is full.
        /// </returns>
        public bool TryEnqueue(T? item)
        {
            var backoff = new Backoff();
            var tail = _tail.Load(Ordering.Relaxed);

            while (true)
            {
                var index = tail & (_oneLap - 1);
                var lap = tail & ~(_oneLap - 1);

                var newTail = index + 1 < Capacity() ? tail + 1 : unchecked(lap + _oneLap);

                ref var slot = ref _buffer[(int)index];
                var stamp = slot.Stamp.Load(Ordering.Acquire);

                if (tail == stamp)
                {
                    var t = _tail.CompareExchange(newTail, tail);
                    if (t == tail)
                    {
                        slot.Value = item;
                        slot.Stamp.Store(tail + 1, Ordering.Release);
                        return true;
                    }

                    tail = t;
                    backoff.Spin();
                }
                else if (unchecked(stamp + _oneLap) == tail + 1)
                {
                    Interlocked.MemoryBarrier();
                    var head = _head.Load(Ordering.Relaxed);

                    if (unchecked(head + _oneLap) == tail)
                        return false;

                    backoff.Spin();
                    tail = _tail.Load(Ordering.Relaxed);
                }
                else
                {
                    backoff.Snooze();
                    tail = _tail.Load(Ordering.Relaxed);
                }
            }
        }

        /// <summary>
        ///     Adds an element to the end of the queue,
        ///     replacing the oldest element if necessary.
        /// </summary>
        /// <param name="item">The element to add to the queue.</param>
        /// <param name="overwritten">
        ///     When the queue is full, contains the oldest element that was replaced;
        ///     otherwise, contains the default value of <typeparamref name="T" />.
        /// </param>
        /// <returns>
        ///     <see cref="InsertResult.Success" /> if no element was replaced;
        ///     <see cref="InsertResult.Overwritten" /> if the oldest element was replaced.
        /// </returns>
        public InsertResult Enqueue(T? item, out T? overwritten)
        {
            var backoff = new Backoff();
            var tail = _tail.Load(Ordering.Relaxed);

            while (true)
            {
                var index = tail & (_oneLap - 1);
                var lap = tail & ~(_oneLap - 1);

                var newTail = index + 1 < Capacity() ? tail + 1 : unchecked(lap + _oneLap);

                ref var slot = ref _buffer[(int)index];
                var stamp = slot.Stamp.Load(Ordering.Acquire);

                if (tail == stamp)
                {
                    var t = _tail.CompareExchange(newTail, tail);
                    if (t == tail)
                    {
                        slot.Value = item;
                        slot.Stamp.Store(tail + 1, Ordering.Release);
                        overwritten = default;
                        return InsertResult.Success;
                    }

                    tail = t;
                    backoff.Spin();
                }
                else if (unchecked(stamp + _oneLap) == tail + 1)
                {
                    Interlocked.MemoryBarrier();

                    var head = unchecked(tail - _oneLap);
                    var newHead = unchecked(newTail - _oneLap);

                    var h = _head.CompareExchange(newHead, head);
                    if (h == head)
                    {
                        _tail.Store(newTail, Ordering.SeqCst);

                        overwritten = slot.Value;
                        slot.Value = item;

                        slot.Stamp.Store(tail + 1, Ordering.Release);
                        return InsertResult.Overwritten;
                    }

                    backoff.Spin();
                    tail = _tail.Load(Ordering.Relaxed);
                }
                else
                {
                    backoff.Snooze();
                    tail = _tail.Load(Ordering.Relaxed);
                }
            }
        }

        /// <summary>
        ///     Attempts to remove an element from the beginning of the queue.
        /// </summary>
        /// <param name="item">
        ///     When this method returns <see langword="true" />, contains the removed element;
        ///     otherwise, contains the default value of <typeparamref name="T" />.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if an element was removed;
        ///     otherwise, <see langword="false" /> if the queue is empty.
        /// </returns>
        public bool TryDequeue(out T? item)
        {
            var backoff = new Backoff();
            var head = _head.Load(Ordering.Relaxed);

            while (true)
            {
                var index = head & (_oneLap - 1);
                var lap = head & ~(_oneLap - 1);

                ref var slot = ref _buffer[(int)index];
                var stamp = slot.Stamp.Load(Ordering.Acquire);

                if (head + 1 == stamp)
                {
                    var @new = index + 1 < Capacity() ? head + 1 : unchecked(lap + _oneLap);

                    var h = _head.CompareExchange(@new, head);
                    if (h == head)
                    {
                        item = slot.Value;
                        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                            slot.Value = default;

                        slot.Stamp.Store(unchecked(head + _oneLap), Ordering.Release);
                        return true;
                    }

                    head = h;
                    backoff.Spin();
                }
                else if (stamp == head)
                {
                    Interlocked.MemoryBarrier();
                    var tail = _tail.Load(Ordering.Relaxed);

                    if (tail == head)
                    {
                        item = default;
                        return false;
                    }

                    backoff.Spin();
                    head = _head.Load(Ordering.Relaxed);
                }
                else
                {
                    backoff.Snooze();
                    head = _head.Load(Ordering.Relaxed);
                }
            }
        }

        /// <summary>
        ///     Gets a value that indicates whether this is empty.
        /// </summary>
        /// <value>true if this is empty; otherwise, false.</value>
        /// <remarks>
        ///     For determining whether the collection contains any items, use of this property is recommended
        ///     rather than retrieving the number of items from the <see cref="Count" /> property and comparing it to 0.
        ///     However, as this collection is intended to be accessed concurrently, it may be the case that another thread will
        ///     modify the collection after <see cref="IsEmpty" /> returns, thus invalidating the result.
        /// </remarks>
        public bool IsEmpty()
        {
            var head = _head.Load(Ordering.SeqCst);
            var tail = _tail.Load(Ordering.SeqCst);

            return tail == head;
        }

        /// <summary>
        ///     Returns <see langword="true" /> if the queue is full.
        /// </summary>
        public bool IsFull()
        {
            var tail = _tail.Load(Ordering.SeqCst);
            var head = _head.Load(Ordering.SeqCst);

            return unchecked(head + _oneLap) == tail;
        }

        /// <summary>
        ///     Gets the number of elements contained in this.
        /// </summary>
        /// <value>The number of elements contained in this.</value>
        /// <remarks>
        ///     For determining whether the collection contains any items, use of the <see cref="IsEmpty" />
        ///     property is recommended rather than retrieving the number of items from the <see cref="Count" />
        ///     property and comparing it to 0.
        /// </remarks>
        public nuint Count()
        {
            while (true)
            {
                var tail = _tail.Load(Ordering.SeqCst);
                var head = _head.Load(Ordering.SeqCst);

                if (_tail.Load(Ordering.SeqCst) == tail)
                {
                    var hix = head & (_oneLap - 1);
                    var tix = tail & (_oneLap - 1);

                    if (hix < tix)
                        return tix - hix;

                    if (hix > tix)
                        return Capacity() - hix + tix;

                    if (tail == head)
                        return 0;

                    return Capacity();
                }
            }
        }

        /// <summary>
        ///     Gets the capacity of the queue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly nuint Capacity() => (nuint)_buffer.Length;

        /// <summary>
        ///     A slot in a queue.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Slot
        {
            /// <summary>
            ///     The current stamp.
            ///     <br />
            ///     If the stamp equals the tail, this node will be next written to. If it equals head + 1,
            ///     this node will be next read from.
            /// </summary>
            public UnsafeAtomicUsize Stamp;

            /// <summary>
            ///     The value in this slot.
            /// </summary>
            public T? Value;
        }
    }
}