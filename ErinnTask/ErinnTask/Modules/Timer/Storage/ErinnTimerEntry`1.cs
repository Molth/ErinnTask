using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Represents one timer slot. It keeps the callback type-erased with an invoker specialized for the original
    ///     payload type, while storing the payload in a slot of type <typeparamref name="TSlot" />. This lets a bucket
    ///     share storage across payload types routed through the same slot type without boxing the payload.
    /// </summary>
    /// <typeparam name="TSlot">The type of the storage slot the payload is routed through.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ErinnTimerEntry<TSlot>
    {
        /// <summary>
        ///     The unique id of the timer entry.
        /// </summary>
        private readonly ulong _uid;

        /// <summary>
        ///     The type-erased user callback (<c>Action&lt;ErinnTimerTag, T?&gt;</c>
        ///     for the payload type that allocated the slot).
        /// </summary>
        [BindingType(typeof(Action<,>))] private readonly Delegate? _callback;

        /// <summary>
        ///     The invoker that knows the payload type the callback consumes.
        /// </summary>
        private readonly ErinnTimerEntryInvoker<TSlot>? _invoker;

        /// <summary>
        ///     The payload, stored in its routed slot.
        /// </summary>
        private readonly TSlot? _slot;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ErinnTimerEntry{TSlot}" /> structure.
        /// </summary>
        /// <param name="uid">The unique id of the timer entry.</param>
        /// <param name="callback">The type-erased user callback.</param>
        /// <param name="invoker">The invoker that knows the payload type the callback consumes.</param>
        /// <param name="slot">The payload, stored in its routed slot.</param>
        private ErinnTimerEntry(ulong uid, Delegate callback, ErinnTimerEntryInvoker<TSlot> invoker, TSlot? slot)
        {
            _uid = uid;
            _callback = callback;
            _invoker = invoker;
            _slot = slot;
        }

        /// <summary>
        ///     Gets the unique id of the timer entry.
        /// </summary>
        public ulong Uid => _uid;

        /// <summary>
        ///     Creates an entry that stores the routed payload without boxing it.
        /// </summary>
        /// <param name="uid">The non-zero unique id of the timer entry.</param>
        /// <param name="callback">The non-null user callback to retain in the entry.</param>
        /// <param name="data">
        ///     The payload to route through <typeparamref name="TSlot" />; its representation must be compatible
        ///     with that slot type.
        /// </param>
        /// <typeparam name="T">The callback parameter type.</typeparam>
        /// <returns>The entry holding the payload and its typed invoker.</returns>
        public static ErinnTimerEntry<TSlot> Create<T>(ulong uid, Action<ErinnTimerTag, T?> callback, T? data)
        {
            TSlot? slot = default;
            Unsafe.As<TSlot?, T?>(ref slot) = data;
            return new ErinnTimerEntry<TSlot>(uid, callback, InvokerCache<T>.Invoker, slot);
        }

        /// <summary>
        ///     Invokes the original callback with the payload held by this entry. The callback's exception propagates to
        ///     the caller.
        /// </summary>
        /// <param name="tag">The reason the callback is being invoked.</param>
        [ThreadAffine]
        public void Invoke(ErinnTimerTag tag) => _invoker!(_callback!, tag, _slot);

        /// <summary>
        ///     Holds the callback invoker for one closed payload type and storage slot type.
        /// </summary>
        /// <typeparam name="T">The callback parameter type.</typeparam>
        private static class InvokerCache<T>
        {
            /// <summary>
            ///     The cached invoker that restores the payload's type before calling the callback.
            /// </summary>
            public static readonly ErinnTimerEntryInvoker<TSlot> Invoker;

            /// <summary>
            ///     Initializes the invoker once for this closed payload type and storage slot type.
            /// </summary>
            static InvokerCache() => Invoker = Invoke;

            /// <summary>
            ///     Restores the payload's type from the storage slot and invokes its typed callback.
            /// </summary>
            /// <param name="callback">The callback originally supplied for the payload type.</param>
            /// <param name="tag">The reason the callback is being invoked.</param>
            /// <param name="slot">The payload stored in the routed slot.</param>
            private static void Invoke(Delegate callback, ErinnTimerTag tag, TSlot? slot) => ((Action<ErinnTimerTag, T?>)callback)(tag, Unsafe.As<TSlot?, T?>(ref slot));
        }
    }
}