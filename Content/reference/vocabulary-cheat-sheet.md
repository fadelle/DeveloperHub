---
title: Programming Vocabulary Cheat Sheet
slug: vocabulary-cheat-sheet
summary: Plain-English meanings for the design/architecture words that come up constantly in reviews and discussions, plus a catalog of the "shapes" most design patterns reduce to.
sources: [Programming Vocabulary Cheat Sheet (plain English) (1).docx, Design Patterns Links.docx]
---

This page translates common programming/design keywords into simple, practical meanings you can confidently use in discussions — and then shows that most design patterns are really just a handful of repeating "shapes" with different names.

## Core words you hear all the time

| Word | Plain meaning | Example |
|---|---|---|
| **Intent** | Why this thing exists | `class EmailSender` — intent is to send emails, not store users or format HTML |
| **Responsibility** | What this class is allowed to decide (rule: one class, one responsibility) | Repository → data access. Factory → object creation. Resolver → choosing an implementation |
| **Behavior** | What the object can do — methods | `repo.Save(order);` |
| **State** | What the object remembers — fields/properties | `string ApiKey; bool IsActive;` |
| **Abstraction** | Hiding details you don't want to think about | `IRepository` hides SQL, Mongo, connection details |
| **Contract** | A promise: "if you implement this, you must provide these methods" | `interface IRepository` |
| **Coupling** | How much things depend on each other — high coupling = hard to change, low = flexible | Depends on an interface → low coupling. Depends on a concrete class → high coupling |
| **Cohesion** | How focused a class is — high cohesion = does one thing well | A smell is a class with a grab-bag of unrelated methods |

## Architecture / design words

| Word | Plain meaning | Rule |
|---|---|---|
| **Polymorphism** | Same interface, different behavior (`PgRepository` vs `MongoRepository` behind `IRepository`) | — |
| **Inheritance** | Reusing behavior via a base class | Use when objects truly *are* the same thing |
| **Composition** | Building behavior by combining objects | Prefer composition over inheritance |
| **Dependency Injection** | Giving objects what they need from outside | `OrderService(IRepository repo)` |
| **Factory** | A place where objects are created | Use when creation depends on runtime data |
| **Resolver** | A chooser — maps a key to an implementation | — |
| **Lifecycle / Lifetime** | How long an object lives | Singleton → whole app, Scoped → per request, Transient → per use |

## Code quality words

- **Smell** — something feels wrong (a `switch` on type, a pile of `if`/`else`).
- **Refactor** — improve code without changing behavior.
- **Boilerplate** — repetitive code you have to write every time.

## The most important word: Shape

**Shape** means the structure of code, not its details. When developers say "these two things have the same shape," they mean the same pattern, the same responsibilities, the same flow — different names, same idea:

```csharp
interface Factory {
    T Create(Key key);
}
interface Resolver {
    T Resolve(Key key);
}
```

Both are "key-based selection, input → output." Experienced developers recognize the shape, not the syntax — which is why a new pattern name rarely means a genuinely new idea.

## Translating review comments

| When someone says | They mean |
|---|---|
| "This violates SRP" | "This class decides too many things" |
| "The abstraction is wrong" | "We're hiding the wrong details" |

## The most common shapes, with examples

### Factory / Resolver shape (most common)
`Key / Input → choose → object`. Code shape: `T Get(Key key)`. Examples: `repo = factory.Create(type)`, `provider = resolver.Resolve(key)`, `services.GetRequiredService<T>()`. Use for runtime choice between multiple implementations, replacing switch/if chains — see [Polymorphism vs Generics](/topics/oop-design/polymorphism-vs-generics) for a full worked example.

### Pipeline shape (flow)
`Input → step → step → step → Output`. Code shape: `var result = Step3(Step2(Step1(input)));`. Examples: ASP.NET middleware, validation chains, request processing.

### Strategy shape (behavior swap)
`Same call → different behavior`. Code shape: an `IStrategy` interface with one `Execute()` method. Examples: payment methods, discount rules, sorting algorithms. Often combined with the Factory shape. For the lightest-weight version of this shape — one method's behavior varying, not a whole interface — see [Passing Behavior as Data with Func&lt;T&gt;](/topics/oop-design/func-as-behavior-parameter).

### Template shape (fixed flow, variable steps)
Same algorithm, some steps change. An abstract base class defines `Run()` calling `Step1()` then an abstract `Step2()` that subclasses override. Examples: framework base classes, lifecycle hooks, processing skeletons.

### Adapter shape (translation)
`A → translate → B`. A class implementing a target interface while wrapping a legacy dependency internally. Examples: wrapping third-party APIs, legacy-to-new interfaces, DTO mappers. Worked example: [Adapter Pattern: Bridging an Incompatible Interface](/topics/oop-design/adapter-pattern).

### Composite shape (tree)
`Whole = parts + parts + parts`. An `IComponent` with `Leaf` and `Composite` implementations. Examples: UI trees, file systems, menu hierarchies.

### Observer / event shape (notify)
`Something happens → many listeners`. Code shape: `event Action Changed;`. Examples: .NET events, pub/sub systems, message buses. Worked example: [Pub/Sub: The Mechanism Behind Event-Driven Messaging](/topics/software-architecture/pub-sub-messaging).

## The big insight

Most design patterns are just names for these shapes. If you recognize the shape, you already know the solution — the rest is vocabulary.

## Further reading on these patterns

<div class="site-note">
<strong>Site note:</strong> these are external links, not this site's own content — kept here as a reading list rather than reproduced, since the source material for this section was itself just a list of links rather than original explanation.
</div>

- [Adapter Pattern (GeeksforGeeks)](https://www.geeksforgeeks.org/adapter-pattern/)
- [Decorator Pattern (freeCodeCamp — Basic Design Patterns)](https://www.freecodecamp.org/news/the-basic-design-patterns-all-developers-need-to-know/)
- [Strategy Pattern (Refactoring.Guru, C# examples)](https://refactoring.guru/design-patterns/strategy/csharp/example)
- [Template, Builder, Factory, State (The Dev Piece)](http://www.thedevpiece.com/design-patterns-that-every-developer-should-know/)
- [Composite Pattern (C# Corner)](https://www.c-sharpcorner.com/article/composite-patterns-in-C-Sharp/)
