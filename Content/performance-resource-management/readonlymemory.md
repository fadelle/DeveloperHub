---
title: "ReadOnlyMemory<T>: Read-Only Views Without Copying"
slug: readonlymemory
summary: ReadOnlyMemory<T> exposes a read-only view over an array without copying it — closer to a reference type in cost, but immutable in a way ordinary reference types aren't.
order: 1
sources: [C# ReadOnlyMemory usage.docx]
related: [boxing-unboxing, value-types-vs-reference-types]
---

## Problem

Exposing an internal array or byte buffer from a class usually forces a choice between two bad options: return the array directly (callers can mutate it, breaking encapsulation) or copy it before returning (safe, but an allocation and a copy on every access). `ReadOnlyMemory<T>` is the escape from that tradeoff: it lets a type safely expose a read-only *view* over an array without copying it, and without letting the caller mutate the underlying data.

## When It Applies

Reach for `ReadOnlyMemory<T>` when a type owns a buffer — a `byte[]`, a `float[]`, any array — and needs to expose it to callers who should be able to *read* it but never *change* it, without paying for a defensive copy on every access. It's a natural fit for fields like `private readonly ReadOnlyMemory<byte> _bytes;` that are populated once and read many times.

## Solution

There are two ways to create a `ReadOnlyMemory<T>`:

**Implicit conversion (short and clean).** The .NET library defines this operator on the type itself:

```csharp
public static implicit operator ReadOnlyMemory<T>(T[] array);
```

It takes a `T[]` and returns a `ReadOnlyMemory<T>`:

```csharp
float[] arr = { 1f, 2f, 3f };
ReadOnlyMemory<float> mem = arr;   // implicit conversion
```

**Explicit construction (manual), using the constructor directly:**

```csharp
float[] arr = { 1f, 2f, 3f };
ReadOnlyMemory<float> mem = new ReadOnlyMemory<float>(arr);
```

Use the implicit form (`=> _values`) for clean code, or the explicit form (`=> new ReadOnlyMemory<float>(_values)`) when the conversion should be visually obvious at the call site.

**What `ReadOnlyMemory<T>` actually is:** think of it as working like a reference type in terms of cost — it doesn't copy the underlying array — but *unlike* an ordinary reference type, it's immutable: if the wrapped data is a list, nobody can add, insert, or update through the `ReadOnlyMemory<T>` view. To actually read the data inside a `ReadOnlyMemory<T>`, convert it with `.ToArray()`.

## Example: A Read-Only Byte Field Without a Defensive Copy

A common use: a field that's populated once from an external source and exposed read-only from then on.

```csharp
private readonly ReadOnlyMemory<byte> _bytes;
```

It can be filled directly from any byte-producing source, relying on the implicit operator:

```csharp
_bytes = Encoding.UTF8.GetBytes(data);
```

This works because of the implicit operator the type defines for arrays:

```csharp
public static implicit operator ReadOnlyMemory<T>(T[]? array);
```

**Expected behavior:** `_bytes` holds a read-only view over the array `Encoding.UTF8.GetBytes(data)` produced — no copy of that array is made to create the view, and nothing outside this type can mutate the bytes through `_bytes`. Reading the data back out requires `.ToArray()`, which *does* copy — the cost of a copy is deferred to the point where mutable access is actually needed, rather than paid defensively up front.

## Example: Implicit Conversion as a General Pattern — the `Money` Wrapper

The same implicit-operator idea generalizes beyond arrays — it's the same mechanism that lets a type convert to and from another type transparently at the call site:

```csharp
public readonly struct Money
{
    public decimal Amount { get; }

    public Money(decimal amount)
    {
        Amount = amount;
    }

    // Convert decimal → Money
    public static implicit operator Money(decimal value)
        => new Money(value);

    // Convert Money → decimal
    public static implicit operator decimal(Money money)
        => money.Amount;
}
```

```csharp
Money price = 10.99m;   // uses the implicit operator — a decimal works directly as a Money
decimal n = price;      // uses the implicit operator — a Money works directly as a decimal
```

**Expected behavior:** `price` holds a `Money` value constructed from the literal `10.99m` with no explicit `new Money(...)` call needed, and `n` extracts the underlying `decimal` the same way — both directions of the conversion are defined once, on the type, and apply everywhere that type is used.

## Tradeoffs & Common Mistakes

- **Forgetting `ReadOnlyMemory<T>` is still backed by the original array.** If something elsewhere still holds a mutable reference to the *same* underlying array (rather than going through the `ReadOnlyMemory<T>` view), that code can still mutate the data — the immutability guarantee is about the view, not a guarantee that the underlying memory can never change from anywhere.
- **Reaching for `.ToArray()` out of habit.** Calling `.ToArray()` to read data copies it — fine occasionally, but defeats the entire point if done repeatedly in a hot path. Prefer working with `.Span` for read-only access when a full copy isn't actually needed.
- **Using explicit construction everywhere out of caution.** The implicit operator exists precisely so simple array-to-`ReadOnlyMemory<T>` assignments don't need a `new ReadOnlyMemory<T>(...)` wrapper cluttering the code — reserve the explicit form for places where the conversion should be visually obvious.

## Alternatives

<div class="site-note">
<strong>Site note:</strong> the two alternatives below (<code>ImmutableArray&lt;T&gt;</code> and <code>ReadOnlySpan&lt;T&gt;</code>) extend beyond the source material to place <code>ReadOnlyMemory&lt;T&gt;</code> among its closest relatives in the BCL — included for navigation, not asserted as the author's own tested recommendation.
</div>

- **Need the array to be fully immutable, not just read-only *through this view*?** `ImmutableArray<T>` guarantees the underlying data itself can never change, at the cost of a real copy on construction.
- **Need to pass a read-only slice without even the small overhead of `Memory<T>`'s wrapper, and don't need to store it (e.g. across an `await`)?** `ReadOnlySpan<T>` is the stack-only equivalent — faster, but it can't be stored in a field or used across an `async` boundary the way `ReadOnlyMemory<T>` can.
