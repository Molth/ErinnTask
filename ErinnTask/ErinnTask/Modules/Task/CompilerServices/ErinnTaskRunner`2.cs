using System;
using System.Runtime.CompilerServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Stores an async state machine without boxing it and reuses its continuation delegate.
    /// </summary>
    /// <typeparam name="TStateMachine">The type of the async state machine.</typeparam>
    /// <typeparam name="T">The type of the result.</typeparam>
    internal sealed class ErinnTaskRunner<TStateMachine, T> : IErinnTaskSource<T> where TStateMachine : IAsyncStateMachine
    {
        /// <summary>
        ///     The bounded queue of available runners shared by this closed runner type.
        /// </summary>
        private static readonly ErinnArrayQueue<ErinnTaskRunner<TStateMachine, T>> Pool = new(ErinnTaskPool.MAX_POOLED_INSTANCES);

        /// <summary>
        ///     The lifecycle state and continuation shared by each use of this runner.
        /// </summary>
        private ErinnTaskSourceLifecycle _lifecycle;

        /// <summary>
        ///     The result supplied when the async method completes successfully.
        /// </summary>
        private T _result;

        /// <summary>
        ///     The state machine stored with its original type.
        /// </summary>
        private TStateMachine _stateMachine;

        /// <summary>
        ///     The cached delegate that resumes the stored state machine.
        /// </summary>
        private readonly Action _stateMachineContinuation;

        /// <summary>
        ///     Initializes a runner that can only be obtained through <see cref="Rent" />.
        /// </summary>
        private ErinnTaskRunner()
        {
            _lifecycle = new ErinnTaskSourceLifecycle(ErinnTaskSourceState.Consumed | ErinnTaskSourceState.Released);
            _result = default!;
            _stateMachine = default!;
            _stateMachineContinuation = Run;
        }

        /// <summary>
        ///     Gets the continuation that resumes the stored state machine.
        /// </summary>
        public Action Continuation => _stateMachineContinuation;

        /// <summary>
        ///     Gets the version that identifies the current use of this runner.
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
        ///     Gets the completed result, consumes the operation, and returns the
        ///     runner when no call is active.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the operation is pending, its version is
        ///     stale, or it has already been consumed or released.
        /// </exception>
        /// <exception cref="Exception">
        ///     Rethrows the captured exception if the operation completed
        ///     with a failure.
        /// </exception>
        public T GetResult(ulong version)
        {
            var exception = _lifecycle.Consume(version, ref _result, out var result);
            TryRelease();

            if (exception != null)
                ThrowHelpers.RethrowDispatchInfo(exception);
            return result;
        }

        /// <summary>
        ///     Completes the pending operation with the specified result, publishes completion,
        ///     and invokes a registered continuation inline. After consumption and after
        ///     active state-machine or callback calls have returned, the runner is
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
        ///     publishes completion, and invokes a registered continuation inline. After
        ///     consumption and after active state-machine or callback calls have returned, the runner
        ///     is offered to its shared bounded queue if the queue has capacity.
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
        ///     Gets an available runner, allocating only when the pool is empty.
        /// </summary>
        /// <returns>A runner prepared for one async operation.</returns>
        public static ErinnTaskRunner<TStateMachine, T> Rent()
        {
            if (!Pool.TryDequeue(out var runner))
                runner = new ErinnTaskRunner<TStateMachine, T>();

            runner!._lifecycle.Prepare();
            return runner;
        }

        /// <summary>
        ///     Copies the state machine after its builder has been connected to this runner.
        /// </summary>
        /// <param name="stateMachine">The state machine to store without boxing.</param>
        public void SetStateMachine(ref TStateMachine stateMachine) => _stateMachine = stateMachine;

        /// <summary>
        ///     Resumes the stored state machine and postpones pooling until its call
        ///     has returned. The continuation runs on whichever thread invoked it.
        /// </summary>
        private void Run()
        {
            _lifecycle.EnterExecution();
            try
            {
                _stateMachine.MoveNext();
            }
            finally
            {
                if (_lifecycle.ExitExecution())
                    TryRelease();
            }
        }

        /// <summary>
        ///     Claims release, clears typed operation data, and returns
        ///     this runner to the shared bounded queue when it has capacity.
        /// </summary>
        private void TryRelease()
        {
            if (!_lifecycle.TryBeginRelease())
                return;

            _result = default!;
            _stateMachine = default!;
            Pool.TryEnqueue(this);
        }

        /// <summary>
        ///     Publishes completion and returns the runner to its
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