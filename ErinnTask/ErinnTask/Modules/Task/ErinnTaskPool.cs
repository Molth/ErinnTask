// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Defines the capacity used by each runner and completion-source queue.
    /// </summary>
    internal static class ErinnTaskPool
    {
        /// <summary>
        ///     The maximum number of returned instances retained by each queue.
        /// </summary>
        public const int MAX_POOLED_INSTANCES = 64;
    }
}