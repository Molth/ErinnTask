using System;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Defines the operations required to expose a pooled result source
    ///     through <see cref="ErinnTask{T}" />.
    /// </summary>
    /// <typeparam name="T">The result type supplied by the operation.</typeparam>
    /// <remarks>
    ///     Implementations own their lifecycle and result data.
    ///     <list type="bullet">
    ///         <item>
    ///             <description>Completion may race with one continuation registration.</description>
    ///         </item>
    ///         <item>
    ///             <description>A source-backed operation may be consumed only once.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Multiple continuation registrations for one operation are
    ///                 unsupported.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Continuations run inline on the completing thread, or on the
    ///                 registering thread if completion
    ///                 already occurred.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    internal interface IErinnTaskSource<T>
    {
        /// <summary>
        ///     Gets the version that identifies the current use of this pooled source.
        /// </summary>
        ulong Version { get; }

        /// <summary>
        ///     Gets whether the identified operation has completed.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <returns>
        ///     <see langword="true" /> if the operation has completed; otherwise
        ///     <see langword="false" />.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the version is stale or the operation was
        ///     consumed or released.
        /// </exception>
        bool IsCompleted(ulong version);

        /// <summary>
        ///     Registers one continuation. If completion has already been
        ///     published, invokes it inline on this thread; otherwise,
        ///     invokes it inline on the thread that later publishes completion.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <param name="continuation">The non-null continuation to invoke.</param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the version is stale, the operation was consumed, or a
        ///     continuation was already registered.
        /// </exception>
        /// <exception cref="Exception">
        ///     Propagates an exception thrown by <paramref name="continuation" />
        ///     when completion was already published and this call invokes it inline.
        /// </exception>
        void OnCompleted(ulong version, Action continuation);

        /// <summary>
        ///     Gets the result and consumes the identified operation.
        /// </summary>
        /// <param name="version">The version that identifies the operation.</param>
        /// <returns>The result supplied when the operation completed.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the version is stale, consumed, released, or
        ///     still pending.
        /// </exception>
        /// <exception cref="Exception">Rethrows the captured exception if the operation faulted.</exception>
        T GetResult(ulong version);

        /// <summary>
        ///     Completes the operation with a result and invokes a registered continuation inline.
        /// </summary>
        /// <param name="result">The result of the operation.</param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the operation is no longer pending.
        /// </exception>
        /// <exception cref="Exception">
        ///     Propagates an exception thrown by a registered continuation.
        /// </exception>
        void SetResult(T result);

        /// <summary>
        ///     Completes the operation with an exception and invokes a registered continuation inline.
        /// </summary>
        /// <param name="exception">The non-null exception thrown by the operation.</param>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the operation is no longer pending.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="exception" /> is null.
        /// </exception>
        /// <exception cref="Exception">
        ///     Propagates an exception thrown by a registered continuation.
        /// </exception>
        void SetException(Exception exception);
    }
}