---
title: "Lazy Sequences: IEnumerable<T>, yield return, and IAsyncEnumerable<T>"
slug: lazy-sequences-iterators-async-streams
summary: IEnumerable<T> is a recipe, not a plate of cookies — it doesn't run until something enumerates it. yield return is how you write that recipe by hand, and IAsyncEnumerable<T> carries the same idea across an await.
order: 1
sources: [IEnumerable Vs List Example.docx, Iterators (yield return).docx, CSharp-Async-Streams-Guide.docx]
related: []
---

## Problem

`IEnumerable<T>` and `List<T>` look interchangeable — both can be `foreach`ed, both hold a sequence of `T`. They aren't interchangeable, because `IEnumerable<T>` describes *how to produce* a sequence, lazily, while `List<T>` *is* the sequence, already produced and sitting in memory. Treat an `IEnumerable<T>` backed by a database query as if it were a `List<T>`, and the same query runs again every single time it's enumerated — including by accident, including twice in the same method.

```csharp
var names = GetNames();  // <<-- no database query happens here

foreach (var name in names)  // <<-- NOW the query actually runs
{
    Console.WriteLine(name);
}

foreach (var name in names)  // <<-- it runs again
{
    Console.WriteLine(name);
}
```

## When It Applies

This applies any time a method returns `IEnumerable<T>` (or `IAsyncEnumerable<T>`) backed by something expensive — a database query, a file read, a network call — rather than an in-memory collection. It also applies whenever you're deciding how to *write* a lazy sequence yourself, or whether a sequence should be synchronous (`IEnumerable<T>`, consumed with `foreach`) or asynchronous (`IAsyncEnumerable<T>`, consumed with `await foreach`) in the first place.

## Solution

**`IEnumerable<T>` is lazy.** It is not saved in memory automatically the way a `List<T>` is — it's a *plan* for how to fetch or produce data, not the data itself. Calling `GetNames()` in the example above does not immediately query the database; only *enumerating* the result — a `foreach`, a `.ToList()`, a `.First()` — actually runs it, and running it again re-runs it, because the underlying query is deferred until enumeration happens.

> **Quick analogy:** `IEnumerable<T>` is like a recipe — it tells you how to bake cookies, but doesn't bake them yet. `List<T>` is like a plate of cookies — already baked and ready to eat. Walking back into the kitchen (enumerating the `IEnumerable<T>` again) bakes a fresh batch again, rather than handing you the same plate.

If only one database call is wanted, **materialize the sequence immediately** with `.ToList()`, `.ToArray()`, `.Single()`, or `.First()`:

```csharp
var names = GetNames().ToList(); // query executes once, results are saved

foreach (var name in names)
{
    Console.WriteLine(name); // uses the in-memory list
}

foreach (var name in names)
{
    Console.WriteLine(name); // still the in-memory list, no new DB call
}
```

## Example: Writing an Iterator by Hand with yield return

An **iterator** is a method that returns a sequence of values one at a time, instead of building the whole collection first. In C#, iterator methods return `IEnumerable<T>` (synchronous) or `IAsyncEnumerable<T>` (asynchronous), and use `yield return` to produce each value — `yield break` stops the sequence early. Values are generated lazily: nothing runs until a `foreach` actually starts pulling items.

```csharp
IEnumerable<int> Numbers()
{
    Console.WriteLine("Returning 1");
    yield return 1;

    Console.WriteLine("Returning 2");
    yield return 2;

    Console.WriteLine("Returning 3");
    yield return 3;
}

var bla = Numbers(); // just stores the iterator — produces nothing yet

foreach (var n in bla)
{
    Console.WriteLine("Loop saw " + n);
}
```

**Expected output** — the interleaving is the whole point: each `Console.WriteLine` inside `Numbers()` only runs right before the matching `yield return`, driven by the `foreach`, not all at once up front:

```
Returning 1
Loop saw 1
Returning 2
Loop saw 2
Returning 3
Loop saw 3
```

This is also why an infinite sequence is safe to write and doesn't hang or exhaust memory on its own — it never tries to materialize "everything" at once:

```csharp
IEnumerable<int> Numbers()
{
    var number = 0;
    while (true)
        yield return number++;
}
```

