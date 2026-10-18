// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Identifies messages used by task-related
    ///     <see cref="System.InvalidOperationException" /> exceptions.
    /// </summary>
    internal enum ExceptionResource
    {
        /// <summary>
        ///     The task source has assigned every non-zero version and cannot be
        ///     prepared for another operation.
        /// </summary>
        InvalidOperation_TaskVersionExhausted,

        /// <summary>
        ///     The operation already has a registered continuation.
        /// </summary>
        InvalidOperation_TaskAlreadyHasContinuation,

        /// <summary>
        ///     The operation was consumed or its pooled source was reused.
        /// </summary>
        InvalidOperation_TaskConsumedOrReused,

        /// <summary>
        ///     The operation is no longer pending.
        /// </summary>
        InvalidOperation_TaskNoLongerPending,

        /// <summary>
        ///     The operation result was requested before completion.
        /// </summary>
        InvalidOperation_TaskNotCompleted
    }
}