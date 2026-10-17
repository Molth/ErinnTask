using System;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Provides the maximum single-dimensional, zero-based (SZ) array length used by collection growth logic.
    /// </summary>
    internal static class ArrayHelpers
    {
        /// <summary>
        ///     Gets the maximum number of elements that may be contained in a single-dimensional, zero-based (SZ) array.
        /// </summary>
        /// <returns>The maximum count of elements allowed in a single-dimensional, zero-based (SZ) array.</returns>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <description>
        ///                 This property reports the runtime limit on the number of elements, not bytes, in an array.
        ///                 An allocation below this limit can still fail; an allocation above it will fail.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 The limit applies only to single-dimensional, zero-bound (SZ) arrays.
        ///                 <see cref="Array.Length" /> may return a larger value for a multidimensional array.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        public static int MaxLength
        {
            get
            {
#if NET6_0_OR_GREATER
                return Array.MaxLength;
#else
                return 2147483591;
#endif
            }
        }
    }
}