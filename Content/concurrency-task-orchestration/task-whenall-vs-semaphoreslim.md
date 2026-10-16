---
title: Task.WhenAll vs SemaphoreSlim: Waiting vs Limiting
slug: task-whenall-vs-semaphoreslim
summary: Two primitives that get confused constantly because they're almost always used together — one waits for completion, the other limits how much runs at once. Neither does the other's job.
order: 3
sources: [Task.WhenAll and SemaphoreSlim.docx]
related: [threads-tpl-async, await-inside-loop, concurrency-tool-comparison]
---

## Problem

`Task.WhenAll` and `SemaphoreSlim` show up together so often that it's easy to assume they do related halves of the same job. They don't. One of them **waits**; the other one **limits**. Confusing the two leads to two different mistakes: using `Task.WhenAll` alone on a large batch and accidentally running everything at once, or adding a semaphore without realizing you still need `Task.WhenAll` (or equivalent) to know when the batch is actually done.

## When It Applies

Reach for this distinction before writing any "process N things with some concurrency cap" loop — it's the foundational pair that [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison) builds on. If you can't immediately say which of "waits for completion" and "limits concurrency" each line of your code is responsible for, start here before reaching for `Parallel.ForEachAsync` or a `Channel<T>`.

## Solution

### `Task.WhenAll(...)` — wait for a group of tasks to finish

```csharp
var tasks = messages.Select(m => ProcessAsync(m));

await Task.WhenAll(tasks);
```

Start all tasks → wait until every task is finished → continue. **It does not limit concurrency.** If there are 100 tasks, all 100 can run concurrently.

### `SemaphoreSlim(...)` — limit how many tasks can run at the same time

```csharp
var semaphore = new SemaphoreSlim(30);

await semaphore.WaitAsync();
try
{
    await ProcessAsync();
}
finally
{
    semaphore.Release();
}
```

Maximum 30 operations may run simultaneously. **It does not wait for all tasks** — it only controls how many can enter.

### Together — the typical pattern

```csharp
var semaphore = new SemaphoreSlim(30);

var tasks = messages.Select(async message =>
{
    await semaphore.WaitAsync();
    try
    {
        await ProcessAsync(message);
    }
    finally
    {
        semaphore.Release();
    }
});

await Task.WhenAll(tasks);
```

With 100 messages total: a maximum of 30 run simultaneously, and the call waits until all 100 finish. **`SemaphoreSlim` = concurrency limit. `Task.WhenAll` = wait for completion.** Neither one substitutes for the other.

## Example: Batch(30) vs Semaphore(30) — Why They're Not the Same Throughput

It's tempting to assume "30 at a time" means the same thing whether you batch strictly or gate with a semaphore. It doesn't, because of what happens when one item finishes early.

**Fixed batches of 30, waiting for each batch to fully drain:**

```
30 running → wait all 30 → 30 running → wait all 30 → 10 running → wait all 10
```

Every batch runs only as fast as its *slowest* member — a batch with one unusually slow item leaves the other 29 slots idle until that one finishes.

**A rolling semaphore of 30, replacing finished work immediately:**

```
30 running → when one message finishes, the next waiting one takes its slot → keep ~30 running → until all are done
```

A semaphore keeps roughly 30 operations running continuously, because a freed permit is immediately claimed by the next waiting item instead of waiting for an entire batch to drain. **With a semaphore, processing 70 items is faster than batching them 30 at a time with `Task.WhenAll` between batches** — the difference is entirely in what happens to the *other* 29 slots while one slow item finishes.

**Expected behavior applied to a Pulsar-style consumer:**

| Approach | Behavior |
|---|---|
| Batch version | Receive batch of 50 → process max 30 concurrently → wait until all 50 finish → receive next batch |
| Rolling semaphore version | Receive messages continuously → keep max 30 processing at all times → no waiting for batch completion |

**Rolling semaphore = highest throughput. Batch + `Task.WhenAll` = simpler code. Sequential await = safest but slowest.**

## Tradeoffs & Common Mistakes

- **Using `Task.WhenAll` alone on a large, un-gated collection.** With no semaphore (or equivalent), `Task.WhenAll(messages.Select(ProcessAsync))` starts *all* of them concurrently — fine for 10 items, potentially catastrophic for 10,000 against a connection-pooled database.
- **Forgetting that a semaphore alone doesn't tell you when you're done.** `SemaphoreSlim` bounds concurrency but has no concept of "the batch is finished" — you still need `Task.WhenAll` (or a worker/`Channel<T>` lifecycle) to know every item has been processed.
- **Mistaking fixed-batch-of-N for a true concurrency-30 pipeline.** They converge only when every item takes the same amount of time; real workloads rarely do, and the batch-boundary idle time is easy to miss in testing with small, uniform test data.
- **Forgetting `Release()` in `finally`.** A permit that's never released silently shrinks the effective concurrency limit over the life of the process — and because `SemaphoreSlim` gives no error for "leaked" permits, this failure mode is invisible until throughput mysteriously degrades.

## Alternatives

- **Simpler code over maximum throughput:** fixed batches with `Task.WhenAll` between them — easier to reason about, at the cost of idle time at each batch boundary.
- **Maximum throughput for a continuous or highly variable-latency workload:** the rolling semaphore pattern, or move further to the comparison in [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison), where `Parallel.ForEachAsync` and a bounded `Channel<T>` are weighed against this exact tradeoff.
- **A quick memory trick to keep the two straight:** `Task.WhenAll` = "wait for everybody." `SemaphoreSlim` = "only N people may enter."
