---
title: "Code Smells: switch on Type, if/else Chains, and the Case for Polymorphism"
slug: code-smells-and-polymorphism
summary: A switch on type or a long if/else chain isn't automatically wrong — but it's a recurring clue that behavior belongs in the objects themselves, not in the code calling them.
order: 2
sources: [code_smells_explained_csharp_both_approaches.docx]
related: [inheritance-vs-composition, polymorphism-vs-generics]
---

## Problem

A code smell is a warning sign that code might have a design problem even though it still works correctly. One of the most common: code that checks an object's type — a string label, a `switch`, a chain of `if`/`else` — and branches to different behavior for each one.

```csharp
switch (animalType)
{
    case "Dog": Console.WriteLine("Woof"); break;
    case "Cat": Console.WriteLine("Meow"); break;
    case "Cow": Console.WriteLine("Moo"); break;
}
```

**The problem isn't this one switch — it's what happens when a new case shows up.** Adding `Duck` means changing this switch. If the same kind of type-check is scattered across several parts of the application, adding one new type means hunting down and updating every one of them.

```csharp
if (animalType == "Dog")
    Console.WriteLine("Woof");
else if (animalType == "Cat")
    Console.WriteLine("Meow");
else if (animalType == "Cow")
    Console.WriteLine("Moo");
else if (animalType == "Duck")
    Console.WriteLine("Quack");
```

A "pile" of `if`/`else` is the same smell in a different shape — many conditions stacked one after another, performing the same kind of type-based decision. The longer the chain gets as types are added, the easier it becomes to miss a spot when requirements change.

## When It Applies

Reach for this whenever you notice the same kind of type-check — a `switch` on a label, a chain of `is`/`==` comparisons — making the same decision in more than one place, or growing every time a new case is added. It does **not** apply to every `switch` or `if`/`else` on principle — see Tradeoffs below for exactly where the line is.

## Solution

**Polymorphism lets each type provide its own implementation of a shared operation**, so the calling code never has to ask what type it's holding — it just calls the operation, and the right behavior runs automatically based on the actual object.

**The one-sentence version worth memorizing:** instead of asking an object "What type are you?" and deciding what to do, tell it "Do your job."

## Example: Fixing It with an Abstract Class

An abstract class declares the operation; each child class supplies its own implementation with `override`.

```csharp
public abstract class Animal
{
    public abstract void MakeSound();
}

public class Dog : Animal
{
    public override void MakeSound() => Console.WriteLine("Woof");
}

public class Cat : Animal
{
    public override void MakeSound() => Console.WriteLine("Meow");
}
```

Calling `animal.MakeSound()` runs the `Dog` or `Cat` implementation according to the actual object — no `switch`, no `if`/`else` chain anywhere in this picture.

## Example: Upcasting — Passing a Child to a Parent Parameter

This is the mechanic that makes the fix above actually usable: a method can declare a parameter typed as the parent (`Animal`), and callers can pass any child (`Dog`, `Cat`) because each child **is an** `Animal`. C# performs this conversion — called upcasting — implicitly, with no cast needed.

```csharp
public static void PlaySound(Animal animal)
{
    animal.MakeSound();
}

PlaySound(new Dog()); // Output: Woof
PlaySound(new Cat()); // Output: Meow
```

Step by step: `PlaySound` expects an `Animal`. `Dog` inherits from `Animal`, so `new Dog()` can be passed directly. Inside `PlaySound`, the parameter is *typed* as `Animal`, but it still *refers to* a `Dog` object. Calling `animal.MakeSound()` invokes `Dog.MakeSound()` at runtime — the exact same function body handles `Cat` too, with zero changes.

The same thing works for a variable assignment, not just a parameter:

```csharp
Animal animal = new Dog(); // Valid: Dog is an Animal
animal.MakeSound();         // Output: Woof
```

**Expected behavior — the rule worth internalizing:** the *parent* type controls which members you can access at compile time (you can only call what `Animal` declares through an `Animal`-typed reference) — but the *actual child object* determines which overridden implementation actually runs at runtime. These are two different questions, resolved at two different times.

## Example: Fixing It with an Interface Instead

An interface defines a contract rather than a shared base class: any type that implements `IAnimal` must provide `MakeSound()`, without inheriting from a common `Animal` ancestor.

```csharp
public interface IAnimal
{
    void MakeSound();
}

public class Dog : IAnimal
{
    public void MakeSound() => Console.WriteLine("Woof");
}

public class Cat : IAnimal
{
    public void MakeSound() => Console.WriteLine("Meow");
}
```

```csharp
public static void PlaySound(IAnimal animal)
{
    animal.MakeSound();
}

PlaySound(new Dog()); // Output: Woof
PlaySound(new Cat()); // Output: Meow
```

**Expected behavior:** identical observable outcome to the abstract-class version — `PlaySound` accepts `IAnimal`, not a specific class; a `Dog` can be passed because it implements `IAnimal`; `animal.MakeSound()` invokes whichever implementation the actual object provides. The difference between the two fixes isn't in what they accomplish here — it's in the relationship each one models, covered next.

## Tradeoffs & Common Mistakes

- **Treating every `switch` or `if`/`else` as a smell on sight.** They're not automatically bad — plenty of conditionals have nothing to do with type-based behavior selection. The smell is specifically **repeatedly checking an object's type to choose its behavior**; a `switch` over, say, HTTP status code ranges or a genuinely fixed, closed set of cases isn't this problem.
- **Choosing between abstract class and interface by habit rather than by relationship.** They solve this exact problem identically — the real choice is a modeling question, covered in [Inheritance vs Composition](/topics/oop-design/inheritance-vs-composition).
- **Forgetting a C# class can only inherit one base class, but implement many interfaces.** This alone often settles the choice: if a type already needs to inherit from something else, or needs to satisfy more than one contract, an interface is usually the only option that fits.

**Abstract class vs. interface, side by side:**

| Question | Abstract class | Interface |
|---|---|---|
| Relationship | `Dog : Animal` — Dog **is an** Animal | `Dog : IAnimal` — Dog **follows the contract** |
| Method syntax | `abstract` method + `override` in child | Method declaration + public implementation |
| Shared code and state | Can hold fields, constructors, and shared implementations | Typically a pure contract; modern C# allows default interface members |
| More than one? | A class can inherit only one base class | A class can implement multiple interfaces |
| Function parameter | `PlaySound(Animal animal)` | `PlaySound(IAnimal animal)` |

Choose an abstract base class when there's a meaningful shared base with real common code or state to inherit. Choose an interface when what's needed is primarily a shared capability or contract, especially across otherwise-unrelated types. Neither choice requires a `switch` or `if`/`else` chain to select behavior per type — that's the whole point of reaching for either one.

## Alternatives

- **The type to use isn't known until runtime — it comes from a list, a config value, or a database row, not from code written explicitly for that type?** That's a step beyond this case — see [Polymorphism vs Generics](/topics/oop-design/polymorphism-vs-generics) for exactly where generics stop being the right tool and factories/resolvers take over.
- **Still deciding whether two types should share a base class at all**, independent of the switch/if-else smell? See [Inheritance vs Composition](/topics/oop-design/inheritance-vs-composition) for the broader "is-a vs. has-a" question this case's abstract-class option assumes is already settled.
