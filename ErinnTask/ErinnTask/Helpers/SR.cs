// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Contains constant string resources used for exception messages and formatting.
    /// </summary>
    internal static class SR
    {
        /// <summary>
        ///     The message used when a required reference argument is null.
        /// </summary>
        public const string ArgumentNull_MustBeNotNull = "The argument must not be null.";

        /// <summary>
        ///     The message used when an argument must be non-negative.
        /// </summary>
        public const string ArgumentOutOfRange_MustBeNonNegative = "The argument must be non-negative.";

        /// <summary>
        ///     The message used when a timer timestamp cannot be represented in microseconds.
        /// </summary>
        public const string ArgumentOutOfRange_TimerTimestampOutOfRange = "The converted timestamp is outside the supported range.";

        /// <summary>
        ///     The message used when a task source already has a continuation.
        /// </summary>
        public const string InvalidOperation_TaskAlreadyHasContinuation = "The operation already has a continuation.";

        /// <summary>
        ///     The message used when a task source has been consumed or reused.
        /// </summary>
        public const string InvalidOperation_TaskConsumedOrReused = "The operation has already been consumed or its source has been reused.";

        /// <summary>
        ///     The message used when an operation is no longer pending.
        /// </summary>
        public const string InvalidOperation_TaskNoLongerPending = "The operation is no longer pending.";

        /// <summary>
        ///     The message used when a task result is requested before completion.
        /// </summary>
        public const string InvalidOperation_TaskNotCompleted = "The operation has not completed.";

        /// <summary>
        ///     The message used when a scheduler has exhausted its non-repeating timer uids.
        /// </summary>
        public const string InvalidOperation_TimerUidExhausted = "The scheduler has exhausted its timer uid range.";

        /// <summary>
        ///     The message used when a timer bucket entry changes during a single-threaded cancellation operation.
        /// </summary>
        public const string InvalidOperation_TimerEntryChangedDuringCancellation = "The timer entry changed during cancellation.";

        /// <summary>
        ///     The message used when a pooled task source has exhausted its non-repeating versions.
        /// </summary>
        public const string InvalidOperation_TaskVersionExhausted = "The task source has exhausted its version range.";
    }
}