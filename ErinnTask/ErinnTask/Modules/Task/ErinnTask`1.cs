using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Represents an awaitable operation that returns a value without boxing the operation or its result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Synchronously completed operations store their result in the task value and use no completion
    ///                 source; awaiting such a task completes synchronously. The default value is already complete and
    ///                 supplies <see langword="default" /> for <typeparamref name="T" />.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Suspended or faulted operations use a pooled source. Copies share that source and its version,
    ///                 so consuming one copy invalidates every copy; await each source-backed operation exactly once.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Completion may race with continuation registration, and the continuation is invoked once.
    ///                 Concurrent attempts to consume the same operation or register multiple continuations are not
    ///                 supported.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Continuations run inline on the thread that completes the operation, or registers after it is
    ///                 complete; execution and synchronization contexts are not captured.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>Pool growth, Debug state machines, and exception paths can allocate.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    [AsyncMethodBuilder(typeof(ErinnTaskMethodBuilder<>))]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ErinnTask<T>
    {
        /// <summary>
        ///     The source of a suspended operation, or <see langword="null" /> for an inline result.
        /// </summary>
        private readonly IErinnTaskSource<T>? _source;

        /// <summary>
        ///     The version that identifies this use of the pooled source.
        /// </summary>
        private readonly ulong _version;

        /// <summary>
        ///     The inline result of a synchronously completed operation.
        /// </summary>
        private readonly T _result;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ErinnTask{T}" /> structure with an inline result.
        /// </summary>
        /// <param name="result">The result of the operation.</param>
        internal ErinnTask(T result)
        {
            _source = null;
            _version = 0;
            _result = result;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ErinnTask{T}" /> structure with a pooled source.
        /// </summary>
        /// <param name="source">The source of the suspended operation.</param>
        internal ErinnTask(IErinnTaskSource<T> source)
        {
            _source = source;
            _version = source.Version;
            _result = default!;
        }

        /// <summary>
        ///     Gets the awaiter for this operation. This method is intended for compiler-generated <c>await</c>
        ///     expressions; manually consuming a source-backed operation still follows its single-await rule.
        /// </summary>
        /// <returns>The awaiter for this operation.</returns>
        public Awaiter GetAwaiter() => new(this);

        /// <summary>
        ///     Represents an awaiter that consumes a result and returns its source to the pool.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            /// <summary>
            ///     The operation being awaited.
            /// </summary>
            private readonly ErinnTask<T> _task;

            /// <summary>
            ///     Initializes a new instance of the <see cref="Awaiter" /> structure.
            /// </summary>
            /// <param name="task">The operation being awaited.</param>
            internal Awaiter(ErinnTask<T> task) => _task = task;

            /// <summary>
            ///     Gets whether the operation is complete. Inline results are always complete; source-backed operations
            ///     query the captured source version. Pending operations return <see langword="false" />.
            /// </summary>
            /// <exception cref="InvalidOperationException">Thrown if the source version is stale or the operation was consumed.</exception>
            public bool IsCompleted => _task._source == null || _task._source.IsCompleted(_task._version);

            /// <summary>
            ///     Gets the result and releases the pooled source, if one was used.
            /// </summary>
            /// <returns>The result of the operation.</returns>
            /// <exception cref="InvalidOperationException">
            ///     Thrown if the operation is pending, its source version is stale, or it has
            ///     already been consumed.
            /// </exception>
            /// <exception cref="Exception">Rethrows the stored exception if the operation completed with a failure.</exception>
            public T GetResult() => _task._source == null ? _task._result : _task._source.GetResult(_task._version);

            /// <summary>
            ///     Registers the continuation to run inline when the operation completes. An inline result invokes it
            ///     immediately; a source-backed operation that has already completed invokes it immediately on the
            ///     calling thread. This method does not capture the execution context, and a source-backed operation
            ///     accepts only one continuation.
            /// </summary>
            /// <param name="continuation">The continuation to invoke.</param>
            /// <exception cref="ArgumentNullException">Thrown if <paramref name="continuation" /> is null.</exception>
            /// <exception cref="InvalidOperationException">
            ///     Thrown if the source version is stale or consumed, or a continuation is
            ///     already registered.
            /// </exception>
            public void OnCompleted(Action continuation) => UnsafeOnCompleted(continuation);

            /// <summary>
            ///     Registers the continuation without capturing the execution context. An inline result invokes it
            ///     immediately; a pending source-backed operation stores it until completion, while an already completed
            ///     source invokes it immediately. A source-backed operation accepts only one continuation registration.
            /// </summary>
            /// <param name="continuation">The continuation to invoke.</param>
            /// <exception cref="ArgumentNullException">Thrown if <paramref name="continuation" /> is null.</exception>
            /// <exception cref="InvalidOperationException">
            ///     Thrown if the source version is stale or consumed, or a continuation is
            ///     already registered.
            /// </exception>
            public void UnsafeOnCompleted(Action continuation)
            {
                ThrowHelpers.ThrowIfNull(continuation, ExceptionArgument.continuation);

                var source = _task._source;
                if (source == null)
                    continuation();
                else
                    source.OnCompleted(_task._version, continuation);
            }
        }
    }
}