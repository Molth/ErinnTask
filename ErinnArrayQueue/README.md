# ErinnArrayQueue

[![NuGet](https://img.shields.io/nuget/v/ErinnArrayQueue.svg?style=flat-square)](https://www.nuget.org/packages/ErinnArrayQueue/)

ErinnArrayQueue provides a bounded, fixed-capacity multi-producer, multi-consumer queue.
The queue allocates its slot buffer when it is constructed and does not grow when it becomes full.

## Basic use

`TryEnqueue` returns `false` when the queue is full. `TryDequeue` returns `false` when the queue is empty:

```csharp
using System;
using Erinn;

var queue = new ErinnArrayQueue<string>(capacity: 2);

queue.TryEnqueue("first");
queue.TryEnqueue("second");

if (!queue.TryEnqueue("third"))
    Console.WriteLine("The queue is full.");

while (queue.TryDequeue(out var item))
    Console.WriteLine(item);
```

The capacity must be greater than zero. Constructing a queue with a zero or negative capacity
throws `ArgumentOutOfRangeException`.

## Queue behavior

- `Capacity` is fixed for the lifetime of the queue.
- Multiple producers and consumers can enqueue and dequeue concurrently.
- `TryEnqueue` leaves the queue unchanged and returns `false` when it is full.
- `TryDequeue` returns `false` and sets its `out` value to the default value of `T` when the queue is empty.
- `IsEmpty`, `IsFull`, and `Count` report the observed queue state. Concurrent operations can change that state immediately after a property is read.
- `Capacity` and `Count` use the native-sized unsigned type `nuint`.

## Overwriting the oldest element

`Enqueue` always inserts the new element. If the queue is full, it replaces the oldest element and
returns it through `overwritten`:

```csharp
using System;
using Erinn;

var queue = new ErinnArrayQueue<int>(capacity: 2);

queue.Enqueue(10, out _);
queue.Enqueue(20, out _);

if (queue.Enqueue(30, out var overwritten) == InsertResult.Overwritten)
    Console.WriteLine(overwritten); // 10
```

`InsertResult.Success` means no element was replaced. `InsertResult.Overwritten` means the oldest
element was replaced. Use the result to distinguish an overwritten `null` value from the default
value assigned to `overwritten` when no element was replaced.

## Source

The queue follows the bounded MPMC algorithm by Dmitry Vyukov and is based on Crossbeam's
[`ArrayQueue`](https://github.com/crossbeam-rs/crossbeam/blob/main/crossbeam-queue/src/array_queue.rs).

## License

ErinnArrayQueue is distributed under the MIT License.
