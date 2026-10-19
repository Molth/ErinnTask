# ErinnTask and ErinnTimer

[![NuGet](https://img.shields.io/nuget/v/ErinnTask.svg?style=flat-square)](https://www.nuget.org/packages/ErinnTask/)
[![NuGet](https://img.shields.io/nuget/v/ErinnTimer.svg?style=flat-square)](https://www.nuget.org/packages/ErinnTimer/)
[![NuGet](https://img.shields.io/nuget/v/crossbeam-queue.svg?style=flat-square)](https://www.nuget.org/packages/crossbeam-queue/)

This solution contains two .NET libraries. ErinnTimer provides a caller-driven timer scheduler.
ErinnTask provides custom awaitable types and timer integration for that scheduler. It uses the
`crossbeam-queue` NuGet package for the bounded concurrent queues used by its object pools.

| Project    | Main API                                                           | Project documentation          |
|------------|--------------------------------------------------------------------|--------------------------------|
| ErinnTimer | `ErinnTimerScheduler`, `ErinnTimerHandle`, `ErinnTimerTag`         | [README](ErinnTimer/README.md) |
| ErinnTask  | `ErinnTask`, `ErinnTask<T>`, timer extensions and microsecond view | [README](ErinnTask/README.md)  |

## ErinnTask

`ErinnTask` and `ErinnTask<T>` are custom awaitable types, not wrappers around `Task` or
`ValueTask`. An `async ErinnTask` method starts synchronously and continues until it returns or
reaches an incomplete await. The default `ErinnTask` is complete; the default `ErinnTask<T>` is
complete with `default(T)`. `CompletedTask` and `FromResult` keep their results inline.

Operations that suspend or fault use pooled sources. A source-backed operation can be consumed
once and accepts one continuation. Copies share the same source, so consuming one copy consumes
all of them. ErinnTask's own awaiter does not capture execution or synchronization contexts;
continuations run inline on the completing thread, or on the registering thread when completion
has already occurred. The awaiter for a value awaited inside an async method controls that value's
resumption behavior.

ErinnTask does not provide conversions to `Task` or `ValueTask`, `ConfigureAwait`, task
combinators, or built-in `CancellationToken` integration. Those types can still be awaited from an
`async ErinnTask` method when their own awaiters support it.

## ErinnTimer

`ErinnTimerScheduler` stores one-shot timers and invokes their callbacks only when the application
calls `Update`. It does not own a clock, choose a unit, create a background thread, or wake itself.
Raw timestamps are absolute `ulong` values: use one origin and unit for scheduling and every
update. Timers run by timestamp and then scheduling order for ties. A timestamp of zero is queued
normally and still requires an update, cancellation, or disposal to complete.

```csharp
using System;
using Erinn;

using var scheduler = new ErinnTimerScheduler();
const ulong now = 1_000_000;
const ulong deadline = 3_000_000;

scheduler.Schedule(deadline, static (tag, name) => Console.WriteLine($"{name}: {tag}"), "refresh");
scheduler.Update(now);      // The timer remains pending.
scheduler.Update(deadline); // The callback receives ErinnTimerTag.Ok.
```

Callbacks run inline on the thread that calls `Update`, `Cancel`, or `Dispose`. A callback may
schedule another timer that is already due, and it may run before the current update returns.
Cancellation invokes the callback with `Canceled`; disposal invokes still-pending callbacks with
`Disposed`. Both are result tags, not exceptions.

The scheduler is unsynchronized and must be accessed from one owning thread. Callback exceptions
are passed to `OnException` when configured. If the handler returns normally, an update continues;
if no handler is set, the exception propagates from `Update` or `Cancel`. Disposal drains all
pending timers, collects callback exceptions without a handler and exceptions thrown by the
handler, releases its storage, then throws an `AggregateException` when any failure occurred.

## Awaitable timers

The ErinnTask project adds `WaitUntil` extensions that return `ErinnTask<ErinnTimerTag>` and an
`ErinnTimerHandle` for the same timer entry. The scheduler still needs calls to `Update`:

```csharp
using Erinn;

static ErinnTask<ErinnTimerTag> CreateWait(ErinnTimerScheduler scheduler, ulong deadline)
{
    return scheduler.InMicroseconds().WaitUntil(deadline, out _);
}

static async ErinnTask<ErinnTimerTag> WaitForDeadlineAsync(ErinnTimerScheduler scheduler, ulong deadline)
{
    return await CreateWait(scheduler, deadline);
}
```

`InMicroseconds()` returns a non-owning `ref struct` view. Its `Schedule`, `Update`, and
`WaitUntil` methods use absolute microsecond timestamps. `WaitUntilMilliseconds` multiplies an
absolute millisecond timestamp by 1,000 and accepts values through `18446744073709551` inclusive.
`WaitUntilSeconds` accepts finite, numerically non-negative values below `18446744073709.55`;
negative zero is accepted as zero, and fractional microseconds are truncated toward zero. These
methods convert units, but do not calculate relative delays or read a clock.

An awaited timer completes with `Ok` when updated, `Canceled` when canceled, or `Disposed` when
the scheduler drains it. Each source-backed wait must be consumed once. Its continuation runs
inline on the thread that updates, cancels, or disposes the scheduler. A continuation exception
raised during timer completion follows the scheduler's callback exception policy.

The view is stack-only and cannot be retained across an `await`. Create it in a synchronous helper
when the calling compiler version does not allow a `ref struct` local in an async method.

## Clock, threading, and allocation

Use the same clock origin and unit for deadlines and calls to `Update`. If an application computes
`deadline = now + delay`, guard against `ulong` overflow; a wrapped deadline sorts near zero.
Convert signed clock values only after confirming that they are non-negative.

Keep all operations on a scheduler instance on its owning thread. ErinnTask sources may complete
and register continuations across threads, but this does not make the timer scheduler thread-safe.
Inline callbacks and continuations can re-enter application code before the triggering operation
returns, so update application state before canceling a timer and keep nested work bounded.

ErinnTimer and ErinnTask avoid allocations on common warmed paths, but do not promise that every
operation is allocation-free. ErinnTask uses a shared bounded reuse queue for each closed runner and
completion-source type; each queue retains returned instances up to its configured capacity. An empty
queue causes a new instance to be allocated, and a return made while the queue is full is discarded.
Queue initialization, timer storage growth, capturing delegates, Debug state machines, and exception
paths may allocate. Timer payloads are stored without boxing.

## Queue dependency

`crossbeam-queue` is an ErinnTask NuGet dependency, not a project in this solution. ErinnTask uses
its `ArrayQueue<T>` for bounded, concurrent reuse queues. See the
[crossbeam-queue package](https://www.nuget.org/packages/crossbeam-queue/).

## License

Both solution projects are distributed under the [MIT License](LICENSE).
