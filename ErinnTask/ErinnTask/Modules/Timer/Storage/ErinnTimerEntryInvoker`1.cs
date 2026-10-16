using System;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Invokes a type-erased user callback with the entry payload, recovering its original type from the storage
    ///     slot through which it was routed.
    /// </summary>
    /// <param name="callback">The type-erased user callback.</param>
    /// <param name="tag">The reason the callback is being invoked.</param>
    /// <param name="slot">The storage slot holding the routed payload.</param>
    /// <typeparam name="TSlot">The type of the storage slot the payload is routed through.</typeparam>
    /// <exception cref="Exception">Propagates an exception thrown by the user callback.</exception>
    internal delegate void ErinnTimerEntryInvoker<TSlot>(Delegate callback, ErinnTimerTag tag, TSlot? slot);
}