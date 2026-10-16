// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Describes why a successfully scheduled timer callback was invoked. Its callback is invoked once with one
    ///     of these values: the timer reached its timestamp, it was canceled, or the owning scheduler was disposed.
    ///     The tag is a status value; it does not itself contain or rethrow callback exceptions.
    /// </summary>
    public enum ErinnTimerTag
    {
        /// <summary>
        ///     The timer reached its timestamp and was invoked by <see cref="ErinnTimerScheduler.Update" />.
        /// </summary>
        Success = 1,

        /// <summary>
        ///     The timer was canceled before it reached its timestamp and was invoked by
        ///     <see cref="ErinnTimerCancelTokenSource.Cancel" />.
        /// </summary>
        Canceled = 2,

        /// <summary>
        ///     The timer was still pending when the owning <see cref="ErinnTimerScheduler" /> was disposed and was
        ///     invoked while the pending timers were drained.
        /// </summary>
        Fault = 3
    }
}