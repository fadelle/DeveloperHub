---
title: "ConcurrentDictionary: Avoiding Check-Then-Act Races on Shared State"
slug: concurrent-dictionary
summary: The obvious "check if the key exists, then add it" pattern on a plain Dictionary has a race condition baked in the moment more than one thread can reach it — ConcurrentDictionary's Try* methods close that gap atomically.
order: 8
sources: [Concurrent Dictionary C# (Thread Safe).docx]
related: [threads-tpl-async, is-singleton-thread-safe, interlocked-thread-safety]
---

## Problem

A `Dictionary<TKey, TValue>` accessed from a single thread is perfectly safe and perfectly fine — reach for `ConcurrentDictionary` only once multiple threads (or multiple concurrent async tasks) can touch the same dictionary. The failure mode isn't usually a crash on first glance — it's a specific, easy-to-miss race in the pattern almost everyone reaches for first:

```csharp
// There are better ways to do this... but it's a useful example of what goes wrong.
if (!dictionary.ContainsKey(id))
    dictionary.Add(id, value);
```

Between the `ContainsKey` check and the `Add` call, another thread can add that same key. When this thread then calls `Add`, it throws — `Dictionary.Add` doesn't allow a duplicate key, and nothing about this code prevents two threads from both passing the `ContainsKey` check before either one calls `Add`.

## When It Applies

Reach for `ConcurrentDictionary` specifically when a dictionary is genuinely shared across multiple threads or concurrent async operations — a cache, a registry of in-flight work keyed by ID, counters keyed by some identifier. If a dictionary is only ever touched from one thread (or one logical sequential flow, even if that flow is `async`), a plain `Dictionary` is simpler and has no extra cost — concurrency safety that isn't needed is pure overhead, not a free safety margin.

## Solution

`ConcurrentDictionary` provides a set of `Try*` methods — `TryAdd`, `TryGetValue`, `TryUpdate`, `TryRemove` — each of which performs its check-and-act as a single atomic operation, closing exactly the race the plain-`Dictionary` pattern above has:

```csharp
var dictionary = new ConcurrentDictionary<string, int>();

bool added = dictionary.TryAdd(id, value);
// added is true if this call actually inserted the key,
// false if the key was already present — no exception either way
```

**Expected behavior:** if two threads call `TryAdd(id, value)` for the same `id` at the same moment, exactly one of them returns `true` (it inserted the value) and the other returns `false` (the key was already there) — there's no window between a check and an act for both threads to slip through, because the check and the act are the same atomic operation. Neither thread throws; both get an accurate, race-free answer about what happened.

## Tradeoffs & Common Mistakes

- **Reaching for `ConcurrentDictionary` by default, even single-threaded.** If a dictionary is never accessed from more than one thread or concurrent async flow, `Dictionary` is simpler and avoids `ConcurrentDictionary`'s extra synchronization overhead — this is a tool for a specific problem, not a strictly-better default.
- **Still using the check-then-act pattern on a `ConcurrentDictionary`.** Writing `if (!dict.ContainsKey(id)) dict[id] = value;` against a `ConcurrentDictionary` reintroduces the exact same race the type exists to prevent — the type being thread-safe doesn't make every *pattern* used against it thread-safe; `TryAdd` (or `GetOrAdd`/`AddOrUpdate` for more elaborate cases) has to actually be used for the atomicity to apply.
- **Assuming `ConcurrentDictionary` makes a lock unnecessary for everything touching that data.** The dictionary's own internal operations are thread-safe, but a sequence of *multiple* operations against it (read a value, compute something from it, write a different value) isn't automatically atomic as a whole — only each individual `Try*` call is.
- **An alternative worth naming explicitly: locking a plain `Dictionary`.** A `lock` around all access to a regular `Dictionary` is also a valid way to make it safe for multi-threaded use — simpler to reason about in some cases, but serializes *all* access to the dictionary rather than allowing the finer-grained concurrency `ConcurrentDictionary`'s internals provide. Which one is better is genuinely workload-dependent, not a settled default either way.

## Alternatives

- **Need the dictionary's value to be computed only once even under concurrent first access** (lazy population by multiple threads racing to populate the same key)? `GetOrAdd` handles exactly this — though its value-factory overload can run more than once under contention if not written carefully, which is worth checking against the specific overload's documented guarantees before relying on it.
- **The shared state isn't keyed lookups at all, just a single piece of state multiple threads touch?** See [Is a Singleton's Method Thread-Safe?](/topics/concurrency-task-orchestration/is-singleton-thread-safe) for the more general version of this question — a singleton's *methods* are inherently thread-safe to call concurrently; it's the singleton's *fields*, if any, that need exactly this kind of explicit protection.
