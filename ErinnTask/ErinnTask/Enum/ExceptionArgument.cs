// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Represents parameter names used in exception messages for argument validation failures.
    /// </summary>
    internal enum ExceptionArgument
    {
        /// <summary>
        ///     The name of the callback parameter.
        /// </summary>
        callback,

        /// <summary>
        ///     The name of the capacity parameter.
        /// </summary>
        capacity,

        /// <summary>
        ///     The name of the continuation parameter.
        /// </summary>
        continuation,

        /// <summary>
        ///     The name of the scheduler parameter.
        /// </summary>
        scheduler,

        /// <summary>
        ///     The name of the timestamp parameter.
        /// </summary>
        timestamp,

        /// <summary>
        ///     A reserved sentinel that is not mapped to a parameter name by <see cref="ThrowHelpers" />.
        /// </summary>
        _dummy
    }
}