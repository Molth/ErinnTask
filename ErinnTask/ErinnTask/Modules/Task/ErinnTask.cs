using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Represents an awaitable operation that does not return a value.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>A default value is a successfully completed operation.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Suspended async methods reuse pooled state machines and continuation delegates. After warm-up,
    ///                 successful operations within the warmed pool capacity can complete without allocating in a Release
    ///                 build.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 A suspended, source-backed operation is single-consumption. Await it exactly once and do not
    ///                 use another copy after it has been consumed.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Continuations may resume on another thread. Concurrent attempts to consume or register
    ///                 multiple continuations on the same operation are not supported.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Execution contexts and synchronization contexts are not captured. Continuations run inline on
    ///                 the thread that completes the awaited operation.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    [AsyncMethodBuilder(typeof(ErinnTaskMethodBuilder))]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ErinnTask
    {
        /// <summary>
        ///     The operation that supplies the completion signal.
        /// </summary>
        private readonly ErinnTask<ErinnTaskUnit> _task;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ErinnTask" /> structure.
        /// </summary>
        /// <param name="task">The operation that supplies the completion signal.</param>
        internal ErinnTask(ErinnTask<ErinnTaskUnit> task) => _task = task;

        /// <summary>
        ///     Gets a default operation that has already completed successfully.
        ///     Awaiting it completes synchronously and does not use pooled completion storage.
        /// </summary>
        public static ErinnTask CompletedTask => default;

        /// <summary>
        ///     Creates an operation that has already completed with the specified result. Awaiting it completes
        ///     synchronously and does not allocate completion storage, regardless of the result type.
        /// </summary>
        /// <param name="result">The result of the operation.</param>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <returns>An operation that supplies <paramref name="result" />.</returns>
        public static ErinnTask<T> FromResult<T>(T result) => new(result);

        /// <summary>
        ///     Gets the awaiter for this operation. This method is intended for compiler-generated <c>await</c>
        ///     expressions; manually consuming a source-backed operation still follows its single-await rule.
        /// </summary>
        /// <returns>The awaiter for this operation.</returns>
        public Awaiter GetAwaiter() => new(_task.GetAwaiter());

        /// <summary>
        ///     Represents an awaiter for an operation that does not return a value.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            /// <summary>
            ///     The awaiter that supplies the completion signal.
            /// </summary>
            private readonly ErinnTask<ErinnTaskUnit>.Awaiter _awaiter;

            /// <summary>
            ///     Initializes a new instance of the <see cref="Awaiter" /> structure.
            /// </summary>
            /// <param name="awaiter">The awaiter that supplies the completion signal.</param>
            internal Awaiter(ErinnTask<ErinnTaskUnit>.Awaiter awaiter) => _awaiter = awaiter;

            /// <summary>
            ///     Gets <see langword="true" /> if the operation is complete; otherwise <see langword="false" />. The
            ///     default awaiter is always complete; a source-backed awaiter reads the completion state for its
            ///     captured source version.
            /// </summary>
            /// <exception cref="InvalidOperationException">Thrown if the source version is stale or the operation was consumed.</exception>
            public bool IsCompleted => _awaiter.IsCompleted;

            /// <summary>
            ///     Observes completion and releases the pooled operation, if one was used.
            /// </summary>
            /// <exception cref="InvalidOperationException">
            ///     Thrown if the source-backed operation is pending, its source version is
            ///     stale, or it has already been consumed.
            /// </exception>
            /// <exception cref="Exception">Rethrows the stored exception if the operation completed with a failure.</exception>
            public void GetResult() => _awaiter.GetResult();

            /// <summary>
            ///     Registers the continuation to run when the operation completes. The continuation is invoked inline on
            ///     the completing thread and the execution context is not captured. Only one continuation can be registered
            ///     on a source-backed operation.
            /// </summary>
            /// <param name="continuation">The continuation to invoke.</param>
            /// <exception cref="ArgumentNullException">Thrown if <paramref name="continuation" /> is null.</exception>
            /// <exception cref="InvalidOperationException">
            ///     Thrown if the source version is stale or consumed, or a continuation is
            ///     already registered.
            /// </exception>
            public void OnCompleted(Action continuation) => _awaiter.OnCompleted(continuation);

            /// <summary>
            ///     Registers the continuation without capturing the execution context. For this implementation it has the
            ///     same completion and single-registration behavior as <see cref="OnCompleted" />.
            /// </summary>
            /// <param name="continuation">The continuation to invoke.</param>
            /// <exception cref="ArgumentNullException">Thrown if <paramref name="continuation" /> is null.</exception>
            /// <exception cref="InvalidOperationException">
            ///     Thrown if the source version is stale or consumed, or a continuation is
            ///     already registered.
            /// </exception>
            public void UnsafeOnCompleted(Action continuation) => _awaiter.UnsafeOnCompleted(continuation);
        }
    }
}