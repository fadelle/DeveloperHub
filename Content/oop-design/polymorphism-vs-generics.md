---
title: "Polymorphism vs Generics: Choosing the Right Tool for Runtime Variation"
slug: polymorphism-vs-generics
summary: When a type is chosen at runtime from data or config and its behavior differs per type, generics are the wrong tool — even though a generic class with T is often the first thing that looks like it fits.
order: 3
sources: [Polymorphism limitations.docx, common interface for multiple repos.docx, Generic vs Polymorphism (abstract classes and interfaces).docx, Generics vs Polymorphism (Abstraction).docx]
related: [code-smells-and-polymorphism, inheritance-vs-composition, adapter-pattern]
---

## Problem

A design regularly looks like this: a base class with shared properties, two or more child classes with different properties, and a decision — driven by a list, a config value, or a database row — about *which* child type to create and populate at runtime. The instinct is almost always the same: write a generic class with `T`, create `T` via a factory, and `switch` on the type to populate `T`'s specific properties. That instinct is half right and half wrong, and the wrong half — the `switch` — is the part that actually causes problems:

```csharp
// The smell:
if (type == "openai") return new OpenAi { ApiKey = ... };
if (type == "github") return new GithubAi { Token = ... };
```

Every new type added to this list means modifying existing code — a direct violation of the Open/Closed Principle, and the "factory" slowly turns into a god-object that knows about every concrete type in the system.

## When It Applies

This case applies specifically when **the concrete type is chosen at runtime** (from a list, a config file, a database row — not known at compile time) **and the concrete types have genuinely different behavior or properties** — not just different data shapes. The deciding rule: **all generic types should be known at compile time, not runtime.** If `T` is only ever used explicitly — `AiService<OpenAiProvider>`, `AiService<GithubAiProvider>` — written directly in code rather than resolved from a list or config, generics are fine. The moment the type comes from runtime data, this case applies instead.

## Solution

**The correct definition of polymorphism (simple and accurate):** a common base class or interface exposes behavior via abstract or virtual methods, and concrete classes implement that behavior differently. **Not fields. Not data shape. Behavior.**

What polymorphism relies on:

- A common contract — an interface or abstract base class.
- Abstract or virtual methods — what varies.
- Concrete implementations — how it varies.
- Callers that depend only on the contract — no casting, no switches.

```csharp
interface IPaymentProvider
{
    Task PayAsync(decimal amount);
}
class StripeProvider : IPaymentProvider
{
    public Task PayAsync(decimal amount) { /* ... */ }
}
class PaypalProvider : IPaymentProvider
{
    public Task PayAsync(decimal amount) { /* ... */ }
}

IPaymentProvider provider = Resolve();
await provider.PayAsync(100);
```

**What polymorphism does *not* rely on:** accessing child fields, checking concrete types, serialization shape, or generics for runtime selection. If a design needs any of those, polymorphism is not the right tool for that part of it — reach for a DTO instead if the variation is really in *data shape* rather than behavior.

**The one-sentence rule that resolves most of these designs:** *if `T` is chosen at runtime and behavior differs, don't use generics — use polymorphism + factories.* And the corollary worth memorizing on its own: **polymorphism is about what an object does, not what it contains.**

The fix, concretely, is to push population into the concrete type instead of switching on type to populate a generic one:

```csharp
abstract class AiProvider
{
    public string Name { get; set; }
    public abstract void Initialize(AiConfig config);
}

class OpenAiProvider : AiProvider
{
    public string ApiKey { get; set; }
    public override void Initialize(AiConfig config)
    {
        ApiKey = config.ApiKey;
    }
}
class GithubAiProvider : AiProvider
{
    public string Token { get; set; }
    public override void Initialize(AiConfig config)
    {
        Token = config.Token;
    }
}
```

Each class populates its own properties; no generic or shared code needs to know about `OpenAiProvider` or `GithubAiProvider` specifically. And — the harder realization — **the generic class usually isn't needed at all.** A generic wrapper like `class AiService<T> where T : AiProvider { public T Provider { get; } }` gains nothing from `T` once the concrete type is resolved at runtime instead of specified at the call site; a plain `class AiService { public AiProvider Provider { get; } }` does the same job with less ceremony.

## Example: One Interface for Multiple Concrete Repos — Resolving by Key

The simplest version of this problem: several repositories implement the same contract, and the right one is chosen per request.

```csharp
interface IRepository
{
    string Key { get; }
    void Save(Item item);
}

class PgRepository : IRepository
{
    public string Key => "pg";
}
class MongoRepository : IRepository
{
    public string Key => "mongo";
}
```

