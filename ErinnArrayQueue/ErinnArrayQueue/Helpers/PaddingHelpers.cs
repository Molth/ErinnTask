using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Provides cache padding types and the assumed cache line size.
    /// </summary>
    internal static class PaddingHelpers
    {
        /// <summary>
        ///     The assumed cache line size, in bytes.
        /// </summary>
        /// <remarks>
        ///     The Rust source assumes the following cache line sizes by architecture:
        ///     <list type="bullet">
        ///         <item>
        ///             <description>x86-64, aarch64, and powerpc64: 128 bytes.</description>
        ///         </item>
        ///         <item>
        ///             <description>arm, mips, mips64, sparc, and hexagon: 32 bytes.</description>
        ///         </item>
        ///         <item>
        ///             <description>m68k: 16 bytes.</description>
        ///         </item>
        ///         <item>
        ///             <description>s390x: 256 bytes.</description>
        ///         </item>
        ///         <item>
        ///             <description>All other architectures: 64 bytes.</description>
        ///         </item>
        ///     </list>
        ///     This C# implementation uses 128 bytes on every target. The size is a reasonable guess and
        ///     is not guaranteed to match the actual cache line length of the machine. On modern Intel
        ///     architectures, the spatial prefetcher pulls pairs of 64-byte cache lines at a time, so
        ///     128 bytes are assumed.
        /// </remarks>
        public const int CACHE_LINE_SIZE = 128;

        /// <summary>
        ///     Provides a fixed-size padding region.
        /// </summary>
        /// <remarks>
        ///     In concurrent programming, commonly accessed pieces of data may need to be kept from
        ///     sharing a cache line. Updating an atomic value invalidates its whole cache line, which
        ///     makes the next access to that line slower for other CPU cores. Padding can help keep an
        ///     update to one piece of data from invalidating other cached data. This type reserves
        ///     <see cref="CACHE_LINE_SIZE" /> bytes but does not guarantee cache-line address alignment.
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, Size = CACHE_LINE_SIZE)]
        public readonly struct CachePadding
        {
            /// <summary>
            ///     The placeholder byte stored by this padding structure.
            /// </summary>
            private readonly byte _dummy;
        }
    }
}