---
title: A Timer Calling Async Work Can Exhaust the Thread Pool
slug: timer-async-thread-pool-exhaustion
summary: A Timer firing every second, each tick kicking off an async I/O call, looks harmless — until the I/O gets slow enough that the thread pool runs out of room to run the next tick at all.
order: 6
sources: [Calling async fxn inside a timer.docx]
related: [threads-tpl-async, task-whenall-vs-semaphoreslim]
---

## Problem

A `System.Timers.Timer` fires on an interval — say, every second — and each tick calls an `async` function that does some I/O-bound work. Individually, each tick looks fine: it reaches its `await`, suspends, and control returns so the timer can fire the next tick on schedule. The trap is in what "suspends" actually costs: each in-flight `await` holds a reservation on the thread pool while it waits. If the I/O being awaited is slow — slower than the timer's interval — ticks start piling up faster than they finish, each one holding its own thread-pool slot, until the pool has no room left to run the *next* tick at all. At that point the timer doesn't fire late; it effectively stalls, waiting for a thread-pool slot that won't free up until one of the backed-up calls finally completes.

## When It Applies

This applies to any recurring trigger — a `Timer`, a polling loop, a scheduled background job — that calls `async` I/O-bound work on an interval shorter than, or comparable to, that work's worst-case latency. It's easy to miss in testing, where the I/O dependency usually responds quickly and the problem never manifests — it shows up specifically when that dependency slows down in production, which is exactly the moment a system most needs to keep working.

## Solution

The failure mode: each timer tick that reaches its `await` doesn't block a thread, but it does hold a spot in the thread-pool's bookkeeping until that `await` completes. A healthy system has ticks starting and finishing faster than new ones arrive, so the number of in-flight ticks stays low and bounded. Once the awaited I/O takes long enough that ticks accumulate faster than they drain, the number of concurrently in-flight calls grows without bound — and because the thread pool itself is a shared, finite resource, enough backed-up ticks can starve *everything else* trying to use the thread pool too, not just this timer.

```
Tick 1 → await (slow I/O, still pending)
Tick 2 → await (slow I/O, still pending)
Tick 3 → await (slow I/O, still pending)
...
Tick N → thread pool has no room left to even start
```

The fix isn't "use a different timer" — it's making sure a tick can't start new work while the previous one(s) are still outstanding, so the number of concurrently in-flight calls stays bounded regardless of how slow the I/O gets.

## Example: Bounding Concurrent Ticks Instead of Letting Them Pile Up

The simplest fix is to track whether a tick is already in flight and skip starting a new one if so — trading "always fire exactly on schedule" for "never let more than one call run at a time":

```csharp
private int _isRunning; // 0 = idle, 1 = in flight

private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
{
    if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        return; // previous tick hasn't finished — skip this one

    try
    {
        await DoIoBoundWorkAsync();
    }
    finally
    {
        Interlocked.Exchange(ref _isRunning, 0);
    }
}
```

**Expected behavior:** if `DoIoBoundWorkAsync` is still running when the next tick fires, that tick is skipped entirely rather than starting a new overlapping call — the number of concurrently in-flight calls is capped at one, no matter how slow the I/O gets or how many ticks the timer fires in the meantime. The tradeoff is explicit: during a slow period, some ticks simply don't run, rather than all of them queuing up and eventually starving the thread pool. For a scenario that needs some real concurrency rather than a strict cap of one, a bounded tool from [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison) — a `SemaphoreSlim` sized to the number of overlapping calls that's actually safe — replaces the single `_isRunning` flag with a small fixed limit instead of one.

## Tradeoffs & Common Mistakes

- **Assuming `await` means "no cost while waiting."** It's true that `await` doesn't block a thread — but it isn't free either; the suspended call still occupies a slot in the thread pool's own bookkeeping until it resumes, and enough of those slots held at once is exactly what causes this failure.
- **Not noticing the problem until the dependency is already slow in production.** Because the failure only manifests once the awaited I/O is slow enough, this bug is often invisible in normal testing — the timer, the async method, and the test environment can all look completely healthy right up until the dependency degrades.
- **Treating a stalled timer as "the timer is broken."** The timer itself is usually fine — it's waiting for a thread-pool slot, same as everything else backed up behind the same exhaustion. Debugging this by looking at the timer in isolation, rather than at how many calls are actually in flight, tends to miss the real cause.
- **Fixing this by just increasing the thread pool's minimum thread count.** That raises the threshold before exhaustion happens, but doesn't address the underlying issue — ticks are still piling up unboundedly whenever the I/O is slow enough; it only buys more headroom before the same failure mode reappears.

## Alternatives

- **Need bounded concurrency with more than one call allowed at a time**, rather than a strict "skip if anything is already running" rule? See [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison) — a `SemaphoreSlim` sized to a safe concurrency limit generalizes the single-flag approach above.
- **The recurring work is really a continuous stream rather than a fixed-interval poll?** A dedicated background worker reading from a bounded `Channel<T>` (also covered in [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison)) avoids the timer-interval-vs-latency mismatch entirely, since there's no fixed tick rate to outrun in the first place.
- **The Timer's callback itself needs review** — `Timer` callbacks that are `async void` (as in the example above) can't be awaited by the timer and silently swallow unhandled exceptions outside a `try`/`catch` inside the callback; see [Threads, the TPL, and async/await](/topics/concurrency-task-orchestration/threads-tpl-async) for why `async void` is specifically risky and where it's unavoidable (event handlers, which this effectively is).
