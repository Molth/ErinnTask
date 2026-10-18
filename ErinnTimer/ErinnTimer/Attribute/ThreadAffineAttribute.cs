using System;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Marks a method or property that must be accessed on the thread that owns the associated state.
    /// </summary>
    /// <remarks>
    ///     This attribute is metadata only. It does not perform runtime thread checks
    ///     or provide synchronization.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    internal sealed class ThreadAffineAttribute : Attribute
    {
    }
}