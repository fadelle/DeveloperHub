---
title: "Passing Behavior as Data: Adapting Function Signatures with Func<T>"
slug: func-as-behavior-parameter
summary: A method can't be passed, returned, or assigned to a variable directly — but a delegate like Func<T> can, which is what makes it possible to accept wildly different method signatures through one parameter, by wrapping the mismatch in a small lambda adapter.
order: 6
sources: [Dynamic Func.docx]
related: [adapter-pattern, polymorphism-vs-generics]
---

## Problem

A method needs to accept "some function to call later" as a parameter — but the functions that might be passed in have different signatures from each other, and none of them necessarily match whatever signature the parameter itself declares. A method in C# isn't a value that can be passed around on its own; `Func<T>` and its relatives are the values — and getting a method with an incompatible signature into a `Func<T>`-shaped parameter needs a small translation step, the same shape as adapting any other incompatible interface.

## When It Applies

Reach for this whenever a method needs to accept behavior to invoke later, rather than a value to use right away — deferred or conditional execution, a callback, lazy initialization where the expensive work should only run if and when it's actually needed. It's a lighter-weight alternative to defining a full interface (as in the Strategy shape from the [Programming Vocabulary Cheat Sheet](/reference/vocabulary-cheat-sheet)) when only one method's worth of behavior needs to vary, not a whole family of related methods.

## Solution

Define the parameter as simply as the calling code actually needs — often a `Func<T>` that takes no inputs and just returns a value, even if the functions that will eventually be passed in take their own parameters:

```csharp
public void Check(Func<string> acquire)
{
    var value = acquire();
    // ... use value
}
```

Any function can be adapted to fit this signature, regardless of its own parameter list, by wrapping it in a lambda that takes nothing and calls the real function inside:

```csharp
public string Calc(string str)
{
    return str.ToUpper();
}
```

```csharp
Check(() => Calc("hello"));
```

**The lambda `() => Calc("hello")` is the adapter** — it matches `Func<string>`'s signature (no parameters, returns `string`) exactly, while the work it does inside calls `Calc`, which has a completely different signature of its own. `Check` never needs to know `Calc` exists, or what arguments it needed — it only ever sees something matching `Func<string>`.

**When the signatures already match exactly, skip the wrapper:**

```csharp
public string Calc2()
{
    return "default";
}

Check(Calc2);          // signatures already match — pass the method group directly
Check(() => Calc2());  // equivalent, but unnecessary ceremony here
```

## Example: Why This Needs a Delegate at All

The underlying reason a method can't just be passed directly the way a value can:

- **You can pass a `Func` as an argument.** You can't do that with a method on its own, because a parameter accepts objects, and a method by itself isn't an object — `Func<T>` is.
- **You can return a `Func`.** Same reasoning — a method return type has to be an object (or a value type); wrapping the method in a delegate is what makes "returning a method" possible at all.
- **You can assign a `Func` to a variable.** A bare method name can't be assigned to a variable on its own; a `Func` (or any delegate) holding a reference to that method can.
- **You can construct a `Func` at runtime** — build up what to execute based on conditions evaluated while the program is running, rather than only what was fixed at compile time.

**Expected behavior — lazy, conditional execution:** because a `Func<T>` is a value, it can be handed to a method well before it's actually invoked, and that method can decide *whether* to call it at all:

```csharp
public T GetOrCompute<T>(Func<T> compute, bool shouldCompute)
{
    return shouldCompute ? compute() : default!;
}
```

The expensive work inside `compute` never runs unless `shouldCompute` is true — the caller handed over *how* to compute the value, not the value itself, and `GetOrCompute` is what decides whether that computation actually happens. This is the same underlying mechanism [Lazy-Loading and Caching Per-Request Data with ??=](/topics/performance-resource-management/lazy-loading-caching) relies on, just expressed as an explicit parameter instead of a cached field.

## Tradeoffs & Common Mistakes

- **Wrapping a method in a lambda when it already matches the target signature.** `Check(() => Calc2())` works, but it's unnecessary — `Check(Calc2)` does the same thing with one less layer of indirection to read.
- **Reaching for `Func<T>` adapters to paper over a design that really wants a named interface.** A single ad-hoc `Func` is a lightweight fit for one varying operation; once several *related* operations need to vary together (not just one method, but a coherent set of behaviors), an interface — the Strategy shape — usually reads more clearly than a handful of loosely related `Func` parameters.
- **Forgetting that a captured lambda holds onto its closure.** `() => Calc("hello")` closes over whatever it references from the surrounding scope — if that scope includes something expensive or mutable, the lambda keeps it alive and visible for as long as the `Func` itself is held, which is easy to overlook when the lambda looks like a tiny, free throwaway wrapper.
- **Overusing `Func`/`Action` parameters as a substitute for clear naming.** A method whose entire contract is "call this function with these inputs" can become hard to read if every parameter is itself a delegate — a few well-named `Func`/`Action` parameters are fine; a method signature dominated by them is a sign the responsibility might be better expressed as a small interface instead.

## Alternatives

- **Several related behaviors need to vary together, not just one method?** That's the Strategy shape from the [Programming Vocabulary Cheat Sheet](/reference/vocabulary-cheat-sheet) — define an interface instead of threading multiple `Func`/`Action` parameters through a method signature.
- **The mismatch is about an entire object's interface, not a single function signature?** See [Adapter Pattern](/topics/oop-design/adapter-pattern) — the same translate-the-shape idea, one level up, applied to a whole type rather than a single method.
