using System;

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
    [BindingType(typeof(ArrayQueue<>))]
    public sealed class ErinnArrayQueue<T>
    {
        /// <summary>
        ///     The internal queue implementation wrapped by this type.
        /// </summary>
        private ArrayQueue<T> _handle;

        /// <summary>
        ///     Creates a new bounded queue with the given capacity.
        /// </summary>
        /// <param name="capacity">The requested queue capacity.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="capacity" /> is less than or equal to zero.
        /// </exception>
        public ErinnArrayQueue(int capacity) => _handle = new ArrayQueue<T>(capacity);

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
        public bool IsEmpty => _handle.IsEmpty();

        /// <summary>
        ///     Returns <see langword="true" /> if the queue is full.
        /// </summary>
        public bool IsFull => _handle.IsFull();

        /// <summary>
        ///     Gets the number of elements contained in this.
        /// </summary>
        /// <value>The number of elements contained in this.</value>
        /// <remarks>
        ///     For determining whether the collection contains any items, use of the <see cref="IsEmpty" />
        ///     property is recommended rather than retrieving the number of items from the <see cref="Count" />
        ///     property and comparing it to 0.
        /// </remarks>
        public int Count => (int)_handle.Count();

        /// <summary>
        ///     Gets the capacity of the queue.
        /// </summary>
        public int Capacity => (int)_handle.Capacity();

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
        public bool TryEnqueue(T? item) => _handle.TryEnqueue(item);

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
        public InsertResult Enqueue(T? item, out T? overwritten) => _handle.Enqueue(item, out overwritten);

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
        public bool TryDequeue(out T? item) => _handle.TryDequeue(out item);
    }
}