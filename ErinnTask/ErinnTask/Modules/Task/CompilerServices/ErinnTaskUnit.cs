using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Represents the completion signal of an operation that does not return a value.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ErinnTaskUnit
    {
    }
}