Because the choice is dynamic — per request — a factory is genuinely needed:

```csharp
interface IRepositoryFactory
{
    IRepository Create(string type);
}
class RepositoryFactory : IRepositoryFactory
{
    private readonly IDictionary<string, IRepository> _repos;

    public RepositoryFactory(IEnumerable<IRepository> repos)
    {
        _repos = repos.ToDictionary(r => r.Key);
    }

    public IRepository Create(string key)
    {
        if (!_repos.TryGetValue(key, out var repo))
            throw new InvalidOperationException($"Unknown repository: {key}");

        return repo;
    }
}
```

```csharp
services.AddSingleton<IRepository, PgRepository>();
services.AddSingleton<IRepository, MongoRepository>();
services.AddSingleton<IRepositoryFactory, RepositoryFactory>();
```

Usage — the consuming service never branches on type at all:

```csharp
class OrderService
{
    private readonly IRepositoryFactory _repoFactory;

    public OrderService(IRepositoryFactory repoFactory)
    {
        _repoFactory = repoFactory;
    }

    public void Process(string repoKey, Order order)
    {
        var repo = _repoFactory.Create(repoKey);
        repo.Save(order);
    }
}
```

**Expected behavior:** `_repoFactory.Create("pg")` returns the registered `PgRepository` instance; an unknown key throws immediately with a clear message rather than silently picking a default. Adding a third repository means registering a new `IRepository` implementation in DI — `OrderService` and `RepositoryFactory` never change.

## Example: Factory + Resolver — Resolving and Populating a New Instance Per Credential

A harder variant of the same problem: instead of choosing between pre-built singletons, each item in a *list of credentials* needs to produce a **new, populated instance** of the matching provider type.

```csharp
class AiCredential
{
    public string ProviderType { get; set; } // "openai" | "github"
    public string OpenAiApiKey { get; set; }
    public string GithubToken { get; set; }
}
```

A factory *per provider* does the creation and population — this is the piece that replaces the `switch`:

```csharp
interface IAiProviderFactory
{
    string ProviderType { get; }
    AiProvider Create(AiCredential credential);
}
class OpenAiProviderFactory : IAiProviderFactory
{
    public string ProviderType => "openai";
    public AiProvider Create(AiCredential credential)
    {
        return new OpenAiProvider
        {
            Name = "OpenAI",
            ApiKey = credential.OpenAiApiKey
        };
    }
}
class GithubAiProviderFactory : IAiProviderFactory
{
    public string ProviderType => "github";
    public AiProvider Create(AiCredential credential)
    {
        return new GithubAiProvider
        {
            Name = "GitHub AI",
            Token = credential.GithubToken
        };
    }
}
```

A resolver replaces the `switch` entirely with a dictionary lookup:

```csharp
class AiProviderResolver
{
    private readonly Dictionary<string, IAiProviderFactory> _factories;

    public AiProviderResolver(IEnumerable<IAiProviderFactory> factories)
    {
        _factories = factories.ToDictionary(f => f.ProviderType);
    }

    public AiProvider Resolve(AiCredential credential)
    {
        return _factories[credential.ProviderType].Create(credential);
    }
}
```

Using it against a list of credentials:

```csharp
var credentials = new List<AiCredential>
{
    new AiCredential { ProviderType = "openai", OpenAiApiKey = "abc" },
    new AiCredential { ProviderType = "github", GithubToken = "xyz" }
};

var resolver = new AiProviderResolver(new IAiProviderFactory[]
{
    new OpenAiProviderFactory(),
    new GithubAiProviderFactory()
});

List<AiProvider> providers = credentials.Select(resolver.Resolve).ToList();
```

**Expected behavior:** OpenAI credentials become an `OpenAiProvider`, GitHub credentials become a `GithubAiProvider` — with no `if`, no `switch`, no generics, and no casting anywhere in this flow. A new provider type means adding one new `AiProvider` subclass and one new `IAiProviderFactory` implementation; nothing existing changes.

## Example: Func-Based Resolver — A Leaner Variant of the Same Resolver

The interface-per-factory design above is explicit and DI-friendly, but it's also more ceremony than every codebase wants. A resolver can store `Func<AiCredential, AiProvider>` delegates instead of factory instances, keeping the same dictionary-lookup shape with less boilerplate:

