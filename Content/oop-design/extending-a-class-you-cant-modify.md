---
title: "Extending a Class You Can't Modify: Extension Methods, Decorator, and Composition"
slug: extending-a-class-you-cant-modify
summary: A library class or a sealed class needs new or modified behavior, but you can't touch its source and can't inherit from it — three ways to add functionality from the outside, and when each one fits.
order: 5
sources: [Best way to add functionality to library classes.docx, extending sealed classes.docx]
related: [inheritance-vs-composition, adapter-pattern, sqlcommand-decorator]
---

## Problem

A class from a library — or a `sealed` class in your own codebase — needs a new method, or slightly different behavior from one it already has, and the usual first instinct (inherit from it, override what's different) isn't available: the library doesn't support meaningful subclassing, or the class is `sealed` specifically to prevent inheritance. `sealed` means exactly what it says — the class cannot be directly inherited from or modified, by design.

## When It Applies

This applies whenever inheritance is off the table for a type you need to extend — either because it's `sealed`, or because it comes from a library whose base types weren't designed to be meaningfully subclassed. It's a narrower, more mechanical question than [Inheritance vs Composition](/topics/oop-design/inheritance-vs-composition): that case asks *whether* inheritance is the right relationship; this one assumes the answer is "inheritance isn't available at all" and asks what to do instead.

## Solution

Three approaches cover most of this problem, in roughly increasing order of how much new behavior they can carry:

- **Extension methods** — when the need is purely additive: new methods that read naturally as if they belonged to the type, with no state of their own and no need to intercept or modify existing methods.
- **Decorator (wrap + delegate)** — when the need is to add *or modify* behavior around the existing methods, including methods that need their own state, or need to run logic before/after delegating to the original.
- **Composition without a shared interface** — when a hook or event the library already exposes is enough, and a dedicated wrapper isn't needed at all.

## Example: Extension Methods — Purely Additive Behavior

Extension methods let you define methods as if they were instance methods of a type, even though they're really static methods in a separate class — and they work on a `sealed` class exactly the same way they work on any other type, since they never actually modify or inherit from the class:

```csharp
public sealed class SealedClass
{
    public void OriginalMethod()
    {
        Console.WriteLine("Original method");
    }
}

public static class SealedClassExtensions
{
    public static void NewMethod(this SealedClass sealedObject)
    {
        Console.WriteLine("Extended method");
    }
}
```

```csharp
SealedClass sealedObject = new SealedClass();
sealedObject.OriginalMethod(); // "Original method"
sealedObject.NewMethod();      // "Extended method"
```

**Expected behavior:** `NewMethod()` is callable directly on any `SealedClass` instance, reading exactly like a real instance method at the call site — but it has no access to the class's private state, and it cannot override or change what `OriginalMethod()` does. Extension methods are purely additive; they're the right tool only when that's all that's needed.

## Example: Decorator Pattern with Composition — Adding or Modifying Behavior

When the new functionality needs its own state, or needs to intercept or modify an existing method rather than just add a new one, wrap the sealed instance in a new class that implements the same surface and delegates to the original:

```csharp
public sealed class SealedClass
{
    public void OriginalMethod()
    {
        Console.WriteLine("Original method");
    }
}

public class DecoratorClass
{
    private readonly SealedClass _sealedObject;

    public DecoratorClass(SealedClass sealedObject)
    {
        _sealedObject = sealedObject;
    }

    public void NewMethod()
    {
        Console.WriteLine("Extended method");
    }

    public void OriginalMethod()
    {
        _sealedObject.OriginalMethod();
    }
}
```

```csharp
SealedClass sealedObject = new SealedClass();
DecoratorClass decoratorObject = new DecoratorClass(sealedObject);

decoratorObject.OriginalMethod(); // "Original method" — delegated through
decoratorObject.NewMethod();      // "Extended method" — genuinely new
```

**Expected behavior:** `DecoratorClass` can add brand-new methods (`NewMethod`) and can also wrap existing ones (`OriginalMethod`) to add logic before or after delegating to `_sealedObject` — something extension methods can't do, since an extension method can't intercept a call to the original method once code is already calling it directly. The cost is that callers now depend on `DecoratorClass` instead of `SealedClass` directly, so this only works where that substitution is acceptable at the call site. See [Wrapping ADO.NET SqlCommand](/topics/databases-sql/sqlcommand-decorator) for a real production example of exactly this shape — a decorator around `SqlCommand` that adds timing and logging without touching ADO.NET itself.

## Tradeoffs & Common Mistakes

- **Reaching for extension methods when state or interception is actually needed.** An extension method cannot hold instance state tied to a particular object, and cannot change what an existing method does — if either is needed, it's a sign the Decorator approach is the right one, not a reason to contort the extension method.
- **Letting a decorator's constructor-and-delegate boilerplate balloon.** A decorator wrapping a type with many members means forwarding every member that isn't being changed — fine for a small surface, tedious and error-prone for a large one; an interface extracted from the original type (if one doesn't already exist) can make this more maintainable by making the forwarding explicit and compiler-checked.
- **Forgetting that callers now depend on the wrapper, not the original type.** Any code expecting a `SealedClass` specifically can't accept a `DecoratorClass` instead unless both implement a shared interface — decorator-based extension works best when consumers are written against an interface in the first place, which is the same lesson as [Inheritance vs Composition](/topics/oop-design/inheritance-vs-composition).
- **General library-class guidance worth keeping in mind:** when a library class *does* support meaningful inheritance, plain subclassing and overriding is still the simplest option — these three approaches are specifically for when that door is closed, not a universal first choice over inheritance.

## Alternatives

- **The library class exposes hooks or events already** (a `protected virtual` method, a `public event`), and the extra functionality fits as a handler rather than a wrapper? Composition against those hooks — a class that holds an instance of the library class and reacts to its events — can be simpler than a full decorator, since there's no need to forward the entire original surface.
- **The mismatch is about an incompatible *interface shape*, not missing behavior** — translating one contract into another rather than adding to one? That's [Adapter](/topics/oop-design/adapter-pattern), a related but distinct shape.
- Per the [Programming Vocabulary Cheat Sheet](/reference/vocabulary-cheat-sheet), extension methods and the Decorator pattern both sit outside the catalog of "shapes" there — worth adding there as a known gap if the shape catalog is extended later.
