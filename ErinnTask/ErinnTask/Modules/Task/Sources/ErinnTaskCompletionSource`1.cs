using System;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Supplies pooled completion storage for operations completed outside an async state machine.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Each closed <typeparamref name="T" /> has a separate queue shared by all
    ///                 threads.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 A source may be rented and returned by different threads when completion and
    ///                 consumption happen on different threads.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 The queue is thread-safe and retains at most
    ///                 <see cref="ErinnTaskPool.MAX_POOLED_INSTANCES" /> sources; a source returned while it is full is
    ///                 discarded.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    internal sealed class ErinnTaskCompletionSource<T> : IErinnTaskSource<T>
    {
        /// <summary>
        ///     The bounded queue of available sources shared by this closed result type.
        /// </summary>
        private static readonly ErinnArrayQueue<ErinnTaskCompletionSource<T>> Pool = new(ErinnTaskPool.MAX_POOLED_INSTANCES);

        /// <summary>
        ///     The lifecycle state and continuation shared by each use of this source.
        /// </summary>
        private ErinnTaskSourceLifecycle _lifecycle;

        /// <summary>
        ///     The result supplied when the operation completes successfully.
        /// </summary>
        private T _result;

        /// <summary>
        ///     Initializes completion storage that can only be obtained through <see cref="Rent" />.
        /// </summary>
        private ErinnTaskCompletionSource()
        {
            _lifecycle = new ErinnTaskSourceLifecycle(ErinnTaskSourceState.Consumed | ErinnTaskSourceState.Released);
            _result = default!;
        }

        /// <summary>
        ///     Gets the version that identifies the current use of this source.
        /// </summary>
        public ulong Version => _lifecycle.Version;

        /// <summary>
        ///     Gets whether the identified operation has completed.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <returns>
        ///     <see langword="true" /> if the operation has completed; otherwise
        ///     <see langword="false" />.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if <paramref name="version" /> is stale or the operation was
        ///     consumed or released.
        /// </exception>
        public bool IsCompleted(ulong version) => _lifecycle.IsCompleted(version);

        /// <summary>
        ///     Registers the one continuation. If completion has already been
        ///     published, invokes it inline on this
        ///     thread; otherwise, the completing thread invokes it inline when it publishes completion.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <param name="continuation">The non-null continuation to invoke.</param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the version is stale, the operation was
        ///     consumed or released, or a continuation is already registered.
        /// </exception>
        /// <exception cref="Exception">
        ///     Propagates an exception thrown by the continuation when completion was
        ///     already published.
        /// </exception>
        public void OnCompleted(ulong version, Action continuation)
        {
            try
            {
                _lifecycle.OnCompleted(version, continuation);
            }
            finally
            {
                TryRelease();
            }
        }

        /// <summary>
        ///     Gets the completed result, consumes the operation,
        ///     and returns the source to its pool when no call is active.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the version is stale, consumed, released, or
        ///     still pending.
        /// </exception>
        /// <exception cref="Exception">Rethrows the captured exception if the operation faulted.</exception>
        public T GetResult(ulong version)
        {
            var exception = _lifecycle.Consume(version, ref _result, out var result);
            TryRelease();

            if (exception != null)
                ThrowHelpers.RethrowDispatchInfo(exception);
            return result;
        }

        /// <summary>
        ///     Completes the pending operation with the specified result,
        ///     publishes completion, and invokes a registered continuation inline.
        ///     After consumption and after all active source calls have returned, the source is
        ///     offered to its shared bounded queue if the queue has capacity.
        /// </summary>
        /// <param name="result">The result of the operation.</param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if completion has already
        ///     started or the operation is no longer pending.
        /// </exception>
        /// <exception cref="Exception">
        ///     Propagates an exception thrown by a registered continuation.
        /// </exception>
        public void SetResult(T result)
        {
            _lifecycle.BeginCompletion();
            _result = result;
            CompleteAndRelease();
        }

        /// <summary>
        ///     Completes the pending operation with the specified exception,
        ///     publishes completion, and invokes a registered continuation inline.
        ///     After consumption and after all active source calls have returned, the source is
        ///     offered to its shared bounded queue if the queue has capacity.
        /// </summary>
        /// <param name="exception">The non-null exception thrown by the async method.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="exception" /> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if completion has already
        ///     started or the operation is no longer pending.
        /// </exception>
        /// <exception cref="Exception">
        ///     Propagates an exception thrown by a registered continuation.
        /// </exception>
        public void SetException(Exception exception)
        {
            _lifecycle.BeginExceptionCompletion(exception);
            CompleteAndRelease();
        }

        /// <summary>
        ///     Gets an available source from the shared queue, allocating one when the queue is
        ///     empty.
        ///     The returned source has an advanced version and is ready for exactly one operation.
        /// </summary>
        /// <returns>A source prepared for one operation.</returns>
        public static ErinnTaskCompletionSource<T> Rent()
        {
            if (!Pool.TryDequeue(out var source))
                source = new ErinnTaskCompletionSource<T>();

            source!._lifecycle.Prepare();
            return source;
        }

        /// <summary>
        ///     Marks an operation as abandoned and releases its source when scheduling
        ///     failed before the operation was returned to a caller.
        ///     Call this only for an unexposed source that has not begun completion.
        /// </summary>
        public void ReleaseUnscheduled()
        {
            _lifecycle.Abandon();
            TryRelease();
        }

        /// <summary>
        ///     Claims source release, clears its result, and returns it to the shared bounded queue
        ///     when it has capacity.
        /// </summary>
        private void TryRelease()
        {
            if (!_lifecycle.TryBeginRelease())
                return;

            _result = default!;
            Pool.TryEnqueue(this);
        }

        /// <summary>
        ///     Publishes completion and returns the source to its
        ///     pool if an inline continuation consumed the operation.
        /// </summary>
        private void CompleteAndRelease()
        {
            try
            {
                _lifecycle.Complete();
            }
            finally
            {
                TryRelease();
            }
        }
    }
}