```csharp
class AiProviderResolver
{
    private readonly Dictionary<string, Func<AiCredential, AiProvider>> _creators;

    public AiProviderResolver(IEnumerable<IAiProviderFactory> factories)
    {
        _creators = factories.ToDictionary(
            f => f.ProviderType,
            f => (Func<AiCredential, AiProvider>)f.Create
        );
    }

    public AiProvider Resolve(AiCredential credential)
    {
        if (!_creators.TryGetValue(credential.ProviderType, out var create))
            throw new InvalidOperationException("Unsupported provider");

        return create(credential);
    }
}
```

This still wires up cleanly through DI — registering providers in DI tells the resolver what it can inject into its own constructor list:

```csharp
services.AddSingleton<AiManager>();
services.AddSingleton<AiProviderResolver>();
services.AddSingleton<IAiProviderFactory, OpenAiProviderFactory>();
services.AddSingleton<IAiProviderFactory, GithubAiProviderFactory>();
```

And it can be wrapped in a small manager for bulk or single resolution:

```csharp
class AiManager
{
    private readonly AiProviderResolver _resolver;

    public AiManager(AiProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public List<AiProvider> BuildProviders(IEnumerable<AiCredential> creds)
        => creds.Select(_resolver.Resolve).ToList();

    public AiProvider BuildProvider(AiCredential cred)
        => _resolver.Resolve(cred);
}
```

**Taken to its leanest form**, the factory classes can be dropped entirely in favor of inline lambdas, if there's truly nothing else those factory classes would ever need to do:

```csharp
var resolver = new Dictionary<string, Func<AiCredential, AiProvider>>
{
    ["openai"] = c => new OpenAiProvider { ApiKey = c.OpenAiApiKey },
    ["github"] = c => new GithubAiProvider { Token = c.GithubToken }
};

if (!resolver.TryGetValue(cred.ProviderType, out var factory))
{
    throw new InvalidOperationException($"Unsupported AI provider: {cred.ProviderType}");
}

var provider = factory(cred);
```

**Choosing between the two resolver styles:** the interface-per-factory version is more discoverable (each factory is a named, testable class you can find by type), more consistent with how the rest of a DI-heavy codebase is likely structured, and easier to extend if a factory ever needs its own dependencies injected. The `Func`-based version is faster to write and reads more like configuration than code — a reasonable choice for a small, stable set of providers where each factory really is "one line of object construction" and nothing more. If a factory is ever likely to grow its own logic or dependencies, start with the interface version; it's a larger refactor to grow *into* interfaces later than to have started with them.

## Tradeoffs & Common Mistakes

- **Generic class switching on `T`.** `class AiService<T> { ... switch (typeof(T)) ... }` is the clearest sign this case applies — if a generic parameter needs a runtime type check to do anything useful, the generic isn't earning its complexity.
- **Factory with a `switch`.** Moves the problem out of the generic class but doesn't solve it — every new type still means editing existing code, which is the Open/Closed violation this whole case exists to avoid.
- **An `AiService<T>`-style wrapper that gains nothing from `T`.** If the generic parameter is only ever resolved at runtime (never written explicitly as `AiService<OpenAiProvider>` in actual calling code), it's not doing any compile-time work — drop it for the plain base-type version.
- **Reaching for polymorphism when the real variation is in data shape, not behavior.** DTO inheritance used purely to describe different JSON shapes, or a base class used only to "carry" data rather than expose behavior, is a sign the problem calls for plain DTOs, not an abstract base class.
- **Returning a base type and then casting back down to check the concrete type.** "Trying to get the concrete type back" after resolving to the base/interface type defeats the entire purpose of depending on the contract — if calling code needs to know the concrete type, the design has leaked past polymorphism's boundary.

## Alternatives

**When generics genuinely would make sense here:** only if the concrete type is chosen explicitly at the call site, at compile time — `AiService<OpenAiProvider>`, `AiService<GithubAiProvider>` — written directly in code, not resolved from a list or config at runtime. That is a different scenario from everything above, and it's the one case where reaching for `T` is the right instinct rather than the wrong one.

**The final checklist this case reduces to:**

- ❌ Generic class switching on `T`
- ❌ Factory with a `switch`
- ✅ Abstract base class with a virtual/abstract `Initialize`
- ✅ Concrete types own their own properties
- ✅ One factory per concrete type
- ✅ Runtime selection via a registry/resolver

See [Inheritance vs Composition](/topics/oop-design/inheritance-vs-composition) for the related question of when a shared base class is the right call at all, and the [Programming Vocabulary Cheat Sheet](/reference/vocabulary-cheat-sheet) for the "Factory / Resolver shape" this pattern is an instance of.
