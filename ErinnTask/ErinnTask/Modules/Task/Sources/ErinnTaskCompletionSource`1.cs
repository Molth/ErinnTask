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
    ///             <description>Each closed <typeparamref name="T" /> has a separate thread-local pool.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 A source may move between thread-local pools after completion and consumption happen on
    ///                 different threads.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>The pool is an unsynchronized array-backed stack accessed only by its owning thread.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    internal sealed class ErinnTaskCompletionSource<T> : IErinnTaskSource<T>
    {
        /// <summary>
        ///     The array-backed stack of available sources on the current thread.
        /// </summary>
        [ThreadStatic] private static ErinnTaskPool<ErinnTaskCompletionSource<T>>? _pool;

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
        /// <returns><see langword="true" /> if the operation has completed; otherwise <see langword="false" />.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if <paramref name="version" /> is stale or the operation was
        ///     consumed or released.
        /// </exception>
        public bool IsCompleted(ulong version) => _lifecycle.IsCompleted(version);

        /// <summary>
        ///     Registers the one continuation. If completion has already been published, invokes it inline on this
        ///     thread; otherwise, the completing thread invokes it inline when it publishes completion.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <param name="continuation">The non-null continuation to invoke.</param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the version is stale, the operation was consumed or released, or
        ///     a continuation is already registered.
        /// </exception>
        /// <exception cref="Exception">Propagates an exception thrown by the continuation when completion was already published.</exception>
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
        ///     Gets the completed result, consumes the operation, and returns the source to its pool when no call is active.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the version is stale, consumed, released, or still pending.</exception>
        /// <exception cref="Exception">Rethrows the captured exception if the operation faulted.</exception>
        public T GetResult(ulong version)
        {
            var exception = _lifecycle.Consume(version, ref _result, out var result);
            TryRelease();

            if (exception != null)
                ThrowHelpers.Throw(exception);
            return result;
        }

        /// <summary>
        ///     Completes the pending operation with the specified result, publishes completion, and invokes a registered
        ///     continuation inline. After consumption and after all active source calls have returned, the source is
        ///     returned to the thread-local pool of the thread that performs the final release.
        /// </summary>
        /// <param name="result">The result of the operation.</param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if completion has already started or the operation is no longer
        ///     pending.
        /// </exception>
        /// <exception cref="Exception">Propagates an exception thrown by a registered continuation.</exception>
        public void SetResult(T result)
        {
            _lifecycle.BeginCompletion();
            _result = result;
            CompleteAndRelease();
        }

        /// <summary>
        ///     Completes the pending operation with the specified exception, publishes completion, and invokes a
        ///     registered continuation inline. After consumption and after all active source calls have returned, the
        ///     source is returned to the thread-local pool of the thread that performs the final release.
        /// </summary>
        /// <param name="exception">The non-null exception thrown by the async method.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="exception" /> is null.</exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if completion has already started or the operation is no longer
        ///     pending.
        /// </exception>
        /// <exception cref="Exception">Propagates an exception thrown by a registered continuation.</exception>
        public void SetException(Exception exception)
        {
            _lifecycle.BeginExceptionCompletion(exception);
            CompleteAndRelease();
        }

        /// <summary>
        ///     Gets an available source, creating the current thread's pool on first use and allocating a source only
        ///     when that pool is empty. The returned source has an advanced version and is ready for exactly one operation.
        /// </summary>
        /// <returns>A source prepared for one operation.</returns>
        public static ErinnTaskCompletionSource<T> Rent()
        {
            var pool = GetPool();
            if (!pool.TryPop(out var source))
                source = new ErinnTaskCompletionSource<T>();

            source._lifecycle.Prepare();
            return source;
        }

        /// <summary>
        ///     Marks an operation as abandoned and releases its source when scheduling failed before the operation was
        ///     returned to a caller. Call this only for an unexposed source that has not begun completion.
        /// </summary>
        public void ReleaseUnscheduled()
        {
            _lifecycle.Abandon();
            TryRelease();
        }

        /// <summary>
        ///     Claims source release, clears its result, and returns it to the pool of the consuming thread.
        /// </summary>
        private void TryRelease()
        {
            if (!_lifecycle.TryBeginRelease())
                return;

            _result = default!;
            GetPool().Push(this);
        }

        /// <summary>
        ///     Publishes completion and returns the source to its pool if an inline continuation consumed the operation.
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

        /// <summary>
        ///     Gets the source pool for the calling thread, creating its initial array on first use.
        /// </summary>
        /// <returns>The pool local to the current thread.</returns>
        private static ErinnTaskPool<ErinnTaskCompletionSource<T>> GetPool() => _pool ??= new ErinnTaskPool<ErinnTaskCompletionSource<T>>();
    }
}