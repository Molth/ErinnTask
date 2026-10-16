using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Stores and coordinates lifecycle state shared by pooled task-source implementations.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Completion and continuation registration may race; atomic transitions invoke one continuation
    ///                 exactly once.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>Each operation supports one continuation registration and one result consumption.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Continuations execute inline on the thread that completes the operation, or registers after
    ///                 completion.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 The owning source remains responsible for clearing its typed data and returning itself to its
    ///                 pool.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    internal struct ErinnTaskSourceLifecycle
    {
        /// <summary>
        ///     The version that distinguishes this use from earlier uses of the same source.
        /// </summary>
        private ulong _version;

        /// <summary>
        ///     The atomic lifecycle flags for the current operation.
        /// </summary>
        private ErinnTaskSourceState _state;

        /// <summary>
        ///     The number of active callbacks or state-machine calls that must return before this source can be pooled.
        /// </summary>
        private int _executionDepth;

        /// <summary>
        ///     The exception reported by an async method, if one was thrown.
        /// </summary>
        private ExceptionDispatchInfo? _capturedException;

        /// <summary>
        ///     The continuation waiting for completion.
        /// </summary>
        private Action? _registeredContinuation;

        /// <summary>
        ///     Initializes lifecycle state with the supplied flags. Source owners pass consumed and released flags so
        ///     they must call <see cref="Prepare" /> before using the source.
        /// </summary>
        /// <param name="initialState">The initial lifecycle flags. This value is stored as supplied and is not validated.</param>
        public ErinnTaskSourceLifecycle(ErinnTaskSourceState initialState)
        {
            _version = 0;
            _state = initialState;
            _executionDepth = 0;
            _capturedException = null;
            _registeredContinuation = null;
        }

        /// <summary>
        ///     Gets the version that identifies the current use of this source.
        /// </summary>
        public ulong Version => Volatile.Read(ref _version);

        /// <summary>
        ///     Prepares a newly created or released source for another operation by advancing its version and making it
        ///     pending. The caller must not prepare a source that is still in use; this method does not validate that
        ///     precondition.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if this source has exhausted all non-zero versions.</exception>
        public void Prepare()
        {
            ThrowHelpers.ThrowIfTaskVersionExhausted(Version);
            Interlocked.Increment(ref Unsafe.As<ulong, long>(ref _version));
            WriteState(ErinnTaskSourceState.None);
        }

        /// <summary>
        ///     Checks whether the identified operation has published completion.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <returns><see langword="true" /> if the operation has completed; otherwise <see langword="false" />.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if <paramref name="version" /> is stale or the operation was
        ///     consumed or released.
        /// </exception>
        public bool IsCompleted(ulong version)
        {
            var state = Validate(version);
            return (state & ErinnTaskSourceState.Completed) != 0;
        }

        /// <summary>
        ///     Registers the one continuation for the identified operation. If completion has already been published,
        ///     invokes it inline on this thread; otherwise, the completing thread invokes it inline when it publishes
        ///     completion.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <param name="continuation">
        ///     The non-null continuation to invoke inline when completion permits. Null is not validated
        ///     and is unsupported.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the version is stale, the operation is consumed or released, or a
        ///     continuation is already registered.
        /// </exception>
        /// <remarks>The continuation is invoked immediately on the calling thread if completion was already published.</remarks>
        public void OnCompleted(ulong version, Action continuation)
        {
            Validate(version);
            if (Interlocked.CompareExchange(ref _registeredContinuation, continuation, null) != null)
                ThrowHelpers.ThrowInvalidOperation(SR.InvalidOperation_TaskAlreadyHasContinuation);

            while (true)
            {
                var state = ReadState();
                if ((state & (ErinnTaskSourceState.Consumed | ErinnTaskSourceState.Released | ErinnTaskSourceState.ContinuationRegistered)) != 0)
                {
                    Interlocked.CompareExchange(ref _registeredContinuation, null, continuation);
                    if ((state & (ErinnTaskSourceState.Consumed | ErinnTaskSourceState.Released)) != 0)
                        ThrowHelpers.ThrowInvalidOperation(SR.InvalidOperation_TaskConsumedOrReused);

                    ThrowHelpers.ThrowInvalidOperation(SR.InvalidOperation_TaskAlreadyHasContinuation);
                }

                if (CompareExchangeState(state | ErinnTaskSourceState.ContinuationRegistered, state))
                    break;
            }

            InvokeContinuationIfReady();
        }

        /// <summary>
        ///     Reserves the only completion transition for the current operation before its result or exception is
        ///     published.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if completion has already started, the operation has completed, or
        ///     it was consumed or released.
        /// </exception>
        public void BeginCompletion()
        {
            while (true)
            {
                var state = ReadState();
                if ((state & (ErinnTaskSourceState.Completing | ErinnTaskSourceState.Completed | ErinnTaskSourceState.Consumed | ErinnTaskSourceState.Released)) != 0)
                    ThrowHelpers.ThrowInvalidOperation(SR.InvalidOperation_TaskNoLongerPending);

                if (CompareExchangeState(state | ErinnTaskSourceState.Completing, state))
                    return;
            }
        }

        /// <summary>
        ///     Publishes the operation's result and invokes a registered continuation exactly once, if one has been
        ///     registered. The owner must call this after reserving completion and writing the typed result.
        /// </summary>
        /// <exception cref="Exception">Propagates an exception thrown by the registered continuation.</exception>
        public void Complete()
        {
            ErinnTaskSourceState completed;
            while (true)
            {
                var state = ReadState();
                completed = (state & ~ErinnTaskSourceState.Completing) | ErinnTaskSourceState.Completed;
                if (CompareExchangeState(completed, state))
                    break;
            }

            if ((completed & ErinnTaskSourceState.ContinuationRegistered) != 0)
                InvokeContinuationIfReady();
        }

        /// <summary>
        ///     Captures the operation exception and reserves the completion transition. The owner must pass a non-null
        ///     exception and call <see cref="Complete" /> after this method returns.
        /// </summary>
        /// <param name="exception">The non-null exception thrown by the async method.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown by <see cref="ExceptionDispatchInfo.Capture(Exception)" /> if
        ///     <paramref name="exception" /> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if this operation is no longer pending.</exception>
        public void BeginExceptionCompletion(Exception exception)
        {
            var captured = ExceptionDispatchInfo.Capture(exception);
            BeginCompletion();
            _capturedException = captured;
        }

        /// <summary>
        ///     Snapshots the completed result and exception before marking the identified operation consumed.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <param name="resultSource">The result field owned by the source.</param>
        /// <param name="result">When this method returns, contains the result captured before consumption.</param>
        /// <typeparam name="T">The type of the operation result.</typeparam>
        /// <returns>
        ///     The captured exception, or <see langword="null" /> after successful completion. Ignore
        ///     <paramref name="result" /> when an exception is returned.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the operation is stale, consumed, released, or still pending.</exception>
        public ExceptionDispatchInfo? Consume<T>(ulong version, ref T resultSource, out T result)
        {
            Validate(version);
            while (true)
            {
                var state = ReadState();
                if ((state & (ErinnTaskSourceState.Consumed | ErinnTaskSourceState.Released)) != 0)
                    ThrowHelpers.ThrowInvalidOperation(SR.InvalidOperation_TaskConsumedOrReused);

                if ((state & ErinnTaskSourceState.Completed) == 0)
                    ThrowHelpers.ThrowInvalidOperation(SR.InvalidOperation_TaskNotCompleted);

                var exception = _capturedException;
                result = resultSource;
                if (CompareExchangeState(state | ErinnTaskSourceState.Consumed, state))
                    return exception;
            }
        }

        /// <summary>
        ///     Marks an operation that was never exposed to a caller and has not begun completion as consumed. Repeated
        ///     calls and calls after release have no effect.
        /// </summary>
        public void Abandon()
        {
            while (true)
            {
                var state = ReadState();
                if ((state & (ErinnTaskSourceState.Consumed | ErinnTaskSourceState.Released)) != 0)
                    return;

                if (CompareExchangeState(state | ErinnTaskSourceState.Consumed, state))
                    return;
            }
        }

        /// <summary>
        ///     Increments the count of active source calls that prevent pool reuse. Pair each call with one
        ///     <see cref="ExitExecution" /> call after that source call has returned.
        /// </summary>
        public void EnterExecution() => Interlocked.Increment(ref _executionDepth);

        /// <summary>
        ///     Decrements the active-call count and reports whether the caller was the last active call. Call this once
        ///     for each matching <see cref="EnterExecution" />.
        /// </summary>
        /// <returns><see langword="true" /> if the active-call count reached zero; otherwise <see langword="false" />.</returns>
        public bool ExitExecution() => Interlocked.Decrement(ref _executionDepth) == 0;

        /// <summary>
        ///     Claims release only after consumption and after all active calls have returned, then clears references
        ///     shared by all source types.
        /// </summary>
        /// <returns><see langword="true" /> if the caller claimed release; otherwise <see langword="false" />.</returns>
        public bool TryBeginRelease()
        {
            if (Volatile.Read(ref _executionDepth) != 0)
                return false;

            while (true)
            {
                var state = ReadState();
                if ((state & (ErinnTaskSourceState.Consumed | ErinnTaskSourceState.Released)) != ErinnTaskSourceState.Consumed)
                    return false;

                if (CompareExchangeState(state | ErinnTaskSourceState.Released, state))
                    break;
            }

            _capturedException = null;
            _registeredContinuation = null;
            return true;
        }

        /// <summary>
        ///     Rejects a consumed handle or a handle from an earlier pooled use.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <returns>The current atomic state.</returns>
        private ErinnTaskSourceState Validate(ulong version)
        {
            var state = ReadState();
            if (Version != version || (state & (ErinnTaskSourceState.Consumed | ErinnTaskSourceState.Released)) != 0)
                ThrowHelpers.ThrowInvalidOperation(SR.InvalidOperation_TaskConsumedOrReused);

            return state;
        }

        /// <summary>
        ///     Claims and invokes the continuation after both registration and completion have been published.
        /// </summary>
        private void InvokeContinuationIfReady()
        {
            while (true)
            {
                var state = ReadState();
                if ((state & (ErinnTaskSourceState.Completed | ErinnTaskSourceState.ContinuationRegistered)) != (ErinnTaskSourceState.Completed | ErinnTaskSourceState.ContinuationRegistered) || (state & (ErinnTaskSourceState.ContinuationInvoked | ErinnTaskSourceState.Consumed | ErinnTaskSourceState.Released)) != 0)
                    return;

                if (CompareExchangeState(state | ErinnTaskSourceState.ContinuationInvoked, state))
                    break;
            }

            EnterExecution();
            var continuation = Interlocked.Exchange(ref _registeredContinuation, null)!;
            try
            {
                continuation();
            }
            finally
            {
                ExitExecution();
            }
        }

        /// <summary>
        ///     Reads the enum-backed state through the integer view required by atomic primitives.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ErinnTaskSourceState ReadState() => (ErinnTaskSourceState)Volatile.Read(ref Unsafe.As<ErinnTaskSourceState, int>(ref _state));

        /// <summary>
        ///     Atomically replaces the state when it still equals the expected flags.
        /// </summary>
        /// <param name="value">The flags to publish.</param>
        /// <param name="comparand">The flags expected to be present.</param>
        /// <returns><see langword="true" /> if the replacement succeeded; otherwise <see langword="false" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CompareExchangeState(ErinnTaskSourceState value, ErinnTaskSourceState comparand)
        {
            var comparandValue = (int)comparand;
            return Interlocked.CompareExchange(ref Unsafe.As<ErinnTaskSourceState, int>(ref _state), (int)value, comparandValue) == comparandValue;
        }

        /// <summary>
        ///     Publishes a state value with release ordering before the source is exposed to a caller.
        /// </summary>
        /// <param name="value">The lifecycle flags to publish.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteState(ErinnTaskSourceState value) => Volatile.Write(ref Unsafe.As<ErinnTaskSourceState, int>(ref _state), (int)value);
    }
}