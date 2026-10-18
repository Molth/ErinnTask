// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Atomic memory orderings.
    /// </summary>
    /// <remarks>
    ///     Memory orderings specify the way atomic operations synchronize memory. In its weakest
    ///     <see cref="Relaxed" /> ordering, only the memory directly touched by the operation is
    ///     synchronized. A store-load pair of <see cref="SeqCst" /> operations synchronizes other
    ///     memory while additionally preserving a total order of such operations across all threads.
    ///     <br />
    ///     Memory orderings are the same as those of C++20:
    ///     <c>https://en.cppreference.com/w/cpp/atomic/memory_order</c>.
    ///     <br />
    ///     For more information, see the Rust Nomicon atomics chapter at
    ///     <c>../../../nomicon/atomics.html</c>.
    /// </remarks>
    internal enum Ordering
    {
        /// <summary>
        ///     No ordering constraints, only atomic operations.
        /// </summary>
        /// <remarks>Corresponds to <c>memory_order_relaxed</c> in C++20.</remarks>
        Relaxed,

        /// <summary>
        ///     When coupled with a store, all previous operations become ordered before any load of
        ///     this value with <see cref="Acquire" /> (or stronger) ordering. In particular, all previous
        ///     writes become visible to all threads that perform an <see cref="Acquire" /> (or stronger)
        ///     load of this value.
        /// </summary>
        /// <remarks>
        ///     Using this ordering for an operation that combines loads and stores leads to a
        ///     <see cref="Relaxed" /> load operation. This ordering applies only to operations that can
        ///     perform a store. It corresponds to <c>memory_order_release</c> in C++20:
        ///     <c>https://en.cppreference.com/w/cpp/atomic/memory_order#Release-Acquire_ordering</c>.
        /// </remarks>
        Release,

        /// <summary>
        ///     When coupled with a load, if the loaded value was written by a store operation with
        ///     <see cref="Release" /> (or stronger) ordering, then all subsequent operations become
        ///     ordered after that store. In particular, all subsequent loads will see data written
        ///     before the store.
        /// </summary>
        /// <remarks>
        ///     Using this ordering for an operation that combines loads and stores leads to a
        ///     <see cref="Relaxed" /> store operation. This ordering applies only to operations that can
        ///     perform a load. It corresponds to <c>memory_order_acquire</c> in C++20:
        ///     <c>https://en.cppreference.com/w/cpp/atomic/memory_order#Release-Acquire_ordering</c>.
        /// </remarks>
        Acquire,

        /// <summary>
        ///     Has the effects of both <see cref="Acquire" /> and <see cref="Release" /> together.
        ///     For loads it uses <see cref="Acquire" /> ordering; for stores it uses <see cref="Release" />
        ///     ordering.
        /// </summary>
        /// <remarks>
        ///     In the case of compare-exchange, it is possible that the operation ends up not performing
        ///     any store and hence has only <see cref="Acquire" /> ordering. However,
        ///     <see cref="AcqRel" /> will never perform <see cref="Relaxed" /> accesses. This ordering
        ///     applies only to operations that combine loads and stores. It corresponds to
        ///     <c>memory_order_acq_rel</c> in C++20:
        ///     <c>https://en.cppreference.com/w/cpp/atomic/memory_order#Release-Acquire_ordering</c>.
        /// </remarks>
        AcqRel,

        /// <summary>
        ///     Like <see cref="Acquire" />, <see cref="Release" />, and <see cref="AcqRel" /> for load,
        ///     store, and load-with-store operations, respectively, with the additional guarantee that
        ///     all threads see all sequentially consistent operations in the same order.
        /// </summary>
        /// <remarks>
        ///     Corresponds to <c>memory_order_seq_cst</c> in C++20:
        ///     <c>https://en.cppreference.com/w/cpp/atomic/memory_order#Sequentially-consistent_ordering</c>.
        /// </remarks>
        SeqCst
    }
}