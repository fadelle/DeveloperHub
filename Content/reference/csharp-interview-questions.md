---
title: "C# Interview Questions: Quick-Reference Answers"
slug: csharp-interview-questions
summary: Rapid-fire answers to common advanced C# interview questions, spanning interfaces, generics, async, memory, and more — several topics get a full case elsewhere on this site, linked inline.
sources: [C# interview questions.docx]
---

A quick-reference list rather than a case — short, direct answers for topics that come up in interviews, with links out to the deeper treatment where this site already has one.

**Interface vs. abstract class — when to use which?** An interface defines a pure contract — no fields, no constructors, no implementation — and a class can implement several. An abstract class can hold shared state and real implementation, but a class can only inherit from one. Use an interface for a contract shared across unrelated types; use an abstract class to share a common base implementation among closely related types.

**Delegates and events.** A delegate is a reference type holding a reference to a method, enabling callbacks and methods-as-parameters. An event is a delegate with restricted access — only the declaring type can invoke it, while subscribers can only add or remove handlers — built specifically for the notify-many-listeners shape.

**The `yield` keyword.** Marks an iterator method, producing a sequence lazily, one value at a time, instead of building the whole collection up front. See [Lazy Sequences](/topics/sequences-iterators-async-streams/lazy-sequences-iterators-async-streams) for the full treatment, including `yield break`, infinite sequences, and the async equivalent.

**Generics.** Let a class, interface, or method work across multiple types while keeping compile-time type safety and avoiding the casting and boxing that an `object`-based version would need. See [Polymorphism vs Generics](/topics/oop-design/polymorphism-vs-generics) for exactly where generics stop being the right tool — specifically, once the type is chosen at runtime rather than at the call site.

**`ref` vs `out`.** Both pass a variable by reference instead of by value. `ref` requires the variable to already be initialized before the call; `out` doesn't, but requires the method to assign it before returning — `out` is for a method producing an additional result, `ref` is for a method that may read and then replace an existing value. See [Value Types vs Reference Types](/topics/performance-resource-management/value-types-vs-reference-types) for how this differs from ordinary pass-by-value.

**`async`/`await`.** Non-blocking execution — `async` marks a method as asynchronous, `await` suspends it until an operation completes without blocking the calling thread. See [Threads, the TPL, and async/await](/topics/concurrency-task-orchestration/threads-tpl-async) for when this is the right layer to reach for at all, and [Does await Inside a Loop Block the Caller?](/topics/concurrency-task-orchestration/await-inside-loop) for exactly what it does and doesn't wait for.

**Shallow copy vs. deep copy.** A shallow copy copies an object's fields, including references — the copy and the original still point at the same referenced objects. A deep copy also creates new copies of everything referenced, so the two object graphs are fully independent. Custom copy logic is usually needed once an object graph has nested reference types and true independence matters.

**The `using` statement.** Guarantees `Dispose()` is called on an `IDisposable` resource when the block exits — including when an exception is thrown — which is what prevents unmanaged resources (file handles, connections) from leaking even on the error path.

**Dependency Injection and Inversion of Control.** DI supplies a class's dependencies from outside rather than letting it construct them itself; IoC is the broader principle of letting higher-level code control what gets provided to lower-level code. See [Inheritance vs Composition](/topics/oop-design/inheritance-vs-composition) for why this pairs naturally with composition over inheritance in .NET specifically.

**Serialization formats.** XML — interoperable, human-readable, larger and slower. Binary — fast, compact, not human-readable, best for .NET-to-.NET or performance-sensitive cases. JSON — a balance of readability and size, the default for web APIs and cross-platform data interchange.

**Value types vs. reference types.** Value types store their data directly and live on the stack (for locals); reference types store a reference to heap-allocated data. Assigning or passing a value type copies the data; assigning or passing a reference type copies the reference. See [Value Types vs Reference Types](/topics/performance-resource-management/value-types-vs-reference-types) for the full breakdown, including exactly what `ref`/`out` change about this.

**Reflection.** The ability of a program to examine its own types and members at runtime, dynamically invoke methods or constructors, and create instances — used for plugin systems, generic tooling, and dynamic code generation.

**Threading and multithreading.** Concurrent execution via the `Thread`/`ThreadPool` classes or the Task Parallel Library. The core challenges are synchronization, race conditions, deadlocks, and thread safety of shared state. See [Threads, the TPL, and async/await](/topics/concurrency-task-orchestration/threads-tpl-async) for picking the right abstraction level in the first place.

**LINQ.** A unified, strongly-typed query syntax over collections, databases, XML, and other data sources, with compile-time checking and IntelliSense support regardless of the underlying source.

**Value equality vs. reference equality.** Value equality compares the actual data two objects hold; reference equality checks whether two variables point to the same instance. Override `Equals` and `GetHashCode` (and optionally `==`/`!=`) on a custom type to define what value equality means for it.
