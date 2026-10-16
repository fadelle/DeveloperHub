---
title: "Threads, the TPL, and async/await: Picking the Right Abstraction Level"
slug: threads-tpl-async
summary: Three layers of the same problem — raw threads, the Task Parallel Library, and async/await — each trading control for simplicity, and each suited to a different kind of work.
order: 1
sources: [Thread library vs TPL.docx, TPL library.docx, Async await vs TPL.docx, Introduction to Concurrency.docx, Task-Based Asynchronous Programming in C#.docx, async await c#.docx, AsyncAwait VS Task.Run.docx, IO Threads VS Worker Threads.docx]
related: [await-inside-loop, task-whenall-vs-semaphoreslim]
---

## Problem

.NET offers three overlapping ways to run code concurrently — the `Thread` class directly, the Task Parallel Library (`Task`, `Task.Run`, `Parallel.For`), and the `async`/`await` keywords — and it's not always obvious which one a given piece of code should reach for, especially since all three can technically be made to work for almost any scenario. Picking the wrong layer doesn't usually break the code; it just adds unnecessary ceremony (manual thread management for something `await` would handle in two lines) or hides control you actually needed (reaching for `async`/`await` when the real need was fine-grained control over individual threads).

## When It Applies

This is the starting point before the more specific concurrency cases on this site — [Does await Inside a Loop Block the Caller?](/topics/concurrency-task-orchestration/await-inside-loop) and [Task.WhenAll vs SemaphoreSlim](/topics/concurrency-task-orchestration/task-whenall-vs-semaphoreslim) both assume you've already decided you're in `async`/`await` territory. Use this case to make that decision in the first place — whenever you're reaching for *any* form of concurrency and haven't yet settled on raw threads, the TPL, or `async`/`await`.

## Solution

The three layers build on each other, each trading manual control for less code to write:

**The `Thread` library** provides low-level primitives — `Thread`, `ThreadPool` — for creating and managing threads directly. It gives direct control over creation, starting, stopping, and synchronization, but that control is manual: synchronization (locks, mutexes), coordination between threads, and lifecycle management are all the caller's responsibility. It fits scenarios needing fine-grained control over individual threads, or interoperability with legacy code already built around explicit thread management.

**The Task Parallel Library (TPL)** is a higher-level abstraction built on top of that: `Task` and `Task<T>` represent asynchronous operations, with constructs like `Task.Run`, `Parallel.For`, and `Parallel.ForEach` expressing and managing parallelism without manual thread bookkeeping. The TPL handles thread allocation, workload partitioning, scheduling, load balancing, cancellation, and exception handling — the caller focuses on the logic of the tasks, not the threads running them. It's primarily designed for **parallel programming** — using multiple CPU cores to go faster on independent, CPU-bound work — though its `Task` type is also the foundation `async`/`await` is built on.

**`async`/`await`** are language keywords, not a separate runtime — they're built *on top of* the TPL and simplify asynchronous code into a shape that reads like synchronous code. `async` marks a method as asynchronous; `await` suspends that method until an operation completes, without blocking the calling thread. This layer is specifically suited to **I/O-bound work** — HTTP requests, database queries, file access — where the CPU isn't the bottleneck and the point is to avoid holding a thread hostage while waiting on something external.

**What `await` actually does at the point it's written:** it designates a suspension point — the compiler-generated state machine behind the `async` method pauses there until the awaited operation completes, and control returns to *that method's own caller* in the meantime (not to some unrelated outer scope). That suspension is not an exit from the method — a `finally` block wrapping the `await` does not run just because the method is suspended there; it only runs once the method actually completes or throws, same as in synchronous code. An `async` method can itself be `await`ed by whatever calls it, which is what lets suspension chain up a whole call stack rather than stopping at one level. And if an `async` method never actually contains an `await`, it runs synchronously despite the `async` modifier — the compiler flags this with a warning, since it's almost always a sign the `async` modifier doesn't belong there.

**The underlying distinction that decides between TPL and `async`/`await`:** CPU-bound work (sorting, matrix inversion — work that depends entirely on processor time) benefits from parallelism, i.e. the TPL's `Parallel.For`/`Parallel.ForEach`. I/O-bound work (a web service call, a database round trip — work whose duration depends on something *external*) benefits from asynchrony, i.e. `async`/`await` — because waiting faster isn't possible; the only lever available is not blocking a thread while waiting.

## Example: Three Ways to Create and Run a Task

The TPL offers three equivalent-looking ways to start a `Task`, each with a slightly different shape:

```csharp
// 1. Construct, then Start — creation and execution are separate steps
Task task1 = new Task(PrintCounter);
task1.Start();

// 2. Task.Factory.StartNew — creates and starts in one call
Task task2 = Task.Factory.StartNew(PrintCounter);

// 3. Task.Run — the common shorthand for the same thing
Task task3 = Task.Run(() => PrintCounter());
```

All three run `PrintCounter` on a thread-pool thread, asynchronously with respect to the calling thread. **`Task.Run` and `Task.Factory.StartNew` are generally preferred** — they're one statement instead of two, and most code doesn't actually need creation and start to be separate operations. Reach for the two-step `new Task(...)` / `.Start()` form only when something genuinely needs to exist as a `Task` object *before* it's allowed to run.

To make the calling code wait for a task to finish rather than continuing immediately, call `.Wait()`:

```csharp
Task task1 = Task.Run(() => PrintCounter());
task1.Wait(); // blocks the calling thread until task1 finishes
```

**Expected behavior:** without `.Wait()`, the method that started `task1` can reach its own end and print "Completed" before `PrintCounter` has finished running on its own thread — the two run genuinely concurrently. `.Wait()` is a blocking call, though — in `async` code, `await task1` achieves the same ordering without blocking the calling thread, which is almost always the better choice; `.Wait()` exists mainly for synchronous contexts that can't `await`.

**This is really the same choice framed two ways: fire-and-forget, or fire-then-wait.** Starting a task — whether via `Task.Run`, `new Thread(...).Start()`, or anything else — queues that work to run on its own, independently of whatever the calling code does next; if it's a web application, that work runs in the context of the same worker process, on a thread separate from whichever thread is still handling the current request. What happens *after* that is entirely about whether the caller awaits (or otherwise waits for) the result: not awaiting it is fire-and-forget — like putting a pot of water on to boil and moving on to prep other ingredients without ever coming back to check it; awaiting it is fire-then-wait — putting the water on, doing other things for a while, then deliberately coming back and waiting for it before moving to the next step. The entire point of writing something as `async` in the first place is so it *can* be awaited later — if the intent is truly fire-and-forget with no need to ever observe completion or errors, `async`/`await` isn't adding anything that a plain `Task.Run` without an `await` wasn't already providing. See [Does await Inside a Loop Block the Caller?](/topics/concurrency-task-orchestration/await-inside-loop) for exactly what's lost — observability of completion and errors, specifically — when fire-and-forget is chosen over fire-then-wait.

## Tradeoffs & Common Mistakes

- **Reaching for raw `Thread` when the TPL would do.** Manual thread management brings back everything the TPL exists to abstract away — synchronization, exception propagation, scheduling — for no benefit unless something specifically needs per-thread control.
- **Using `Parallel.For`/TPL parallelism on I/O-bound work.** Parallelizing a loop of HTTP calls with `Parallel.ForEach` ties up a thread-pool thread *per call* for the duration of each blocking wait — `async`/`await` (or, for bounded concurrency over many calls, the tools in [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison)) does the same job without occupying threads while waiting.
- **Using `async`/`await` on CPU-bound work expecting it to go faster.** `await` doesn't parallelize anything by itself — it frees the calling thread while waiting on something external. A CPU-bound computation has nothing external to wait on, so wrapping it in `async`/`await` alone doesn't use more cores; that's what `Parallel.For`/`Task.Run` across multiple cores is for.
- **Blocking on a `Task` with `.Wait()` or `.Result` inside `async` code.** This is one of the most common sources of deadlocks in UI and ASP.NET applications with a synchronization context — it blocks the calling thread while the awaited continuation may need that very thread to resume on.
- **Not observing exceptions thrown inside a `Task`.** Exceptions in a task are captured onto the `Task` object, not thrown immediately on the calling thread — an un-awaited, un-observed `Task` that faults can fail silently.
- **Using `async void` outside of event handlers.** An `async Task` method's exceptions are captured on the returned `Task`, observable by whoever awaits it; an `async void` method has no `Task` to capture them on, so an unhandled exception inside it propagates directly and can crash the process instead of failing an awaitable gracefully. `async void` exists specifically for event handlers, which can't return a `Task` because the event signature doesn't allow it — anywhere else, `async Task` is almost always the right choice, including for a `Timer` callback, where an `async void` handler's exceptions are easy to lose track of entirely.

## Alternatives

<div class="site-note">
<strong>Site note:</strong> this case condenses and substantially rewords background material from two public .NET tutorials (dotnettutorials.net's articles on concurrency fundamentals and task-based asynchronous programming) and one source closely matching Microsoft's own "async and await" documentation, rather than reproducing any of them — included to ground the vocabulary (threads, thread pools, concurrency vs. parallelism, CPU-bound vs. I/O-bound, suspension points), not presented as the author's own first-hand case study the way the rest of this topic's cases are. The fire-and-forget-vs-fire-then-wait framing and its analogy are the author's own.
</div>

- **Need fine-grained control over individual threads**, or interoperating with legacy thread-based code? Use the `Thread`/`ThreadPool` classes directly.
- **CPU-bound, parallelizable work** — independent computations that benefit from multiple cores? Use the TPL's parallel constructs (`Parallel.For`, `Parallel.ForEach`) or `Task.Run` for a one-off CPU-bound task.
- **I/O-bound work** — anything waiting on a network call, database, or file system? Use `async`/`await` — see [Does await Inside a Loop Block the Caller?](/topics/concurrency-task-orchestration/await-inside-loop) for exactly how that waiting does and doesn't propagate through a call chain, and [Task.WhenAll vs SemaphoreSlim](/topics/concurrency-task-orchestration/task-whenall-vs-semaphoreslim) for controlling how much of it runs at once.
- **Further reading:** Stephen Cleary's ["There Is No Thread"](https://blog.stephencleary.com/2012/02/async-and-await.html) is a widely-cited, deeper explanation of exactly why `async`/`await` on I/O-bound work doesn't consume a dedicated thread while waiting — the mechanism underlying most of this case's "I/O-bound work" guidance above.
