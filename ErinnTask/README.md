# ErinnTask

[![NuGet](https://img.shields.io/nuget/v/ErinnTask.svg?style=flat-square)](https://www.nuget.org/packages/ErinnTask/)
[![NuGet](https://img.shields.io/nuget/v/ErinnTimer.svg?style=flat-square)](https://www.nuget.org/packages/ErinnTimer/)
[![NuGet](https://img.shields.io/nuget/v/crossbeam-queue.svg?style=flat-square)](https://www.nuget.org/packages/crossbeam-queue/)

ErinnTask provides the custom awaitable types `ErinnTask` and `ErinnTask<T>` for asynchronous
methods. They are not wrappers around `Task` or `ValueTask`; the library does not provide their
combinators or built-in cancellation-token integration.

The project also provides timer extensions for `ErinnTimerScheduler`. Those methods return
awaitable timer results while leaving clock selection, scheduler updates, cancellation, and
disposal under the caller's control.

## Async methods and results

An async method returning `ErinnTask` starts running synchronously and continues until it completes
or reaches an incomplete await. Synchronous successful results are stored directly in the returned
value and do not need a completion source. A default `ErinnTask` is already successful, while a
default `ErinnTask<T>` is complete with `default(T)`.

```csharp
using System;
using Erinn;

static async ErinnTask<int> AddAsync(int left, int right)
{
    await ErinnTask.CompletedTask;
    return left + right;
}

static ErinnTask<int> GetCachedValue() => ErinnTask.FromResult(42);

static async ErinnTask<int> CalculateAsync()
{
    int cached = await GetCachedValue();
    return cached + await AddAsync(1, 2);
}

Console.WriteLine(await CalculateAsync());
```

`CompletedTask` and `FromResult<T>` create inline, already-completed operations. A suspended async
method stores its typed state machine in a pooled runner. If an async method faults before its first
suspension, a pooled completion source stores the exception so the returned operation remains
awaitable. Awaiting a faulted operation rethrows the captured exception.

## Consumption rules

An operation backed by a pooled source supports one continuation registration and one result
consumption. Await each source-backed operation exactly once. Copies share the same source and
version, so consuming one copy consumes every copy. A second consumption, a second continuation
registration, or requesting the result while the operation is pending can throw
`InvalidOperationException`. A pending result request does not wait.

Inline completed operations, including the default values, `CompletedTask`, and the result of
`FromResult<T>`, have no pooled source to consume. For values returned by other methods, follow
that method's contract; source-backed operations remain single-consumption even when copied.

The awaiter follows the C# awaiter pattern. Compiler-generated `await` uses it automatically;
manual use can register a continuation through `OnCompleted` or `UnsafeOnCompleted` and then call
`GetResult` after completion.

## Continuation and context behavior

Completion may race with continuation registration. The registered continuation runs once, inline
on the thread that completes the operation. If the operation is already complete when a continuation
is registered, it runs inline on the registering thread. Completion and continuation execution do
not dispatch work to another thread.

The `ErinnTask` awaiter does not capture `ExecutionContext` or `SynchronizationContext`. Inside an
async method, the awaiter for each awaited value controls its own resumption behavior; for example,
a standard `Task` awaiter may capture a synchronization context. The custom method builder does not
add context capture or scheduling around that awaiter.

Continuations can execute inside the call that completes an operation. Code after an await may
therefore run on the completion thread and re-enter the completing code. Account for the resumption
thread and keep nested continuation work bounded.

## Awaitable timers

`WaitUntil` returns an `ErinnTask<ErinnTimerTag>` and provides an `ErinnTimerHandle` through an
`out` parameter. The scheduler extension uses the scheduler's raw absolute timestamp.
`InMicroseconds()` returns a non-owning view whose raw timestamps are in microseconds, plus methods
that convert absolute millisecond or second timestamps.

```csharp
using Erinn;

static ErinnTask<ErinnTimerTag> CreateMicrosecondWait(ErinnTimerScheduler scheduler, ulong deadlineMicroseconds)
{
    var timers = scheduler.InMicroseconds();
    return timers.WaitUntil(deadlineMicroseconds, out _);
}

static async ErinnTask<ErinnTimerTag> WaitForDeadlineAsync(ErinnTimerScheduler scheduler, ulong deadlineMicroseconds)
{
    return await CreateMicrosecondWait(scheduler, deadlineMicroseconds);
}
```

The scheduler must receive an `Update` call at or past the deadline before the wait can complete.
The result is `ErinnTimerTag.Ok` when the deadline is reached, `Canceled` when the returned handle
cancels the timer, or `Disposed` when the scheduler drains the pending timer. These are result
values, not cancellation exceptions. Consume each source-backed wait once, including canceled or
disposed waits.

### Timestamp units and conversion

- Scheduler `WaitUntil(ulong)` accepts any `ulong` timestamp in the scheduler's unit.
  It uses the absolute timestamp unchanged.
- The microsecond view's `WaitUntil(ulong)` accepts any `ulong` timestamp and uses it as an
  absolute microsecond value.
- `WaitUntilMilliseconds(ulong)` accepts values from `0` through `18446744073709551`, inclusive.
  It multiplies the absolute millisecond timestamp by 1,000.
- `WaitUntilSeconds(double)` accepts finite, numerically non-negative values below
  `18446744073709.55`. It multiplies by 1,000,000 and truncates fractional microseconds toward zero.

Negative zero is accepted as zero by the seconds overload. `NaN`, infinities, negative values, and
values at or above the stated boundary throw `ArgumentOutOfRangeException`. The timestamp is never
treated as a delay and no clock is read. Keep the same time origin and unit for scheduling and
calls to `Update`.

`InMicroseconds()` returns a non-owning `ref struct` view over the existing scheduler. The view does
not dispose the scheduler and cannot be stored in a heap object. Its use in async methods is subject
to the compiler's `ref struct` lifetime rules; keep the view out of any scope that crosses an await.
A default-initialized view has no scheduler, so its timer methods throw `ArgumentNullException`.

Awaitable scheduling can also throw `ObjectDisposedException` for a disposed scheduler and
`InvalidOperationException` if the scheduler uid or pooled source version is exhausted.

## Threading, exceptions, and ownership

The timer scheduler is unsynchronized. Create it and perform scheduling, updates, cancellation,
exception-handler changes, and disposal on its owning thread. Timer callbacks and their await
continuations run inline on the thread that fires, cancels, or disposes the timer. The view and
extension methods do not own the scheduler; disposing it completes its pending timers with
`ErinnTimerTag.Disposed`.

Timer callback failures follow the scheduler's `OnException` policy. If a configured handler returns
normally, due timers continue processing. Without a handler, callback exceptions propagate from
`Update` or cancellation; exceptions thrown by the handler also propagate and stop that operation.
During disposal, draining continues and unhandled callback failures or handler exceptions are
collected into an `AggregateException` after cleanup. A continuation exception raised while a timer
completes is reported through this same policy.

An `async` lambda passed as a timer callback is `async void`. The scheduler cannot await its work
or report later exceptions through `OnException`; keep timer callbacks synchronous and explicitly
observe any separate asynchronous operation.

## Interoperability and allocation

There are no implicit conversions to or from `Task` or `ValueTask`, no `ConfigureAwait`, no
`WhenAll` or `WhenAny`, and no built-in `CancellationToken` integration. The types can still be
awaited by any async method that follows the C# awaiter pattern.

Synchronous success needs no pooled source. Suspended methods reuse typed runners and completion
storage through `ArrayQueue<T>` instances from the `crossbeam-queue` package. Each closed runner or
completion-source type has a separate queue shared across threads and retains returned instances up
to its configured capacity; an empty queue causes a new instance to be allocated, and
a return made while the queue is full is discarded. Queue initialization, timer storage growth, captured
delegates, Debug state machines, and exception paths may allocate. Timer payloads are stored without
boxing. Measure the application's actual call pattern before making allocation claims.

## License

ErinnTask is distributed under the MIT License.
