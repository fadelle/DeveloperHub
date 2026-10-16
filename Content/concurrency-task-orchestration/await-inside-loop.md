---
title: Does await Inside a Loop Block the Caller?
slug: await-inside-loop
summary: What await inside a called method actually pauses, and the three concurrency patterns that follow from it — sequential, fire-and-forget, and start-both-then-wait.
order: 2
sources: [Await_Inside_Loops_Three_Scenarios.docx]
related: [threads-tpl-async, task-whenall-vs-semaphoreslim, concurrency-tool-comparison]
---

## Problem

A loop calls an async method per item — a message, a row, a request. Whether the *next* iteration starts immediately or waits depends entirely on whether the loop `await`s the `Task` it gets back, not on whether the called method itself contains `await`. Getting this backwards is one of the most common async mistakes: a loop that looks like it's "processing everything" can actually be submitting work far faster than it completes it, with no visibility into that gap until something downstream falls over.

```csharp
foreach (var message in messages)
{
    _ = ProcessAsync(message); // caller does not await
}

async Task ProcessAsync(Message message)
{
    await DatabaseCallAsync(message);
    await PublishAsync(message);
}
```

`ProcessAsync` runs synchronously until it reaches its first incomplete `await`, then returns an incomplete `Task`. Because the loop ignores that `Task`, the next iteration starts immediately. **The internal `await` pauses `ProcessAsync` — not the caller.**

## When It Applies

This is the first thing to get right before reaching for any concurrency-limiting tool (`SemaphoreSlim`, `Parallel.ForEachAsync`, a `Channel<T>` — see [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison)): all of those tools assume you already understand which of the three patterns below you're using, since each one changes correctness, not just performance. Reach for this case when you're writing or reviewing any loop — a message consumer, a batch job, a request handler — that calls an `async` method per item, and especially before you optimize a loop "to go faster" by dropping an `await`.

## Solution

Three patterns answer the same question — "does the loop wait?" — differently, with different consequences for reliability and throughput.

| Pattern | Next iteration starts | Reliability | Real throughput |
|---|---|---|---|
| Await function 1, then function 2 | After both finish | Strong — completion and errors observed | Safe but sequential |
| Await neither function | Almost immediately | Weak unless tasks are tracked elsewhere | Submission looks fast; completion may not be |
| Start both, then `await Task.WhenAll` | After both finish | Strong when both tasks are observed | Often best if the operations are independent |

**The bottom line:** the presence of `await` inside a called function does not make its caller wait. Waiting propagates through the call chain only when each caller awaits the `Task` it receives.

Underneath all three, the same mental model applies: calling an async method does not create a thread. It runs on the current thread until an incomplete `await`, then returns a `Task`; a continuation resumes later on an available ThreadPool thread. A waiting `Task` normally occupies no thread for true asynchronous I/O — but large numbers of completions, synchronous sections, `Task.Run`, or blocking calls can still create ThreadPool pressure even when no single call looks expensive.

## Example: Sequential — Await Both Functions

```csharp
foreach (var message in messages)
{
    await Function1Async(message);
    await Function2Async(message);
}
```

**Execution:** function 1 completes, then function 2 starts; the next message begins only after both complete. **Threads:** true asynchronous I/O normally holds no dedicated thread while waiting — a ThreadPool thread runs the synchronous sections and later continuations. **Reliability:** both returned tasks are observed, exceptions propagate to the loop, and success can be tied to an acknowledgment. **Throughput:** usually the lowest of the three patterns, since operations and messages are fully sequential — if function 1 takes 100 ms and function 2 takes 100 ms, one message takes about 200 ms, excluding other work.

**Use this when** function 2 depends on the result or side effect of function 1 — correctness requires the ordering, so there's nothing to gain by changing it.

## Example: Fire-and-Forget — Await Neither Function

```csharp
foreach (var message in messages)
{
    _ = Function1Async(message);
    _ = Function2Async(message);
}
```

**Execution:** both methods start; as soon as each returns an incomplete `Task`, the loop moves to the next message without waiting for completion. **Reliability:** the loop observes neither completion nor exceptions — shutdown, consumer closure, or early acknowledgment can leave work incomplete or cause redelivery. **Throughput:** the loop's *submission* rate may look very high, but *completed-and-acknowledged* throughput can be lower once DB pools, Redis, memory, or publishing become the real bottleneck.

**The main danger:** a counter incremented when work starts measures submitted work, not successfully completed work — this is the single easiest way to convince yourself a system is faster than it actually is.

## Example: Start Both, Then Task.WhenAll

```csharp
foreach (var message in messages)
{
    Task first = Function1Async(message);
    Task second = Function2Async(message);
    await Task.WhenAll(first, second);
}
```

**Execution:** the two functions can overlap for the current message; the next message starts only once both tasks complete. **Reliability:** `Task.WhenAll` observes aggregate completion — a failure in either task prevents the message from being treated as successfully processed. **Throughput:** often better than sequential awaits when both operations are independent, since total time is roughly the *slower* of the two operations rather than their sum.

**Dependency rule:** do not run the two functions together if function 2 needs the result or side effect of function 1 — fall back to sequential awaits in that case.

## Tradeoffs & Common Mistakes

- **Treating a high submission rate as a throughput win.** A non-awaited call measures how fast work can be *queued*, not how fast it's *reliably completed* — count messages as done only once processing, publication, and acknowledgment have all succeeded.
- **Assuming non-awaited calls are "free."** Large numbers of completions, synchronous sections, `Task.Run`, or blocking calls can still create ThreadPool pressure, even though no single non-awaited call blocks the loop.
- **Running independent operations sequentially out of habit.** If function 2 genuinely doesn't depend on function 1, sequential awaiting is leaving throughput on the table for no reliability benefit — `Task.WhenAll` gets the same observability with better latency.
- **Running dependent operations concurrently to "go faster."** If function 2 needs function 1's result, `Task.WhenAll` doesn't just fail to help — it's incorrect, since function 2 may start before function 1's side effect has happened.

## Alternatives

- **Function 2 depends on function 1:** sequential awaits — there is no safe alternative.
- **Functions are independent for one message:** start both, then `await Task.WhenAll`.
- **Many messages must run concurrently:** add bounded concurrency on top of this pattern with `Parallel.ForEachAsync`, `SemaphoreSlim`, or a bounded `Channel<T>` — see [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison) for the full comparison, built on exactly this foundation:

```csharp
await Parallel.ForEachAsync(
    messages,
    new ParallelOptions
    {
        MaxDegreeOfParallelism = 30,
        CancellationToken = cancellationToken
    },
    async (message, ct) =>
    {
        Task dbTask = ReadDatabaseAsync(message, ct);
        Task redisTask = ReadRedisAsync(message, ct);
        await Task.WhenAll(dbTask, redisTask);
        await PublishAsync(message, ct);
        await consumer.AcknowledgeAsync(message.MessageId);
    });
```

A capacity warning worth carrying forward: 30 messages with two independent operations each can create roughly 60 downstream operations at once — the database pool, Redis capacity, and producer limits all need to support that load, regardless of which concurrency tool is managing the 30.
