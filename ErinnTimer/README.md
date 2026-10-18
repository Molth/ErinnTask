# ErinnTimer

[![NuGet](https://img.shields.io/nuget/v/ErinnTimer.svg?style=flat-square)](https://www.nuget.org/packages/ErinnTimer/)

ErinnTimer is a caller-driven scheduler for one-shot callbacks. It does not own a clock, choose a
time unit, calculate delays, create a background thread, or wake itself when a deadline passes.

## Basic use

Schedule an absolute timestamp and advance the same time domain by calling `Update`:

```csharp
using System;
using Erinn;

using var scheduler = new ErinnTimerScheduler();
const ulong now = 500;
const ulong deadline = 750;

scheduler.Schedule(deadline, static (tag, label) => Console.WriteLine($"{label}: {tag}"), "deadline");

scheduler.Update(now);      // The timer remains pending.
scheduler.Update(deadline); // The callback receives ErinnTimerTag.Ok.
```

`Schedule<T>` accepts any `ulong` timestamp and a non-null callback of type
`Action<ErinnTimerTag, T?>`. The scheduler treats the timestamp as an absolute value; it does not
interpret the unit or compare it with a clock. Use the same time origin and unit when scheduling
and updating. Every value from zero through `ulong.MaxValue` is accepted. Calls to `Update` need
not be monotonic: each call runs timers whose timestamp is at most its argument.

Passing a null callback throws `ArgumentNullException`. Scheduling after disposal throws
`ObjectDisposedException`; assigning every non-zero timer uid throws `InvalidOperationException`.

The callback receives the payload unchanged. Reference-type payloads may be null. Scheduling does
not invoke the callback; even a timer at timestamp zero waits for an update, cancellation, or
disposal.

## Ordering and callbacks

Timers run in ascending timestamp order. Timers with equal timestamps run in scheduling order.
A callback may schedule another timer that is already due; it can run before the same `Update`
call returns. Before a callback runs, its queue node is removed and its callback entry is cleared.
The callback may therefore re-enter the scheduler without leaving the current timer pending.

Callbacks run synchronously on the thread that calls `Update`, cancels a handle, or disposes the
scheduler. An `async` lambda passed as a callback becomes `async void`; the scheduler cannot await
its work or report exceptions raised later by that asynchronous work through `OnException`.
Keep callbacks synchronous and explicitly observe any separate asynchronous operation.

## Completion and cancellation

Each successfully scheduled timer invokes its callback once with one terminal tag:

| Tag                      | Meaning                                                | Trigger   |
|--------------------------|--------------------------------------------------------|-----------|
| `ErinnTimerTag.Ok`       | The update timestamp reached or passed the deadline.   | `Update`  |
| `ErinnTimerTag.Canceled` | The timer was pending when its handle canceled it.     | `Cancel`  |
| `ErinnTimerTag.Disposed` | The timer was pending when its scheduler was disposed. | `Dispose` |

`ErinnTimerHandle.Cancel()` returns `true` when it removed a pending timer and callback handling
returned normally. It returns `false` for a default handle, a completed timer, or a timer whose
scheduler has been disposed. If the callback or configured exception handler throws, the exception
propagates and no Boolean result is returned; the timer remains removed.

Handles are immutable value copies. They identify an entry by its scheduler and timer uid, and
cancellation does not clear a handle. `IsCreated` reports that a scheduler issued the handle; it
does not report whether the timer is pending and stays `true` after completion or disposal.
Repeated cancellation returns `false` after the first successful cancellation. A default handle
has no scheduler and cannot cancel a timer.

## Exception handling

Set `OnException` to receive callback exceptions. The current handler is read for each report, so
changing it affects later failures. Setting it to null restores propagation from `Update` and
cancellation.

- If the handler returns normally, `Update` continues with the remaining due timers.
- If no handler is set, a callback exception propagates from `Update` or cancellation and stops
  that operation. The callback's timer is already removed; later due timers remain queued.
- If the handler throws, its exception propagates from `Update` or cancellation and stops that
  operation. The callback's timer is already removed; later due timers remain queued.
- During disposal, draining continues. Callback exceptions without a handler and exceptions thrown
  by the handler are collected into an `AggregateException` after cleanup. Callback exceptions
  handled by a handler that returns normally are not added to the aggregate.

Disposal clears the `OnException` reference after draining. If it throws an `AggregateException`,
cleanup is complete; repeated calls to `Dispose` are no-ops.

## Disposal and reentrancy

Disposal marks the scheduler closed before invoking pending callbacks. Each pending timer completes
with `ErinnTimerTag.Disposed`, then queue and bucket storage are released. A callback invoked during
this drain cannot schedule another timer on the same scheduler. Reentrant and later `Update` calls
are ignored, and `Cancel` returns `false` once disposal starts.

Before disposal, callbacks may schedule or cancel other timers and call `Update` again. The nested
call runs immediately with its own timestamp. Bound callback chains to avoid unbounded work on the
current call stack.

## Threading and time arithmetic

Each scheduler is unsynchronized and must be accessed only from its owning thread. Create,
schedule, update, cancel, change `OnException`, and dispose it on that thread. `ThreadAffine`
annotations are metadata only; they do not check thread ownership at runtime or provide
synchronization.

The application chooses a clock and computes timestamps. Keep the origin and unit consistent; do
not mix frame counts, milliseconds, microseconds, or clock ticks without explicit conversion.
Check `ulong` addition before computing `deadline = now + delay`, because overflow wraps the
deadline to a value near zero. Convert signed clock values only after confirming they are
non-negative.

## Storage and allocations

Payloads are stored without boxing. Reference types share a reference bucket. Reference-free value
types up to four bytes share a small-value bucket; those larger than four and up to eight bytes
share a wide-value bucket. Other value types use a typed bucket owned by the scheduler.

The scheduler can avoid allocations on common warmed paths, but does not guarantee that every
call is allocation-free. Scheduler creation, first use of a payload type, callback delegate
creation, queue or bucket growth, and exception handling may allocate. Measure the actual call
pattern before making performance or garbage-collection claims.

## License

ErinnTimer is distributed under the MIT License.
