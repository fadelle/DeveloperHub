---
title: "Is a Singleton's Method Thread-Safe? It Depends on State, Not the Pattern"
slug: is-singleton-thread-safe
summary: Multiple threads calling the same singleton's method at once isn't automatically a problem — each call runs its own independent stack. The real question is what that method reads or writes on the shared instance itself.
order: 8
sources: [is singleton methods thread safe .docx]
related: [concurrent-dictionary, interlocked-thread-safety]
flagged: true
flagNote: This page reworks and substantially condenses a public Stack Overflow answer on singleton thread safety rather than reproducing it — included because the underlying explanation is genuinely useful and correct, not because the original answer is the author's own first-hand case.
---

## Problem

"Is my singleton thread-safe?" is usually asked as if singletons have one universal answer — but a singleton being shared across threads doesn't by itself make calling its methods unsafe. The actual risk is narrower and easy to misjudge in either direction: assuming a singleton is automatically dangerous just because multiple threads touch it, or assuming it's automatically safe just because "each thread calls the method independently."

## When It Applies

Reach for this distinction any time a singleton (registered as such in DI, or a classic static-instance singleton) is called from code that can run concurrently — multiple requests in a web app, multiple background workers, anything where more than one thread might be inside the singleton's methods at the same moment.

## Solution

A singleton object has two distinct things worth separating clearly:

- **Fields** — stored in memory, genuinely shared across every thread that touches the singleton. Nothing guarantees their consistency under concurrent access unless they're explicitly synchronized.
- **Methods being called** — each call from each thread executes independently, on that thread's own call stack. Calling `Thread.CurrentThread.ManagedThreadId` from inside the method would return a different value per calling thread, because each thread is running its own, separate execution of that method's code — local variables, parameters, and the call stack itself are never shared between concurrent calls, even though they're calls to the same method on the same object.

**The actual rule:** if a singleton is stateless — no fields being read or written across calls — its methods are inherently safe to call concurrently from any number of threads, because there's nothing shared between those calls beyond the method's own code, which every thread executes independently. The danger only appears once the singleton *has* fields that get read or written during those calls — at that point, those specific fields need explicit protection (locks, atomic operations, or a thread-safe collection), not the singleton pattern itself.

## Example: Stateless vs Stateful Singleton Methods

```csharp
// Stateless — safe to call from any number of threads concurrently.
// Nothing here is shared between calls; every local variable is
// independent per call, per thread.
public class Calculator
{
    public int Add(int a, int b) => a + b;
}
```

```csharp
// Stateful — NOT safe without explicit protection.
// _total is a shared field; concurrent calls can interleave their
// read-modify-write on it and lose updates.
public class Counter
{
    private int _total;

    public void Add(int amount)
    {
        _total += amount; // read, modify, write — not atomic
    }
}
```

**Expected behavior:** calling `Calculator.Add` from a hundred threads at once produces exactly the results each call's own arguments predict, every time — there's no shared mutable state for concurrent calls to interfere with. Calling `Counter.Add` from multiple threads at once, by contrast, can lose updates — two threads can both read the same `_total`, both add their own amount to that same stale value, and both write back, with one of the two increments effectively disappearing. The method signatures look equally "thread-safe" at a glance; the actual answer depends entirely on whether there's shared state being mutated underneath.

## Tradeoffs & Common Mistakes

- **Assuming a singleton is unsafe just because it's shared.** As above — a stateless singleton's methods are safe to call concurrently by default, and adding locking around a genuinely stateless method only adds contention for no benefit.
- **Assuming a singleton is safe because "it's just a method call."** The opposite mistake — once a singleton has fields that calls read or write, those calls are not automatically safe just because each one "runs independently"; the independence is in the call stack, not in the shared fields the calls touch.
- **Protecting the wrong thing.** Wrapping an entire method body in a lock when only one field access inside it is actually shared state serializes far more than necessary — identify specifically which fields are shared and mutated, and scope protection to those, not to the whole method by reflex.
- **Forgetting that "shared field" includes anything the field refers to, not just the field itself.** A singleton field holding a mutable collection (not itself protected, e.g. a plain `List<T>` instead of a thread-safe collection) is exactly as unsafe as a shared primitive — see [ConcurrentDictionary](/topics/concurrency-task-orchestration/concurrent-dictionary) for the collection-specific version of this same underlying problem.

## Alternatives

- **The shared state is a dictionary or other keyed collection?** See [ConcurrentDictionary](/topics/concurrency-task-orchestration/concurrent-dictionary) for the thread-safe collection types built specifically for this, rather than hand-rolling locks around a plain collection.
- **The singleton's state genuinely needs to vary per logical operation** (per request, per tenant) rather than being shared across the whole app's lifetime? That's usually a sign the type shouldn't be a singleton at all — a Scoped or Transient DI lifetime (see the lifecycle note in the [Programming Vocabulary Cheat Sheet](/reference/vocabulary-cheat-sheet)) avoids the shared-state problem entirely by giving each logical operation its own instance.
