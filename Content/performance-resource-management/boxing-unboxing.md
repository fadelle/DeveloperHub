---
title: "Boxing and Unboxing: The Hidden Cost of object Parameters"
slug: boxing-unboxing
summary: Passing a value type where an object is expected quietly allocates a box on the heap to hold it — understanding exactly when that happens (and when it doesn't) is what separates a real performance concern from a non-issue.
order: 3
sources: [Boxing and Unboxing in C#.docx]
related: [value-types-vs-reference-types, readonlymemory]
---

## Problem

A function takes an `object` parameter, and a `Car` instance (a reference type) gets passed to it — is that a performance problem? The instinct to worry about "boxing" here is understandable, but it's actually the wrong concern for this specific case: boxing and unboxing are about *value types* being treated as `object`, not about reference types at all. Passing a reference type as `object` passes the reference directly, with no allocation beyond what already existed. The real costs of an `object` parameter here are about type safety and clarity, not boxing.

## When It Applies

This applies any time a value type — `int`, `double`, a `struct` — is assigned to, cast to, or passed as a variable of type `object` (or an interface a struct implements). It also applies whenever reviewing code that uses `object` parameters broadly, to correctly separate the *boxing* cost (real, but only for value types) from the *type-safety* cost (real for both value and reference types, but not a boxing issue).

## Solution

**Boxing** happens when a value type — an `int`, a `double`, a `struct` — is assigned to a variable of type `object`. It creates a new reference-type instance on the heap to hold a copy of the value type's data, which means a real memory allocation and a copy, every time it happens. **Unboxing** is the reverse: extracting the original value type back out of a previously-boxed object, which involves a runtime type check and a cast — also not free.

**Reference types passed as `object` don't box.** If the parameter is a reference type (a class instance, like `Car`), there's no boxing or unboxing involved at all — the reference itself is passed, with no additional allocation. The thing that *does* matter for an `object` parameter, reference type or not, is what's lost by not using a more specific type:

- **Type safety:** passing through `object` loses compile-time type checking — the function may need runtime type checks or casts to recover the actual type, and a mismatch between what's expected and what's actually passed becomes a runtime error instead of a compile-time one.
- **Clarity:** a parameter typed as `Car` documents itself; a parameter typed as `object` doesn't — the reader (or the IDE) has no way to know what's actually expected without looking at the implementation.

## Example: Where the Allocation Actually Happens

```csharp
int number = 42;
object boxed = number;        // boxing: heap allocation + copy
int unboxed = (int)boxed;     // unboxing: type check + copy back
```

Contrast with a reference type going through the same shape:

```csharp
Car car = new Car();
object passed = car;          // no boxing — this is just a reference copy
Car back = (Car)passed;       // no unboxing — this is just a reference cast
```

**Expected behavior:** the `int` path allocates a new object on the heap to hold a copy of `42`; the `Car` path never allocates anything beyond the `Car` object that already existed — `passed` and `car` point at the same instance the entire time. Profiling or reviewing code for boxing-related allocations should specifically look for value types flowing into `object`-typed (or interface-typed) locations — not for reference types being passed around loosely typed, which is a design smell but not a boxing cost.

## Tradeoffs & Common Mistakes

- **Treating any `object` parameter as a boxing concern.** As above — only value types box. Flagging a `Car`-typed `object` parameter as a boxing problem in review is incorrect; the real issue there is type safety and clarity, not allocation.
- **Boxing inside a hot loop without noticing.** A generic-looking line like `list.Add(someInt)` where `list` is `List<object>` (rather than `List<int>`) boxes on every single `Add` — easy to miss because nothing about the syntax looks like an allocation.
- **Unboxing to the wrong type.** Unboxing requires an exact type match (`(int)boxedValue` fails at runtime if the boxed value was actually a `long`) — unlike many other casts, there's no implicit numeric conversion during unboxing.
- **Over-correcting by avoiding `object` everywhere, even where it's harmless.** Boxing a value type once, outside a hot path — logging, a one-off diagnostic — is not worth restructuring code to avoid. This is a cost to recognize in loops and hot paths, not a reason to ban `object` parameters universally.

## Alternatives

- **Need to avoid boxing for a value type used generically** (e.g. across many call sites)? Generic methods and generic collections (`List<T>` instead of `List<object>`, a generic method instead of an `object`-parameter one) avoid the boxing entirely, since the JIT specializes the generic code per value type instead of routing through `object`.
- **Need a type-safe parameter instead of `object`** for the type-safety/clarity concern that applies regardless of boxing? Use the specific type (`Car`), or an interface the type implements if multiple types need to be accepted — see [Polymorphism vs Generics](/topics/oop-design/polymorphism-vs-generics) for choosing between those options when the parameter needs to accept more than one concrete type.
