# ErinnTask

[![NuGet](https://img.shields.io/nuget/v/ErinnTask.svg?style=flat-square)](https://www.nuget.org/packages/ErinnTask/)

ErinnTask is a small asynchronous library for .NET. It provides the custom awaitable types `ErinnTask` and `ErinnTask<T>`, plus a manually driven timer scheduler that can complete those awaitables.

The timer scheduler is deliberately caller-driven: it does not create a thread, own a clock, or wait in the background. Raw `ErinnTimerScheduler` timestamps are unsigned absolute values with no built-in unit: they may represent microseconds, milliseconds, stopwatch ticks, frame counts, simulation steps, or another caller-defined clock. Use the same origin and unit when scheduling and calling `Update`. Your application supplies the timestamps and advances the scheduler from its owning thread.

## Contents

- [What the library provides](#what-the-library-provides)
- [Features this library does not provide](#features-this-library-does-not-provide)
- [Use cases and API selection](#use-cases-and-api-selection)
- [Installation and target frameworks](#installation-and-target-frameworks)
- [Quick start: ErinnTask](#quick-start-erinntask)
- [Quick start: timer callbacks](#quick-start-timer-callbacks)
- [Awaitable timers](#awaitable-timers)
- [Timer and awaitable relationship](#timer-and-awaitable-relationship)
- [Timer timestamps and unit guidance](#timer-timestamps-and-unit-guidance)
- [Cancellation and timer handles](#cancellation-and-timer-handles)
- [Disposal and resource lifetime](#disposal-and-resource-lifetime)
- [Exceptions](#exceptions)
- [Threading and continuation behavior](#threading-and-continuation-behavior)
- [Integration patterns and operational guidance](#integration-patterns-and-operational-guidance)
- [Allocation and performance notes](#allocation-and-performance-notes)
- [Public API map](#public-api-map)
- [Common mistakes](#common-mistakes)
- [Guidance for AI agents and contributors](#guidance-for-ai-agents-and-contributors)
- [License](#license)

## What the library provides

The public API is in the `Erinn` namespace.

| Type                                  | Purpose                                                                                                                              |
|---------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------|
| `ErinnTask`                           | An awaitable operation with no result. It is also the async return type for methods declared as `async ErinnTask`.                   |
| `ErinnTask<T>`                        | An awaitable operation that produces a `T` result. It is also the async return type for methods declared as `async ErinnTask<T>`.    |
| `ErinnTimerScheduler`                 | Stores timers and invokes callbacks when the caller advances it with `Update`; raw timestamps use any consistent caller-chosen unit. |
| `ErinnTimerHandle`                    | A value handle that can cancel one pending timer and invoke its callback with a cancellation tag.                                    |
| `ErinnTimerTag`                       | The timer callback result: `Ok`, `Canceled`, or `Disposed`.                                                                          |
| `ErinnTimerSchedulerExtensions`       | Provides `WaitUntil` for awaitable timers and `InMicroseconds` for a non-owning microsecond view of a scheduler.                     |
| `ErinnTimerSchedulerMicrosecondsView` | A stack-only, non-owning view that provides callback scheduling, updates, and awaitable deadlines in microseconds.                   |
| `ErinnTaskMethodBuilder`              | Compiler-selected builder for async methods returning `ErinnTask`; application code normally does not construct it.                  |
| `ErinnTaskMethodBuilder<T>`           | Compiler-selected builder for async methods returning `ErinnTask<T>`; application code normally does not construct it.               |

`ErinnTask` is not a wrapper around `System.Threading.Tasks.Task` or `ValueTask`. It has no implicit conversion to those types, no `ConfigureAwait`, no built-in `CancellationToken` support, and no `WhenAll` or `WhenAny` combinators. It can be awaited inside a regular `Task`-returning method, and an `ErinnTask`-returning method can await `Task`, `ValueTask`, or another awaitable that follows the C# awaiter pattern.

## Features this library does not provide

These limits are part of the current API contract:

- **Task and ValueTask adapters:** `ErinnTask` is its own awaitable type. There are no implicit conversions or built-in adapters to or from `Task`/`ValueTask`, though each type can be awaited in a compatible async method.
- **Task convenience APIs:** There is no `ConfigureAwait`, `WhenAll`, `WhenAny`, or built-in `CancellationToken` integration. Timer cancellation uses a separate value handle and completes with an `ErinnTimerTag`.
- **Automatic or relative timers:** The scheduler has no clock, delay-based overload, background timer thread, or wake-up mechanism. It accepts absolute timestamps and runs only when the application calls `Update`.
- **Recurring timers:** Each scheduled entry is one-shot. A callback can schedule another entry, but the application chooses its next deadline and catch-up policy.
- **Background work scheduling:** An `async ErinnTask` method starts synchronously and the library does not automatically queue it to the thread pool. Resumption follows the awaited operation's scheduling behavior.
- **Concurrent scheduler use:** One scheduler instance is unsynchronized and single-thread-affine. The `[ThreadAffine]` attribute is metadata; it does not enforce thread ownership at runtime.
- **Context or thread dispatch:** ErinnTask's own awaiter does not capture contexts or dispatch work. In an async ErinnTask method, the awaited type's awaiter controls resumption; for example, a standard `Task` awaiter may capture the current synchronization context. ErinnTask-backed timer continuations run inline.

## Use cases and API selection

ErinnTask is a good fit when an application already owns a regular update loop and wants to advance timers from that loop without creating a timer thread. Supplying timestamps explicitly also makes timer progress controllable: a test or simulation can advance a chosen clock value and observe which callbacks run.

Choose the API that matches how the operation is consumed:

| Need                                                                                                            | Use                                              | Notes                                                                                    |
|-----------------------------------------------------------------------------------------------------------------|--------------------------------------------------|------------------------------------------------------------------------------------------|
| An async method that returns no value or one result                                                             | `ErinnTask` or `ErinnTask<T>`                    | Treat source-backed results as single-consumption awaitables.                            |
| A timer that invokes an action and does not need an `await` result                                              | `ErinnTimerScheduler.Schedule<T>`                | The callback receives a terminal tag and its typed payload.                              |
| An async method that waits for a scheduled deadline                                                             | `WaitUntil`                                      | It returns `ErinnTask<ErinnTimerTag>`; the scheduler still needs regular `Update` calls. |
| Standard task combinators, `CancellationToken` APIs, broad library interoperability, or ordinary background I/O | `Task` or `ValueTask`                            | ErinnTask can await these types, but does not provide their surrounding APIs.            |
| A timer that must wake the process or run independently of an application loop                                  | A platform timer or another autonomous scheduler | ErinnTask's scheduler does not own a clock, thread, or wake-up mechanism.                |

### Good fit

- Frame-based delays, gameplay cooldowns, and timeouts owned by a game, UI, or other regular single-threaded update loop.
- Deterministic tests and simulations that need to advance a caller-controlled clock value.
- Internal async APIs where consumers understand the one-consumption rule and inline continuation behavior.

### Choose another primitive when

- A deadline must wake a sleeping process or run without a regularly driven owner loop.
- Callers require standard `Task`/`ValueTask` combinators, `CancellationToken` integration, or context capture when awaiting ErinnTask's own operations.
- Multiple threads must schedule, update, cancel, or dispose the same timer scheduler concurrently.

For public APIs consumed by arbitrary .NET code, weigh these constraints against the benefits of a custom awaitable before exposing `ErinnTask` directly.

## Installation and target frameworks

Install the `ErinnTask` package from NuGet with `dotnet add package ErinnTask`, or add it through the consuming project's package-management workflow. The package targets `net10.0`, `net9.0`, `net8.0`, `net7.0`, `net6.0`, `net5.0`, and `netstandard2.1`. The `netstandard2.1` target declares `System.Runtime.CompilerServices.Unsafe` as a package dependency.

## Quick start: ErinnTask

An async method can return either task type. It starts executing synchronously and continues until it returns or reaches an await that is not already complete.

```csharp
using Erinn;

static async ErinnTask<int> AddAsync(int left, int right)
{
    await ErinnTask.CompletedTask;
    return left + right;
}

static ErinnTask<int> GetCachedValue() => ErinnTask.FromResult(42);
```

The default value of `ErinnTask` is a successfully completed operation. The default value of `ErinnTask<T>` is also complete and contains `default(T)`. `ErinnTask.FromResult<T>(value)` creates an already completed `ErinnTask<T>` that stores its result inline.

Use `await` to observe a result or rethrow an exception reported by an async method:

```csharp
static async ErinnTask<int> CalculateAsync()
{
    int cached = await GetCachedValue();
    return await AddAsync(cached, 1);
}
```

### Single-consumption rule

An operation backed by a pooled source can be consumed once. Await each source-backed `ErinnTask` or `ErinnTask<T>` exactly once. Do not await multiple copies, call `GetAwaiter().GetResult()` and then await the same operation, or register multiple continuations. Copies of the struct share the same source and version; consuming one copy consumes them all. Reusing a consumed or stale copy can throw `InvalidOperationException`. Calling `GetResult()` on a source-backed operation while it is pending throws `InvalidOperationException`; it does not wait.

Inline completed values such as `ErinnTask.CompletedTask` and values returned by `ErinnTask.FromResult` do not use a pooled source. The safe general rule for an operation returned by another method is still to treat it as single-consumption unless that method explicitly documents otherwise.

An async method that throws records the exception in its returned operation. The exception is rethrown when that operation is awaited or its result is otherwise consumed. A method that creates a timer can also throw synchronously while validating or scheduling the timer, before returning the awaitable.

## Quick start: timer callbacks

Create one scheduler for the thread that will own it, schedule timers with absolute timestamps, and call `Update` from that thread. This short example uses manually supplied timestamps in a microsecond clock domain:

```csharp
using System;
using Erinn;

using (ErinnTimerScheduler scheduler = new ErinnTimerScheduler())
{
    scheduler.OnException = static exception => Console.Error.WriteLine(exception);

    const ulong nowMicroseconds = 1_000_000UL;
    ulong deadline = checked(nowMicroseconds + 2_000_000UL);
    scheduler.Schedule(deadline, static (tag, message) => Console.WriteLine($"{message}: {tag}"), "cache refresh");

    // Advancing to the first timestamp leaves the timer pending.
    scheduler.Update(nowMicroseconds);

    // Advancing to its deadline invokes the callback.
    scheduler.Update(deadline);
}
```

The example advances a controlled clock directly to the deadline. A real application should pass its current timestamp to `Update` regularly from its game loop, UI loop, or other owner-thread loop. The scheduler does not wake itself up when a deadline passes.

`ErinnTimerScheduler.Schedule<T>` accepts an absolute `ulong` timestamp, a non-null `Action<ErinnTimerTag, T?>`, and a payload. When a scheduled timer is eventually fired, canceled, or drained by scheduler disposal, its callback is invoked once. A timer remains pending if none of those actions occurs. The payload is passed to the callback and is not boxed by the scheduler.

Timers are ordered by timestamp. Timers with the same timestamp run in scheduling order. A callback may schedule another timer; if its timestamp is already due, it can also run during the same `Update` call.

## Awaitable timers

The timer extension returns `ErinnTask<ErinnTimerTag>` and provides a cancellation handle through an `out` parameter. The scheduler still has to be driven by the application.

### Microsecond view

`scheduler.InMicroseconds()` is an extension method that returns a stack-only `ErinnTimerSchedulerMicrosecondsView` over the same scheduler. It does not create or own another scheduler. Use this view when the scheduler's `Update` timestamps are in microseconds. For example, create the awaitable in a synchronous helper and await the returned operation from the caller:

```csharp
static ErinnTask<ErinnTimerTag> WaitForMicrosecondDeadline(ErinnTimerScheduler scheduler, ulong deadlineMicroseconds)
{
    ErinnTimerSchedulerMicrosecondsView microseconds = scheduler.InMicroseconds();
    return microseconds.WaitUntil(deadlineMicroseconds, out _);
}
```

The view's `Schedule<T>` and `Update` methods also use microseconds. Callback timers and awaitable timers therefore share the same queue and timestamp domain when used through the view.

The view's `WaitUntil` accepts an absolute microsecond timestamp unchanged. `WaitUntilMilliseconds` converts an absolute millisecond timestamp to microseconds and throws `ArgumentOutOfRangeException` if multiplication would overflow. `WaitUntilSeconds` accepts a finite, non-negative absolute seconds value whose converted value is less than `2^64` microseconds; fractional microseconds are truncated toward zero. These methods accept timestamps, not delays, and all resulting deadlines are compared against microsecond values passed to `Update`.

The view is a `ref struct`: it cannot be stored in a heap object or kept alive across an `await`. Before C# 13, ref-struct locals cannot be declared in async methods. C# 13 and later allow them in an async method only when they are not live across an `await`. Use the `InMicroseconds()` factory to create an initialized view: a default-initialized view has no scheduler, and its timer methods throw `ArgumentNullException`. The returned `ErinnTask<ErinnTimerTag>` is a separate, single-consumption operation and may be awaited according to the normal `ErinnTask` rules. Calls that schedule timers still belong on the scheduler's owner thread.

### Timer and awaitable relationship

`WaitUntil` queues a callback backed by a completion source at the caller-supplied absolute timestamp and returns an `ErinnTask<ErinnTimerTag>` together with a cancellation handle for that same entry. The timestamp is passed to the scheduler unchanged. The lifecycle is:

| Operation                                    | Effect                                                                                                                                                                                                                                                                                                           |
|----------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `scheduler.WaitUntil(timestamp, out handle)` | Queues the timer callback and completion source; returns the awaitable and a handle for that same entry.                                                                                                                                                                                                         |
| `Update(now)` reaches the deadline           | The scheduler removes the due entry and invokes its callback with `Ok`. `now` uses the same time domain and unit as `timestamp`.                                                                                                                                                                                 |
| `handle.Cancel()` while pending              | Removes the entry and invokes its callback inline with `Canceled`. Returns `true` if callback handling returns normally, or `false` if the entry is no longer pending.                                                                                                                                           |
| `Dispose()` while pending                    | Drains the entry and invokes its callback inline with `Disposed`.                                                                                                                                                                                                                                                |
| Await the operation                          | The callback passes the tag to the completion source with `SetResult`, completing the awaitable successfully with that tag. A continuation registered before completion runs inline on the completion thread; one registered after completion runs inline on the registering thread. Consume the operation once. |

`Ok`, `Canceled`, and `Disposed` are result tags, not exceptions. Callback exceptions, and exceptions from manually registered continuations invoked during timer completion, follow the scheduler's exception policy described below. Exceptions thrown by an async method body are captured in that method's returned `ErinnTask`.

```csharp
using Erinn;

static async ErinnTask<ErinnTimerTag> WaitUntilAsync(ErinnTimerScheduler scheduler, ulong deadline)
{
    ErinnTask<ErinnTimerTag> timer = scheduler.WaitUntil(deadline, out _);
    return await timer;
}
```

Call this method from application flow and keep advancing the scheduler from its normal owner-thread loop, as shown in [Quick start: timer callbacks](#quick-start-timer-callbacks). When `Update` reaches the deadline, the operation completes with `Ok`; a continuation registered by `await` runs inline inside that `Update` call.

The terminal-tag meanings and their behavior during cancellation and disposal are compared in [Cancellation and timer handles](#cancellation-and-timer-handles); callback exception handling is described in [Exceptions](#exceptions).

## Timer timestamps and unit guidance

Every timer input is an **absolute timestamp**, not a delay. The raw `ErinnTimerScheduler` has no clock and does not require microseconds: it compares `ulong` timestamps as supplied, without interpreting or converting their unit. They may be microseconds, milliseconds, `Stopwatch` ticks, frame or simulation counters, or values from another clock. Choose one time source, origin, and unit, then use that same domain for raw `Schedule<T>`, `scheduler.WaitUntil`, and every `Update` call. Any value from `0` through `ulong.MaxValue` is accepted by those raw APIs. The separate microsecond view provides millisecond and seconds awaitable overloads that validate and convert to microseconds. Calls to `Update` do not have to be monotonic; each call fires timers whose timestamp is less than or equal to its argument.

| Clock or time representation       | Raw `Schedule`, `scheduler.WaitUntil`, and `Update`         | Microsecond view                                                                              |
|------------------------------------|-------------------------------------------------------------|-----------------------------------------------------------------------------------------------|
| Application monotonic microseconds | Pass the absolute value unchanged.                          | `WaitUntil` accepts it unchanged; view `Schedule` and `Update` use microseconds.              |
| Application monotonic milliseconds | Pass unchanged only if all three raw APIs use milliseconds. | `WaitUntilMilliseconds` converts the deadline; view `Schedule` and `Update` use microseconds. |
| `Stopwatch.GetTimestamp()`         | Pass the non-negative tick count as `ulong`.                | Convert to an absolute microsecond timestamp before calling the microsecond view.             |
| Frame or simulation counter        | Pass the absolute counter consistently.                     | Convert to microseconds first, or use the raw APIs consistently in counter units.             |
| `DateTime` or Unix time            | Only when wall-clock behavior is intentional.               | Convert to an absolute microsecond deadline; a monotonic clock is generally safer.            |

Add a delay with checked arithmetic so overflow is reported instead of wrapping the deadline near zero:

```csharp
ulong nowMilliseconds = 12_500UL; // Sample from the application's monotonic millisecond clock.
ulong delayMilliseconds = 250UL;
ulong deadlineMilliseconds = checked(nowMilliseconds + delayMilliseconds);

ErinnTask<ErinnTimerTag> timer = scheduler.WaitUntil(deadlineMilliseconds, out _);
scheduler.Update(nowMilliseconds); // Later calls use the same millisecond clock.
```

If a clock API returns a signed value, validate that it is non-negative before converting it to `ulong`. If converting units yourself, check both multiplication and addition bounds before conversion. The microsecond view validates millisecond multiplication and seconds values before converting them; fractional seconds below one microsecond are truncated toward zero. The raw scheduler APIs do not perform unit conversion. Keep fractional precision and truncation behavior explicit when converting other clock representations in application code.

A timestamp of zero is queued normally. It completes only after an `Update` call reaches that timestamp, cancellation, or scheduler disposal.

## Cancellation and timer handles

`ErinnTimerHandle` is an immutable, per-timer handle. It is separate from the standard `System.Threading.CancellationToken` APIs and does not remove a timer silently.

- `handle.Cancel()` returns `true` when it canceled a timer that was still pending. The timer callback runs immediately with `ErinnTimerTag.Canceled` on the calling thread.
- It returns `false` for a default handle, a timer that already fired or was canceled, or a timer owned by a disposed scheduler.
- `IsCreated` is true when the scheduler issued the handle for a timer. It remains true after completion or scheduler disposal; use the return value from `Cancel` to determine whether a pending timer was canceled.
- Repeated cancellation is harmless and returns `false` after the first successful cancellation.
- Call `Cancel()` only on the scheduler's owning thread. The handle does not make cross-thread scheduler access safe.

| Event                                   | Callback tag and timing                                 | `Cancel()` result                            | Awaitable timer result                                                             |
|-----------------------------------------|---------------------------------------------------------|----------------------------------------------|------------------------------------------------------------------------------------|
| `Update` reaches or passes the deadline | `Ok`, during `Update`                                   | `false` if called after completion           | Completes with `Ok`                                                                |
| `Cancel()` is called while pending      | `Canceled`, inline on the calling thread                | `true` if callback handling returns normally | Completes with `Canceled`                                                          |
| `Dispose()` drains a pending timer      | `Disposed`, inline during disposal                      | `false` if called after disposal             | Completes with `Disposed`                                                          |
| Default handle                          | No callback; no timer is associated                     | `false`                                      | No associated operation                                                            |
| Timer already completed                 | No callback from this call; completion already happened | `false`                                      | No new completion; any earlier `Ok`, `Canceled`, or `Disposed` result is unchanged |

`Canceled` and `Disposed` are normal `ErinnTimerTag` results, not thrown cancellation or disposal exceptions. Cancellation removes the timer before invoking its callback. If the callback or configured `OnException` handler throws, `Cancel()` propagates that exception after the timer has already been removed, so no Boolean result is returned.

The handle is immutable and does not clear itself after completion. Avoid retaining completed handles longer than necessary, especially when they keep a disposed scheduler reachable.

### Inline completion and same-thread reentrancy

An awaited timer completes its continuation inline. That continuation can call back into the same scheduler before the original `Cancel`, `Update`, or `Dispose` call returns. This is synchronous reentrancy on the scheduler's owner thread, not concurrent access from another thread.

This does not leave the current timer half-removed. The scheduler removes its queue node and clears/releases its bucket entry before invoking the callback. A continuation can therefore cancel another pending timer, schedule another timer, or call `Update` on the owner thread while the current callback is on the stack. A nested `Update` runs immediately using the timestamp passed to that nested call.

| Reentrant call                                                                | Behavior                                                                                                                                                                                                                                                                                                           |
|-------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Cancel the timer whose callback is already running, or a copied handle for it | Returns `false`; the entry is no longer pending. A reused bucket slot has a different timer uid, so an old handle cannot cancel the new timer.                                                                                                                                                                     |
| Cancel another pending timer                                                  | Invokes that timer's callback and continuation inline before the nested `Cancel` returns.                                                                                                                                                                                                                          |
| Schedule a timer and call `Update`                                            | Allowed before disposal. The nested update can complete timers due at its supplied timestamp before the outer call returns.                                                                                                                                                                                        |
| Call `Dispose` from a callback or continuation                                | Disposal closes the scheduler before draining remaining timers with `Disposed`. During that drain, nested `Update` is ignored, `Cancel` returns `false`, attempts to schedule through `Schedule` or `WaitUntil` on the disposing scheduler throw `ObjectDisposedException`, and another `Dispose` call is a no-op. |

These outcomes are part of the synchronous API contract; they do not indicate duplicate completion, a stale handle canceling a reused slot, or a thread-safe scheduler. Keep all scheduler calls on its owner thread, update application-owned state before calling `Cancel`, and keep nested callback chains bounded because they use the current call stack. Exceptions still follow the policies in [Exceptions](#exceptions); removing a timer is not undone when its callback or continuation throws.

## Disposal and resource lifetime

`ErinnTimerScheduler` implements `IDisposable`. Dispose it on its owning thread when it is no longer needed, usually with `using` or `using` declarations.

Disposal marks the scheduler closed, invokes every still-pending timer callback with `ErinnTimerTag.Disposed`, releases queue and bucket storage, and releases the exception handler reference. Repeated calls after the first are no-ops, including when the first call threw an `AggregateException` after cleanup.

After disposal:

- With a non-null scheduler and otherwise valid arguments, raw `Schedule` and `scheduler.WaitUntil` throw `ObjectDisposedException`. Every `ulong` timestamp is valid for those raw APIs; `Schedule` still validates its callback. The microsecond view's millisecond and seconds methods also validate their input ranges before scheduling.
- `Update` is ignored.
- `Cancel` returns `false`.
- Scheduling from a callback that is running during the disposal drain throws `ObjectDisposedException`.

Do not dispose a scheduler while application code still expects it to accept timers. Pending awaitable timers complete with `Disposed` during disposal, so consume each returned task once if you need to observe that outcome.

## Exceptions

Timer callback exceptions are handled by the scheduler that invoked the callback:

1. If `OnException` is set, the scheduler passes the callback exception to the current handler. If the handler returns normally, `Update` continues processing other due timers.
2. If `OnException` is `null`, the callback exception propagates from `Update` or `Cancel`. The timer was already removed and is not restored.
3. If `OnException` itself throws, that exception propagates from `Update` or `Cancel` and stops that operation.
4. During `Dispose`, pending timers are still drained. Callback exceptions without a handler, and exceptions thrown by the handler, are collected and thrown together as an `AggregateException` after storage cleanup.

The callback handler is a single `Action<Exception>` property. Replacing it affects later exception reports. The property is cleared during disposal.

The callback type is `Action<ErinnTimerTag, T?>`. Passing an `async` lambda therefore creates an `async void` callback: the scheduler does not await its asynchronous work, and exceptions from that work are not sent to `OnException`. Keep timer callbacks synchronous and explicitly observe any separate async operation.

For an application loop that should log callback errors and continue processing due timers, set a handler that returns normally:

```csharp
using System;
using Erinn;

internal static class TimerErrorHandling
{
    public static void ConfigureLogging(ErinnTimerScheduler scheduler)
    {
        scheduler.OnException = static exception => Console.Error.WriteLine(exception);
    }
}
```

Call `ConfigureLogging` once when initializing the scheduler, then keep calling `Update` from the owner loop. If `OnException` is left `null`, catch exceptions around `Update` or `Cancel` when the caller needs to handle them there. During `Dispose`, catch the resulting `AggregateException` if a callback throws without a handler or if the configured handler itself throws; disposal finishes draining and releases scheduler storage before throwing it.

`ErinnTimerTag.Disposed` is not how callback exceptions are reported. It means that a timer was pending when the scheduler was disposed. If a manually registered continuation throws while the scheduler is synchronously completing a timer, the exception travels through the callback path and follows the scheduler policy above. A manually registered continuation attached after completion runs on the registering thread; an exception it throws propagates there. Exceptions thrown by an async method body are stored in its returned `ErinnTask` and observed by consuming that operation.

## Threading and continuation behavior

### `ErinnTask` operations

- ErinnTask's own awaiter does not capture `ExecutionContext` or `SynchronizationContext`. In an async method returning ErinnTask, the awaited type's awaiter controls scheduling; a standard `Task` awaiter may capture the current synchronization context by default.
- When a source-backed ErinnTask completes with a continuation registered, that continuation runs inline on the completing thread. If it is registered after completion, it runs inline on the registering thread.
- Completion can race with continuation registration; the stored continuation is invoked once.
- A source-backed operation supports one continuation registration and one result consumption. Concurrent consumers or multiple continuation registrations are unsupported.
- Completion and continuation registration may occur on different threads. This does not make a timer scheduler thread-safe.

For ErinnTask-backed operations, inline continuations can re-enter the code that completed the operation. Keep continuation work bounded and account for the resumption thread in code after `await`.

### `ErinnTimerScheduler`

One scheduler instance is single-thread-affine and unsynchronized. Create, schedule, update, cancel, set its exception handler, and dispose it from the same owning thread. Concurrent access from multiple threads is unsupported. The internal thread-affinity attribute is metadata only; it does not perform runtime checks or add synchronization.

Timer callbacks run inline on the thread calling `Update`, `Cancel`, or `Dispose`. A pending awaitable timer resumes on the thread that completes it, usually the scheduler thread calling `Update`. ErinnTask does not dispatch back to the thread or synchronization context that awaited it. If application work must run on another thread or context, dispatch it explicitly using the application's own mechanism.

## Integration patterns and operational guidance

### Drive the scheduler from one owner loop

Create the scheduler alongside the game, UI, or event-loop component that owns it. On each loop iteration, read the same monotonic clock used to calculate deadlines and call `Update` with that absolute timestamp. A timer becomes eligible when an update reaches or passes its deadline; it does not run at an exact wall-clock instant. If the loop is paused or delayed, the callback waits until the next update.

Keep scheduling, `Update`, cancellation, exception-handler changes, and disposal on that owner thread. If another thread requests cancellation, send the request to the owner loop and call `Cancel` there. The Boolean result tells whether that call removed a still-pending timer; it does not make cross-thread access safe.

`Cancel` invokes the timer callback synchronously. For an awaited timer, that callback can run the waiting continuation inline before `Cancel` returns, so update the owning component's state before calling it.

### Await one timer result and handle all terminal tags

The returned timer operation is a one-shot operation. Store it, await it exactly once, and handle its completion outcome. This example maps `Ok` to `true` and both non-`Ok` results to `false`:

```csharp
static async ErinnTask<bool> WaitForDeadlineAsync(ErinnTimerScheduler scheduler, ulong deadlineMicroseconds)
{
    ErinnTask<ErinnTimerTag> timer = scheduler.WaitUntil(deadlineMicroseconds, out _);
    ErinnTimerTag tag = await timer;
    return tag == ErinnTimerTag.Ok;
}
```

If an owning component needs cancellation, create the timer there, retain the returned handle, and give the one-shot operation to a single consumer. Call `Cancel` on the scheduler's owner thread. The timer operation still needs to be consumed once so its completion source can be returned to the pool. Treat `Canceled` and `Disposed` as outcomes that the caller must interpret; neither is a thrown cancellation exception.

### Define recurring-timer behavior yourself

Timers are one-shot. A callback may schedule another timer, and a newly scheduled timer whose deadline is already due can run later in the same `Update` call. A callback that repeatedly schedules another already-due timer can therefore keep that update loop running indefinitely. For recurring work, explicitly choose whether missed intervals should catch up or be skipped, advance the next deadline accordingly, and bound catch-up work when necessary.

### Keep clock arithmetic and time units consistent

Use one monotonic clock and one origin for both deadlines and `Update`. Do not mix Unix timestamps, elapsed time from process start, frame counters, milliseconds, and microseconds without an explicit conversion. Raw `Schedule` and `scheduler.WaitUntil` compare caller-provided `ulong` values directly. The microsecond view's `WaitUntil` also passes its microsecond input unchanged; its millisecond and seconds methods convert their inputs to microseconds.

The scheduler accepts all raw `ulong` timestamps, but the application is responsible for computing deadlines safely. For `deadline = now + delay`, check that the addition does not overflow `ulong`; a wrapped deadline sorts near zero and can fire much earlier than intended. For wall-clock/calendar deadlines, convert them into the chosen monotonic clock domain rather than passing `DateTime`-style epoch values directly.

### Bound work performed by callbacks and continuations

Callbacks and continuations execute inline. A long-running callback delays the rest of the due timers and the caller of `Update`. An awaited timer's continuation may also schedule more work, cancel other timers, or otherwise re-enter application code before the current `Update` returns. Keep this work bounded and make state transitions safe for that ordering.

When no `OnException` handler is installed, a callback exception propagates and stops the current `Update`; the throwing timer is already removed. Timers not yet dequeued remain pending unless the callback re-entered the scheduler and changed or drained them. If `OnException` is installed and returns normally, `Update` continues with the next timer still queued at that point. Choose this policy deliberately for the owning loop.

## Allocation and performance notes

The library is designed to avoid unnecessary allocations on common paths, but it does not promise that every operation is allocation-free.

- `ErinnTask.CompletedTask` and `ErinnTask.FromResult<T>` store completed values inline and do not rent completion storage.
- A suspended async method uses a pooled typed state-machine runner. Timer awaitables and async methods that fault before their first suspension use pooled completion sources. Completion-source pools are per thread and result type; runner pools are per thread and constructed state-machine/result type.
- After warm-up, successful operations within the available pool capacity can avoid managed allocations in Release builds. Initial pool setup, pool or timer storage growth, Debug state machines, and exception paths can allocate.
- Timer payloads are stored without boxing. Timer queues, buckets, callback delegates, and first-use initialization can still allocate.
- A capturing lambda creates a delegate/closure as required by normal C# semantics. Reuse a cached delegate when profiling shows callback allocation matters. For library-owned callbacks reused on hot paths, use a named static method and an explicit `static readonly` cache.

Measure the real call pattern and build configuration before making allocation claims. Do not infer zero-GC behavior from the fact that the task types are structs or that a callback payload is not boxed.

## Public API map

### Task APIs

| API                                                    | Behavior                                                                                                  | Threading contract                                                                                                   |
|--------------------------------------------------------|-----------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------|
| `ErinnTask.CompletedTask`                              | Completed non-generic operation.                                                                          | No scheduler affinity; completes synchronously.                                                                      |
| `ErinnTask.FromResult<T>(T result)`                    | Completed `ErinnTask<T>` with an inline result.                                                           | No scheduler affinity; completes synchronously.                                                                      |
| `ErinnTask.GetAwaiter()` / `ErinnTask<T>.GetAwaiter()` | Awaiter entry points used by the C# compiler. Manual consumption must follow the single-consumption rule. | No fixed thread affinity; source-backed operations allow one consumer and completion/registration may cross threads. |

### Scheduler and callback timer APIs

| API                                                                     | Behavior                                                                              | Threading contract                                                                       |
|-------------------------------------------------------------------------|---------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------|
| `new ErinnTimerScheduler()`                                             | Creates a new independent scheduler with empty timer and callback storage.            | Choose the thread that will own it; subsequent scheduler operations stay on that thread. |
| `ErinnTimerScheduler.Schedule<T>(ulong, Action<ErinnTimerTag, T?>, T?)` | Schedules a callback with a payload and returns its cancellation handle.              | Scheduler owner thread only; callback runs inline on the thread that triggers it.        |
| `ErinnTimerScheduler.Update(ulong)`                                     | Runs all timers due at or before the supplied absolute timestamp.                     | Scheduler owner thread only; callbacks and continuations run inline.                     |
| `ErinnTimerScheduler.OnException`                                       | Gets or sets the single timer callback exception handler.                             | Read and write on the scheduler owner thread.                                            |
| `ErinnTimerScheduler.Dispose()`                                         | Completes all pending timer callbacks with `Disposed` and releases scheduler storage. | Scheduler owner thread only; pending callbacks run inline during disposal.               |

### Timer handles and tags

| API                          | Behavior                                                                                        | Threading contract                                                                   |
|------------------------------|-------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------|
| `ErinnTimerHandle.Cancel()`  | Cancels one pending timer and invokes its callback with `Canceled`.                             | Scheduler owner thread only; a registered continuation can run inline before return. |
| `ErinnTimerHandle.IsCreated` | Reports whether the scheduler issued this handle; it stays true after completion or disposal.   | Reads handle state only; no scheduler access.                                        |
| `ErinnTimerHandle.Scheduler` | Gets the scheduler that issued the handle, or `null` for a default handle; it may be disposed.  | Reads handle state only; no scheduler access.                                        |
| `ErinnTimerHandle.Uid`       | Gets the timer uid; uids are unique within one scheduler, and a default handle has value `0`.   | Reads handle state only; no scheduler access.                                        |
| `ErinnTimerTag`              | Completion result tag returned to a timer callback or awaiter: `Ok`, `Canceled`, or `Disposed`. | Plain enum value; no thread affinity.                                                |

### Awaitable timer API

| API                                                                    | Behavior                                                                         | Threading contract                                                                    |
|------------------------------------------------------------------------|----------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|
| `ErinnTimerSchedulerExtensions.WaitUntil(ulong, out ErinnTimerHandle)` | Creates an awaitable timer from an absolute timestamp without changing its unit. | Call on the scheduler owner thread; a pending await resumes on the completion thread. |

### Microsecond view APIs

| API                                                                                      | Behavior                                                                                                     | Threading contract                                                                                   |
|------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------|
| `ErinnTimerSchedulerExtensions.InMicroseconds(ErinnTimerScheduler)`                      | Returns a stack-only view for timer operations whose scheduler timestamps use microseconds.                  | Creating the view does not access scheduler state; its timer methods use the scheduler owner thread. |
| `ErinnTimerSchedulerMicrosecondsView.Schedule<T>(ulong, Action<ErinnTimerTag, T?>, T?)`  | Schedules a callback timer at an absolute microsecond timestamp and returns its cancellation handle.         | Scheduler owner thread; callback runs inline on the thread that triggers it.                         |
| `ErinnTimerSchedulerMicrosecondsView.Update(ulong)`                                      | Fires pending timers due at the absolute microsecond timestamp.                                              | Scheduler owner thread; callbacks run inline on the calling thread.                                  |
| `ErinnTimerSchedulerMicrosecondsView.WaitUntil(ulong, out ErinnTimerHandle)`             | Creates an awaitable timer from an absolute microsecond timestamp without changing its value.                | Scheduler owner thread; a pending await resumes on the completion thread.                            |
| `ErinnTimerSchedulerMicrosecondsView.WaitUntilMilliseconds(ulong, out ErinnTimerHandle)` | Converts an absolute millisecond timestamp to microseconds, then creates an awaitable timer.                 | Scheduler owner thread; a pending await resumes on the completion thread.                            |
| `ErinnTimerSchedulerMicrosecondsView.WaitUntilSeconds(double, out ErinnTimerHandle)`     | Converts a finite, non-negative absolute seconds timestamp to microseconds, then creates an awaitable timer. | Scheduler owner thread; a pending await resumes on the completion thread.                            |

## Common mistakes

- **Passing a duration as a timestamp.** Compute `deadline = now + delay` using the same clock and unit as the future `Update` calls.
- **Updating in the wrong unit.** `scheduler.WaitUntil` passes its timestamp through unchanged. The microsecond view converts its millisecond and seconds methods to microseconds, so call `Update` with microseconds when using that view.
- **Expecting an automatic timer thread.** The application must call `Update`; no timer fires while the scheduler is not being advanced.
- **Treating `Canceled` or `Disposed` as exceptions.** They are `ErinnTimerTag` values returned by the awaitable timer.
- **Assuming an await resumes on its original context.** Continuations run inline on the completion thread; no context is captured.
- **Using one scheduler from multiple threads.** Keep all operations on its owning thread or marshal them there yourself.
- **Awaiting a source-backed operation twice.** Consume it once, even if multiple struct copies exist.
- **Assuming `handle.IsCreated` means pending.** It only means the scheduler issued the handle; `Cancel()` reports whether it canceled a live timer.
- **Assuming callback exceptions are swallowed.** Configure `OnException` deliberately or handle the exceptions that propagate from `Update`, `Cancel`, and `Dispose`.

## Guidance for AI agents and contributors

Use this section as the repository-specific operating contract when helping with ErinnTask.

### When using the library

1. Select `ErinnTask` for no result and `ErinnTask<T>` for a result. Use `await`; do not assume Task-specific APIs or implicit conversions exist.
2. Treat source-backed operations as one-shot. Do not duplicate consumption or register more than one continuation.
3. Treat every timer input as an absolute timestamp. Define the clock origin and unit, convert explicitly only when needed, and keep `Update` in that same domain. `scheduler.WaitUntil` and the view's `WaitUntil` pass their timestamps through unchanged; the microsecond view converts millisecond and seconds inputs to microseconds.
4. Keep a scheduler and its cancellation handles on one owner thread. Do not assume the thread-affinity marker enforces this at runtime.
5. Handle `Ok`, `Canceled`, and `Disposed` as distinct completion results. They are not exceptions.
6. Remember that continuations and callbacks run inline. Avoid blocking work and be explicit about reentrancy and dispatch to other contexts.
7. Decide how callback exceptions should be handled by setting `OnException` or by handling exceptions from the triggering operation.

### When changing this repository

1. Read the implementation and XML documentation before changing a contract. This README summarizes public behavior; the source remains authoritative when details change.
2. Keep changes local to a demonstrated need. Do not add runtime checks, synchronization, abstractions, allocations, forwarding wrappers, duplicate checks, or `finally` blocks solely for hypothetical future use or redundant defense. Keep a check or cleanup path when it enforces a real contract or lifecycle invariant.
3. Keep exception creation and throwing in `ThrowHelpers`; use `SR` for established exception messages. Preserve existing `ExceptionArgument` values unless the corresponding parameter contract actually changes.
4. Preserve the one-continuation/one-consumption lifecycle and do not return pooled sources or runners until active calls have returned.
5. Keep timer queue, bucket, handle, and disposal operations single-thread-affine. The thread-affinity attribute is metadata only; do not add runtime thread checks or imply that it synchronizes access.
6. Preserve exception paths: remove callback entries before invocation, and retain the distinct propagation behavior of `OnException`, `Update`, `Cancel`, and `Dispose`.
7. Do not claim thread safety, context capture, cancellation exceptions, automatic clocking, or zero allocations unless the implementation and a representative verification demonstrate the claim. Measure Release behavior before making performance claims.
8. Do not introduce class inheritance. Use interfaces only where they clarify an existing dependency or contract; do not create a base class for hypothetical future types.
9. Keep mutable timer state owned by each scheduler. Do not move scheduler storage into global singletons or add memory-heavy global caches or per-type indexes to improve benchmark results. Keep the timer queue and pools array-backed; do not use linked lists.
10. Do not add field initializers that explicitly assign CLR default values such as `0`, `false`, `null`, or `default`. Assign required non-default state in the instance or static constructor.
11. Follow the existing identifier casing. Spell initialisms as ordinary PascalCase words, such as `Uid`, `Id`, and `Cpu`, rather than `UID`, `ID`, or `CPU`; do not rename unrelated established identifiers as cleanup.
12. New C# files named after generic types must include the type's generic arity in the filename. For example:

   ```text
   ErinnTimerEntry`1.cs
   ```

13. Preserve the existing line-ending style of C# files and follow the current `Modules/Task` and `Modules/Timer` organization. Do not reformat unrelated files or change unrelated public APIs.
14. Follow the project's access pattern: use `public` for members of internal types that other implementation types call, and use `private` when access is local. Avoid `protected`; this repository does not use class inheritance. Implement interfaces with directly callable methods when the concrete call path is intended.
15. For reusable callbacks on hot paths, use named static methods and explicit `static readonly` delegate caches when sharing the delegate avoids repeated creation. Do not rely on compiler-generated lambda caching or capture per-operation state in a shared callback.
16. Keep XML documentation accurate and complete for actual behavior, argument units and ranges, exceptions, threading, and lifecycle effects. Use bullet lists for separate constraints or outcomes; use `<inheritdoc/>` only when the inherited documentation accurately describes the member.
17. Keep examples and this README aligned with the implementation and XML documentation. When behavior changes, update all affected public documentation together.
18. Validate library changes against `ErinnTask/ErinnTask.csproj`, not only against a helper or isolated internal operation. Tests should exercise the relevant public path; report exactly which path they cover and do not alter private state to manufacture a test case.
19. The package is multi-targeted. Build `ErinnTask/ErinnTask.csproj` for all configured targets when a change can affect compatibility. Do not switch to building the solution as a substitute for checking the library project.
20. Release builds create a NuGet package in `ErinnTask/nupkgs`; the packing target clears and recreates that directory. Keep unrelated files out of it.

## License

ErinnTask is distributed under the [MIT License](LICENSE).
