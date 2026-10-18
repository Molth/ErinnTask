// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Identifies supported parameter names for argument exceptions raised by timer helpers.
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
        ///     A reserved sentinel that is not mapped to a parameter name by <see cref="ThrowHelpers" />.
        /// </summary>
        _dummy
    }
}