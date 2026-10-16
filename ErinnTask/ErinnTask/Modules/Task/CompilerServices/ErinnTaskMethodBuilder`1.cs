using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Builds an async operation whose state machine and continuation are pooled after its first suspension.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>The builder is intended for compiler-generated use.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Synchronous success stores <typeparamref name="T" /> inline. The first suspension rents a
    ///                 typed runner; synchronous failure rents a completion source so the exception remains awaitable.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>The state machine resumes inline on the thread selected by the awaited operation.</description>
    ///         </item>
    ///         <item>
    ///             <description>The builder does not capture an execution or synchronization context.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct ErinnTaskMethodBuilder<T>
    {
        /// <summary>
        ///     The pooled source allocated on suspension or synchronous failure.
        /// </summary>
        private IErinnTaskSource<T>? _source;

        /// <summary>
        ///     The inline result of a method that completes without suspending.
        /// </summary>
        private T _result;

        /// <summary>
        ///     Gets the operation returned to the caller of the async method. Before suspension this is an inline
        ///     completed result; after suspension or failure it references the runner/source and its current version.
        /// </summary>
        public ErinnTask<T> Task => _source == null ? new ErinnTask<T>(_result) : new ErinnTask<T>(_source);

        /// <summary>
        ///     Creates a builder without allocating completion storage.
        /// </summary>
        /// <returns>A new builder.</returns>
        public static ErinnTaskMethodBuilder<T> Create() => default;

        /// <summary>
        ///     Runs the state machine synchronously until its first incomplete await or until it completes. The initial
        ///     invocation uses the state machine by reference and does not box it. The compiler-generated state machine
        ///     catches method exceptions and reports them through <see cref="SetException" />.
        /// </summary>
        /// <param name="stateMachine">The state machine to start.</param>
        /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => stateMachine.MoveNext();

        /// <summary>
        ///     Accepts the compiler's state machine notification without installing the supplied boxed state machine.
        ///     On suspension, the typed runner retains the state machine by value through
        ///     <see cref="AwaitUnsafeOnCompleted{TAwaiter, TStateMachine}(ref TAwaiter, ref TStateMachine)" /> or
        ///     <see cref="AwaitOnCompleted{TAwaiter, TStateMachine}(ref TAwaiter, ref TStateMachine)" />.
        /// </summary>
        /// <param name="stateMachine">The state machine reported by the compiler.</param>
        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
        }

        /// <summary>
        ///     Completes the async operation with the specified result.
        /// </summary>
        /// <param name="result">The result of the operation.</param>
        public void SetResult(T result)
        {
            var source = _source;
            if (source == null)
                _result = result;
            else
                source.SetResult(result);
        }

        /// <summary>
        ///     Completes the async operation with the specified exception. If the method never suspended, this rents
        ///     completion storage so the returned operation can carry the failure; otherwise the existing runner source
        ///     stores it and invokes any registered continuation inline.
        /// </summary>
        /// <param name="exception">The non-null exception thrown by the async method.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="exception" /> is null.</exception>
        public void SetException(Exception exception)
        {
            var source = _source ??= ErinnTaskCompletionSource<T>.Rent();
            source.SetException(exception);
        }

        /// <summary>
        ///     Registers the pooled state-machine continuation through the awaiter's context-capturing
        ///     <see cref="INotifyCompletion.OnCompleted" /> method. The awaiter determines where and when the
        ///     continuation runs.
        /// </summary>
        /// <param name="awaiter">The awaiter that supplies the completion signal.</param>
        /// <param name="stateMachine">The state machine to resume.</param>
        /// <typeparam name="TAwaiter">The type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine => awaiter.OnCompleted(GetContinuation(ref stateMachine));

        /// <summary>
        ///     Registers the pooled state-machine continuation through the awaiter's
        ///     <see cref="ICriticalNotifyCompletion.UnsafeOnCompleted" /> method. This builder path does not request
        ///     execution-context capture; scheduling remains controlled by the awaiter.
        /// </summary>
        /// <param name="awaiter">The awaiter that supplies the completion signal.</param>
        /// <param name="stateMachine">The state machine to resume.</param>
        /// <typeparam name="TAwaiter">The type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine => awaiter.UnsafeOnCompleted(GetContinuation(ref stateMachine));

        /// <summary>
        ///     On the first incomplete await, rents a typed runner, connects the builder, copies the state machine, and
        ///     gets the runner's cached delegate. Later awaits reuse the same runner and delegate.
        /// </summary>
        /// <param name="stateMachine">The state machine to store on its first suspension.</param>
        /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
        /// <returns>The continuation that resumes the stored state machine.</returns>
        private Action GetContinuation<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            if (_source == null)
            {
                var source = ErinnTaskRunner<TStateMachine, T>.Rent();
                _source = source;
                source.SetStateMachine(ref stateMachine);
            }

            return ((ErinnTaskRunner<TStateMachine, T>)_source).Continuation;
        }
    }
}