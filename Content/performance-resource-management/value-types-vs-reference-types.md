---
title: "Value Types vs Reference Types: How Parameters Actually Pass"
slug: value-types-vs-reference-types
summary: Every parameter in C# passes by value by default — the confusion is that for a reference type, the value being copied is the reference itself, not the object it points to.
order: 4
sources: [Reference Vs Value Types Explained.docx, Reference Type Passing Explained.docx]
related: [boxing-unboxing]
---

## Problem

"Reference types are passed by reference" is a common but imprecise shorthand that causes real confusion: it's not quite true, and the inaccuracy matters. A method that reassigns a reference-type parameter to a brand-new object does *not* change what the caller's variable points to — which surprises people who took "passed by reference" literally. Understanding exactly what gets copied, and when, is the difference between predicting this correctly and being surprised by it in production.

## When It Applies

This applies to every method call in C#, which makes it worth having exactly right rather than approximately right. It matters most in two recurring situations: debugging why a method's changes to an object "don't show up" in the caller, and deciding whether `ref`/`out` is actually needed for a given method signature.

## Solution

**C# parameters are passed by value by default** — always, for both value types and reference types. What differs between the two is *what* "the value" is:

- For a **value type** (`int`, `float`, a `struct`), the value *is* the data — passing it by value copies the actual data. Changes made to the parameter inside the method have no effect on the caller's variable.
- For a **reference type** (a class instance), the value *is the reference* — passing it by value copies the reference, not the object it points to. Both the caller's variable and the parameter now point at the *same* object, so changes made *through* that reference (mutating the object's properties) are visible to the caller. But reassigning the parameter to point at a *different* object only changes the local copy of the reference — the caller's own reference is untouched.

This is sometimes called "pass by value of the reference," and it's the precise version of what "reference types pass by reference" is gesturing at — close enough to be a useful mental shortcut, wrong enough to cause real bugs if taken literally.

**Pass by reference**, using `ref` or `out`, is a genuinely different thing: rather than copying the reference, it passes a reference *to the variable itself* — which means reassignment inside the method is visible to the caller too.

## Example: Mutating Through a Reference vs Reassigning It

```csharp
class Person
{
    public string Name { get; set; }
}

void ModifyPerson(Person person)
{
    person.Name = "Alice";   // mutates the object the caller's reference also points to
    person = new Person();   // reassigns the LOCAL copy of the reference only
    person.Name = "Bob";     // mutates the new object — invisible to the caller
}

Person p = new Person();
p.Name = "John";

ModifyPerson(p);

Console.WriteLine(p.Name); // "Alice"
```

**Expected behavior:** the output is `"Alice"`, not `"Bob"` and not `"John"`. `person.Name = "Alice"` mutates the same object `p` refers to, so that change is visible outside the method. `person = new Person()` only changes what the *local parameter* points to — `p` in the caller still points at the original object, which was already renamed to `"Alice"` one line earlier. Everything after that reassignment, including `person.Name = "Bob"`, happens to the new object that only `ModifyPerson`'s local `person` variable can see.

## Example: ref and out — Passing the Variable Itself

```csharp
// Pass by value — the default
void UpdateValue(int value)
{
    value = 100;
}

int number = 50;
UpdateValue(number);
Console.WriteLine(number); // 50 — unchanged
```

```csharp
// Pass by reference — explicit, with ref
void UpdateValue(ref int value)
{
    value = 100;
}

int number = 50;
UpdateValue(ref number);
Console.WriteLine(number); // 100 — modified
```

**Expected behavior:** with plain pass-by-value, `UpdateValue` only ever sees and modifies a copy, so the caller's `number` is unaffected. With `ref`, the method receives the variable itself (not a copy), so its reassignment is visible to the caller after the call returns — this applies to value types and reference types alike, since `ref` changes *how the parameter itself is passed*, independent of whether that parameter holds a value or a reference.

`out` is similar to `ref`, but asymmetric in intent: `ref` requires the caller's variable to already be initialized (the method can read it before writing), while `out` is meant for a method to *produce* a value the caller didn't have yet — the method must assign it before returning, and the caller doesn't need to initialize it first. `out` is typically used for returning an additional value from a method, rather than for letting a method modify something the caller already had.

## Tradeoffs & Common Mistakes

- **Assuming a reference-type parameter reassignment is visible to the caller.** As shown above — it isn't. Only mutations *through* the reference (changing a property, calling a method that mutates state) are visible; replacing what the local parameter points to is purely local.
- **Reaching for `ref`/`out` to "fix" the above, when mutation-through-reference was already enough.** If the goal was just to change an object's state, no `ref` is needed — a plain reference-type parameter already allows that. `ref`/`out` is specifically for when the method needs to *replace* the caller's reference entirely, or return an additional value through a parameter.
- **Using `ref`/`out` as a substitute for returning a tuple or a small result type.** Technically works, but usually reads worse — a method signature with several `out` parameters is harder to call correctly than one returning a named tuple or small record, and modern C# tuples largely removed the original motivation for multi-`out` signatures.
- **Forgetting value types copy on every pass.** A large `struct` passed by value copies its entire contents on every call — for a `struct` big enough that this matters, `in` (a read-only reference parameter) avoids the copy without allowing the method to mutate the caller's data.

## Alternatives

- **Need the copy-avoidance of pass-by-reference without allowing mutation?** `in` parameters pass a large `struct` by reference but prevent the method from modifying it — useful specifically for structs large enough that copying is a measurable cost.
- **Passing a value type through something `object`-typed, and want to understand the actual allocation cost of that?** See [Boxing and Unboxing](/topics/performance-resource-management/boxing-unboxing) — a related but distinct concern that only applies to value types crossing into `object`-typed territory, not to ordinary reference-type parameter passing.
