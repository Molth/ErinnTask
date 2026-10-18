// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Identifies messages used by timer-related
    ///     <see cref="System.InvalidOperationException" /> exceptions.
    /// </summary>
    internal enum ExceptionResource
    {
        /// <summary>
        ///     The scheduler has assigned every non-zero timer uid and cannot schedule another timer.
        /// </summary>
        InvalidOperation_TimerUidExhausted,

        /// <summary>
        ///     A timer bucket entry no longer matches its queue node during cancellation.
        /// </summary>
        InvalidOperation_TimerEntryChangedDuringCancellation
    }
}