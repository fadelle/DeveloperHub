---
title: "Atomic Operations and Thread Safety with Interlocked"
slug: interlocked-thread-safety
summary: Atomic does not mean thread-safe — an atomic operation can still sit inside a race condition. Knowing exactly what Interlocked protects, and what it doesn't, is the difference.
order: 10
sources: [CSharp_Thread_Safety_Guide.docx]
related: [is-singleton-thread-safe, concurrent-dictionary, threads-tpl-async]
---

## Problem

`Interlocked.Increment`, `Interlocked.Exchange`, and `Interlocked.CompareExchange` make a single operation atomic — but "atomic" and "thread-safe" are not the same claim, and treating them as interchangeable is where real bugs hide. An atomic operation can protect one variable perfectly while the surrounding code still contains a race condition, because the *rule* that needs to hold true spans more than that one variable. The central discipline this case is built around: **protect the complete operation that must remain correct when threads overlap** — not just the one line that happens to have an atomic method available for it.

## When It Applies

This applies to any field — a counter, a cached reference, a one-time-initialization flag — that's read or written from more than one thread or concurrent task. It's the lower-level counterpart to [Is a Singleton's Method Thread-Safe?](/topics/concurrency-task-orchestration/is-singleton-thread-safe): that case establishes *which* fields need protection in the first place (shared, mutated fields — not local variables, not stateless methods); this case is about *how* to protect them once you've identified them.

## Solution

Three terms get used close together and conflated constantly — worth having exactly right:

| Term | Meaning | Example |
|---|---|---|
| **Atomic operation** | Takes effect as one indivisible action relative to other threads | `Interlocked.Increment` updates a counter atomically |
| **Race condition** | Correctness depends on the timing or ordering of concurrent actions | Two threads read 5, then both write 6 |
| **Thread-safe code** | Preserves its intended behavior when used concurrently | Every increment is counted, and related constraints remain valid |

**How they relate:** atomic operations are tools for preventing *particular* races — they don't automatically make an entire method thread-safe. Thread safety is a property of the overall design, including every path that touches the shared state. A field like `_count` may be accessed by several threads; a *local* variable belongs to one method invocation — but a local variable can still hold a *reference* to an object other threads also access, and that local reference doesn't make the referenced object private or thread-safe.

## Example: Interlocked.Increment — Why Ordinary ++ Loses Updates

`_count++` looks like one operation but isn't: it's read the current value, calculate the next value, write it back — three steps, not one.

```csharp
_count++;
```

If `_count` starts at 5, this interleaving is entirely possible with two threads:

| Step | Thread A | Thread B |
|---|---|---|
| 1 | Reads 5 | |
| 2 | | Reads 5 |
| 3 | Calculates 6 | |
| 4 | | Calculates 6 |
| 5 | Writes 6 | |
| 6 | | Writes 6 |

**Expected (wrong) result:** 6, not 7 — one update is silently lost, and because thread scheduling varies run to run, this bug can pass most of the time and only surface occasionally.

The fix is to make the whole read-modify-write sequence atomic:

```csharp
using System.Threading;

private int _count;

public int RecordEvent()
{
    return Interlocked.Increment(ref _count);
}
```

**Expected behavior:** two concurrent calls starting from 5 produce 6 and 7 in some order — never two 6s — because `Interlocked.Increment` performs the read-modify-write as a single indivisible step. `Interlocked.Decrement` and `Interlocked.Add(ref _count, delta)` work the same way for subtraction and arbitrary deltas; all return the new value. One caveat worth keeping in mind: mixing `Interlocked` increments with *ordinary* `_count++` calls elsewhere in the same codebase can still lose updates — every writer has to use a compatible protocol, not just some of them.

## Example: Interlocked.Exchange — Atomically Taking a Shared Reference

`Exchange` replaces a field's value and returns what it held immediately before, as one atomic step — useful whenever exactly one caller should "claim" a resource for cleanup, even under concurrent calls.

```csharp
// Class member; nullable reference types are enabled.
private IDisposable? _watch;

public void ReleaseWatch()
{
    var watch = Interlocked.Exchange(ref _watch, null);
    if (watch is null)
        return;

    watch.Dispose();
}
```

**When two threads call `ReleaseWatch()` concurrently:** assume `_watch` initially refers to object `W`. One thread exchanges `W` for `null` and receives `W` back; the other thread exchanges `null` for `null` and receives `null`, so it returns immediately. Either thread can win the race — but only one of them ever receives the original reference, which is exactly what makes this safe for cleanup: repeated or concurrent calls won't dispose the same object twice, as long as the same object is never republished into `_watch` afterward and cleanup only ever happens through this one method.

**What this does *not* protect:** the exchange only protects the act of replacing the reference — not the object `W` itself. A different thread that already captured a reference to `W` *before* this exchange ran can still be using it while `Dispose()` executes. And if `Dispose()` throws, `_watch` has already become `null` by that point — a later call to `ReleaseWatch()` will not automatically retry the cleanup, since as far as the field is concerned, there's nothing left to release.

## Example: Protecting a Check-Then-Act Decision — lock vs CompareExchange

An atomic increment can still sit inside a race if the *rule* being enforced spans more than the increment itself:

```csharp
// Unsafe if the requirement is that _count never exceeds 10.
if (_count < 10)
    Interlocked.Increment(ref _count);
```

Two threads can both observe `9`, both pass the check, and both increment — producing `10` and `11`. Each individual increment was atomic and correct; the *combined* check-then-increment was not, because nothing stopped both threads from passing the check before either one's increment took effect.

**Fix 1 — a lock around the whole decision**, the simplest correct option when more than one step needs to stay consistent:

```csharp
private readonly object _countLock = new object();
private int _count;

public bool TryIncrement()
{
    lock (_countLock)
    {
        if (_count >= 10)
            return false;

        _count++;
        return true;
    }
}
```

Only one thread at a time executes the block. Starting at 9, one thread increments to 10; the next thread to acquire the lock sees 10 and correctly returns `false`. Every access that participates in this rule needs to go through the *same* lock object — a lock protects nothing if some code paths use it and others don't.

**Fix 2 — CompareExchange, for a lock-free conditional update:**

```csharp
// Alternative implementation using only atomic updates.
public bool TryIncrement()
{
    while (true)
    {
        int current = Volatile.Read(ref _count);
        if (current >= 10)
            return false;

        int observed = Interlocked.CompareExchange(
            ref _count, current + 1, current);

        if (observed == current)
            return true;

        // Another thread changed the value; read and retry.
    }
}
```

`CompareExchange` stores `current + 1` into `_count` *only if* `_count` still equals `current`, and always returns whatever value it observed before the attempt. If that observed value matches `current`, this call's attempt succeeded; otherwise, something else changed `_count` in the meantime, and the loop reads the fresh value and retries. **Expected behavior:** functionally equivalent to the lock-based version for this specific rule, with no blocking — at the cost of being noticeably harder to read and to extend. A `lock` is usually easier to maintain once the decision involves more than one field.

## Example: Run-Once Initialization with CompareExchange

A common narrower use of `CompareExchange`: guaranteeing exactly one caller, among any number of concurrent callers, proceeds past a given point.

```csharp
private int _started;

public void StartOnce()
{
    if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        return;

    StartWork(); // Application-specific method.
}
```

**Expected behavior:** only the single caller whose `CompareExchange` call actually changes `_started` from `0` to `1` proceeds to call `StartWork()`; every other concurrent caller sees a non-zero value returned and returns immediately. This guarantees exactly one *attempt* through this path — it does **not** tell other callers when startup has actually finished, and if `StartWork()` throws, `_started` remains `1` forever, so a naive retry won't happen automatically. Completion tracking, failure handling, and retry all need additional state on top of this pattern, or a higher-level initialization mechanism (like `Lazy<T>`) built for that purpose.

## Tradeoffs & Common Mistakes

- **Treating "atomic" as a synonym for "thread-safe."** The whole point of this case: an atomic operation protects one indivisible action, not the broader rule or invariant the surrounding code is trying to maintain.
- **Protecting the write but not the check.** `if (_count < 10) Interlocked.Increment(...)` is the canonical version of this mistake — each half is individually fine; the gap between them is the race.
- **Assuming `Volatile.Read` makes an increment atomic.** It doesn't — `Volatile.Read` provides a read with volatile memory ordering and returns an accurate snapshot at that instant, but that value can change immediately afterward, and it does nothing to make a subsequent `_count++` or a check-then-act sequence atomic.
- **Creating a new lock object per call.** A `lock` only coordinates callers that share the *same* lock object — a fresh `object()` created inside the method being "protected" synchronizes nothing, since every call locks on its own private instance.
- **Mixing synchronization strategies for the same field.** One code path using `Interlocked`, another using a plain `_count++`, a third using a `lock` — none of these compose safely together; every writer to a given piece of shared state needs to follow the *same* protocol.
- **Trusting a clean test run as proof.** Race conditions are timing-dependent — a passing concurrent test run demonstrates the race didn't manifest that time, not that it's impossible.

## Alternatives

| Requirement | Typical choice |
|---|---|
| Increment or add to an independent counter | `Interlocked.Increment` / `Interlocked.Add` |
| Take a reference or replace one value | `Interlocked.Exchange` |
| Change a value only when it matches an expected value | `Interlocked.CompareExchange` |
| Check a condition and update several related values | `lock` around the whole operation |
| Coordinate a collection shared by threads | An appropriate concurrent collection, or consistent locking |

For that last row specifically, see [ConcurrentDictionary](/topics/concurrency-task-orchestration/concurrent-dictionary) — the collection-shaped version of exactly the same underlying discipline: protect the complete operation, not just the piece that happens to have an atomic method available.
