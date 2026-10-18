using System;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Represents the independently combinable lifecycle flags for one task source operation.
    /// </summary>
    [Flags]
    internal enum ErinnTaskSourceState
    {
        /// <summary>
        ///     No lifecycle transition has been published.
        /// </summary>
        None = 0,

        /// <summary>
        ///     A completion caller has reserved the source and is
        ///     preparing its result or exception for publication.
        /// </summary>
        Completing = 1,

        /// <summary>
        ///     The result or exception has been published.
        /// </summary>
        Completed = 2,

        /// <summary>
        ///     One continuation has been registered.
        /// </summary>
        ContinuationRegistered = 4,

        /// <summary>
        ///     The registered continuation has been claimed for invocation.
        /// </summary>
        ContinuationInvoked = 8,

        /// <summary>
        ///     The result has been consumed or the unexposed operation was abandoned.
        /// </summary>
        Consumed = 16,

        /// <summary>
        ///     Release has been claimed; the owner is clearing
        ///     operation data before returning the source to a pool.
        /// </summary>
        Released = 32
    }
}