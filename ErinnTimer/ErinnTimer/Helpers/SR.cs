// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Contains constant strings used for exception messages.
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
        ///     The message used when a scheduler has exhausted its non-repeating timer uids.
        /// </summary>
        public const string InvalidOperation_TimerUidExhausted = "The scheduler has exhausted its timer uid range.";

        /// <summary>
        ///     The message used when a timer bucket entry changes during a
        ///     single-threaded cancellation operation.
        /// </summary>
        public const string InvalidOperation_TimerEntryChangedDuringCancellation = "The timer entry changed during cancellation.";
    }
}