The caller is responsible for eventually stopping — with `Take(10)`, a `break`, or cancellation — since nothing else will. The same laziness that makes an infinite sequence safe also makes `yield return` memory-efficient for ordinary large sequences: reading a file one line at a time, instead of `File.ReadAllLines(...)` loading the whole thing up front —

```csharp
IEnumerable<string> ReadLines()
{
    using var reader = new StreamReader("bigfile.txt");
    string line;
    while ((line = reader.ReadLine()) != null)
        yield return line;
}
```

— reads one line at a time, exactly matching how much the caller has actually consumed, and it reads more naturally than manually building and returning a `List<string>` one `Add` call at a time.

## Example: Streaming Data Asynchronously with IAsyncEnumerable&lt;T&gt;

The return type determines which keyword applies: a `Task<T>`/`ValueTask<T>` is `await`ed — wait for one eventual result. An `IEnumerable<T>` is `foreach`ed — read a synchronous sequence. An `IAsyncEnumerable<T>` is read with `await foreach` — it waits for *each next item*, not for one single result.

A useful mental model: an asynchronous stream is like a tunnel — items pass through one at a time, sometimes immediately and sometimes after an unpredictable delay. The timing doesn't need to be random; the important point is simply that the next item may not be ready yet.

```csharp
await foreach (var message in consumer.Messages(cancellationToken))
{
    // Process each message as it arrives
}
```

**Why not `await Messages(...)` directly?** Because the result is a *sequence*, not a `Task` or `ValueTask` representing one eventual value — `await foreach` repeatedly awaits the sequence's `MoveNextAsync()` operation behind the scenes, once per item, rather than awaiting the whole thing once.

| Type | Next item | Typical use |
|---|---|---|
| `IEnumerable<T>` | Must be obtained synchronously — waiting blocks the thread | In-memory collections, or values generated immediately |
| `IAsyncEnumerable<T>` | Can be awaited without blocking a thread | Messages arriving over a network connection, paginated results, anything with real latency between items |

**Expected behavior:** a consumer like `consumer.Messages(cancellationToken)` yields messages as they actually arrive — the loop body runs once per message, each `await` inside the implicit `MoveNextAsync()` call suspending only until the *next* message is ready, never blocking a thread while waiting the way a synchronous blocking read would.

**Remember, as the throughline across all three:** `List<T>` is a stored, finite collection with a `Count`. `IEnumerable<T>` is a synchronous sequence, finite or infinite, that doesn't run until enumerated. `IAsyncEnumerable<T>` is the same lazy idea, carried across an `await`, for sequences whose next item isn't synchronously available.

## Tradeoffs & Common Mistakes

- **Enumerating a database-backed `IEnumerable<T>` more than once without meaning to.** Passing the same un-materialized `IEnumerable<T>` into two separate `foreach` loops, or into both a `.Count()` check and a later loop, silently doubles (or triples) the number of database round trips — with no compiler warning, since this is entirely legal C#.
- **Materializing too early out of habit.** Calling `.ToList()` immediately on every `IEnumerable<T>` defeats the memory and streaming benefits this case is built around — reserve materialization for when the sequence is genuinely reused, not as a reflex.
- **Writing an infinite `yield return` sequence without a caller-side stop condition.** The sequence itself doesn't hang, but a `foreach` over it without a `Take(n)`, a `break`, or cancellation will — the safety of an infinite iterator depends entirely on the consumer, not the producer.
- **Calling `await Messages(...)` instead of `await foreach`.** `IAsyncEnumerable<T>` is a sequence, not an awaitable single value — attempting to `await` it directly doesn't compile, which usually surfaces this mistake immediately rather than silently.

## Alternatives

- **Need the sequence consumed more than once, or need `Count`/indexing?** Materialize it — `.ToList()` or `.ToArray()` — immediately after fetching, and work with the materialized collection from then on.
- **Need backpressure or concurrency control over how an `IAsyncEnumerable<T>` stream is consumed**, rather than just reading it in order? See [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison) — a bounded `Channel<T>` is the natural next step once a stream needs multiple concurrent workers instead of one sequential `await foreach`.
