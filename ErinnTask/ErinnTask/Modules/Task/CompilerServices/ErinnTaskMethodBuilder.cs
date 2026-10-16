using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Builds an async operation that does not return a value.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>The builder is intended for compiler-generated use.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 It stores synchronous success inline and delegates suspension, fault storage, continuation
    ///                 execution, and pooling to <see cref="ErinnTaskMethodBuilder{T}" />.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>It does not capture an execution or synchronization context.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct ErinnTaskMethodBuilder
    {
        /// <summary>
        ///     The builder that supplies the completion signal.
        /// </summary>
        private ErinnTaskMethodBuilder<ErinnTaskUnit> _builder;

        /// <summary>
        ///     Gets the operation returned to the caller of the async method.
        /// </summary>
        public ErinnTask Task => new(_builder.Task);

        /// <summary>
        ///     Creates a builder without allocating completion storage.
        /// </summary>
        /// <returns>A new builder.</returns>
        public static ErinnTaskMethodBuilder Create() => default;

        /// <summary>
        ///     Runs the state machine synchronously until its first incomplete await or until it completes. Exceptions
        ///     thrown by the state machine are handled through <see cref="SetException" /> by the generated code.
        /// </summary>
        /// <param name="stateMachine">The state machine to start.</param>
        /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => _builder.Start(ref stateMachine);

        /// <summary>
        ///     Accepts the compiler's state machine notification. The builder uses its typed runner to retain the state
        ///     machine, so this implementation does not install the supplied boxed state machine.
        /// </summary>
        /// <param name="stateMachine">The state machine reported by the compiler.</param>
        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);

        /// <summary>
        ///     Completes the async operation successfully.
        /// </summary>
        public void SetResult() => _builder.SetResult(default);

        /// <summary>
        ///     Completes the async operation with the specified non-null exception. If the method never suspended, the
        ///     generic builder rents completion storage so the returned operation can carry the failure.
        /// </summary>
        /// <param name="exception">The non-null exception thrown by the async method.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="exception" /> is null.</exception>
        public void SetException(Exception exception) => _builder.SetException(exception);

        /// <summary>
        ///     Registers the state-machine continuation with an awaiter that implements
        ///     <see cref="INotifyCompletion" />. Context-capture behavior is determined by that awaiter's
        ///     <see cref="INotifyCompletion.OnCompleted" /> implementation.
        /// </summary>
        /// <param name="awaiter">The awaiter that supplies the completion signal.</param>
        /// <param name="stateMachine">The state machine to resume.</param>
        /// <typeparam name="TAwaiter">The type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine => _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);

        /// <summary>
        ///     Registers the state-machine continuation through <see cref="ICriticalNotifyCompletion.UnsafeOnCompleted" />.
        ///     This path does not request execution-context capture; resumption still follows the awaiter's behavior.
        /// </summary>
        /// <param name="awaiter">The awaiter that supplies the completion signal.</param>
        /// <param name="stateMachine">The state machine to resume.</param>
        /// <typeparam name="TAwaiter">The type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }
}