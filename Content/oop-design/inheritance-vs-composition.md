---
title: Inheritance vs Composition
slug: inheritance-vs-composition
summary: Inheritance models "is-a," composition models "has-a" — and in .NET, composition wins more often than the textbook examples suggest, because dependency injection is built around it.
order: 1
sources: [DotNet_Inheritance_vs_Composition_CSharp.docx]
related: [polymorphism-vs-generics, extending-a-class-you-cant-modify]
---

## Problem

This is one of the oldest design questions in object-oriented programming, and in .NET it has a sharper answer than most languages give it: **inheritance models an "is-a" relationship; composition models a "has-a" relationship.** The common failure mode isn't choosing the wrong one in obvious cases — it's reaching for inheritance because it's the first tool taught, in situations that are actually "has-a" or "uses-a," and ending up with a class hierarchy that doesn't reflect how the domain actually works.

## When It Applies

Ask this question every time a new class seems like it should "extend" an existing one. In .NET specifically, this decision is sharpened by dependency injection, interfaces, and the built-in DI container — composition is especially common here precisely because the framework is built around injecting dependencies rather than subclassing them. Reach for this case before modeling any relationship between two types, and especially before using inheritance purely to reuse a chunk of behavior.

## Solution

**Inheritance** means one class derives from another — use it when there is a true "is-a" relationship:

```csharp
public class Animal
{
    public virtual void Speak()
    {
        Console.WriteLine("Some sound");
    }
}

public class Dog : Animal
{
    public override void Speak()
    {
        Console.WriteLine("Woof");
    }
}
```

```csharp
Animal animal = new Dog();
animal.Speak(); // Woof
```

Examples of genuine "is-a" relationships: `Animal` → `Dog`, `Employee` → `Manager`.

**Composition** means one class contains another — use it for a "has-a" relationship:

```csharp
public class Engine
{
    public void Start()
    {
        Console.WriteLine("Engine started");
    }
}

public class Car
{
    private readonly Engine _engine;

    public Car(Engine engine)
    {
        _engine = engine;
    }

    public void Drive()
    {
        _engine.Start();
        Console.WriteLine("Driving...");
    }
}
```

```csharp
var engine = new Engine();
var car = new Car(engine);
car.Drive();
```

A `Car` **has an** `Engine` — it doesn't make sense to say a car "is an" engine.

## Example: Why Composition Is Preferred in .NET — a Misused Inheritance, Corrected

Here's the incorrect use of inheritance that shows up often in practice:

```csharp
public class EmailNotifier
{
    public virtual void Send(string message)
    {
        Console.WriteLine($"Email: {message}");
    }
}

public class SmsNotifier : EmailNotifier
{
    public override void Send(string message)
    {
        Console.WriteLine($"SMS: {message}");
    }
}
```

An SMS notifier is not an `EmailNotifier` — the inheritance relationship is simply wrong, even though the code compiles and runs. The correct approach uses an interface plus composition:

```csharp
public interface INotifier
{
    void Send(string message);
}

public class EmailNotifier : INotifier
{
    public void Send(string message)
    {
        Console.WriteLine($"Email: {message}");
    }
}

public class SmsNotifier : INotifier
{
    public void Send(string message)
    {
        Console.WriteLine($"SMS: {message}");
    }
}
```

Now a service can depend on the interface instead of a concrete email implementation:

```csharp
public class OrderService
{
    private readonly INotifier _notifier;

    public OrderService(INotifier notifier)
    {
        _notifier = notifier;
    }

    public void PlaceOrder()
    {
        _notifier.Send("Order placed.");
    }
}
```

Switching implementations needs no change to `OrderService` at all:

```csharp
var service = new OrderService(new EmailNotifier());

// Later
service = new OrderService(new SmsNotifier());
```

**Expected behavior:** `OrderService` never needs to know or care which `INotifier` it was given — the Email vs. SMS decision lives entirely outside it, which is the whole point of preferring composition here.

## Example: Composition and the Built-in DI Container

Composition works naturally with .NET's built-in DI container — this is a large part of why composition dominates in ASP.NET Core applications specifically:

```csharp
builder.Services.AddScoped<INotifier, EmailNotifier>();
builder.Services.AddScoped<OrderService>();

public class OrderService
{
    private readonly INotifier _notifier;

    public OrderService(INotifier notifier)
    {
        _notifier = notifier;
    }
}
```

The DI container creates and injects whichever implementation was registered — `OrderService` never constructs an `INotifier` itself, it only declares that it needs one.

## Example: Combining Both — Inheritance for the Family, Composition for the Usage

Most real .NET applications use both, and that's by design, not a compromise:

```csharp
public abstract class PaymentProcessor
{
    public abstract void Process(decimal amount);
}

public class CreditCardProcessor : PaymentProcessor
{
    public override void Process(decimal amount)
    {
        Console.WriteLine($"Paid {amount} by card.");
    }
}
```

That inheritance hierarchy — a genuine "is-a" family of payment processors — gets *composed* into another class:

```csharp
public class CheckoutService
{
    private readonly PaymentProcessor _processor;

    public CheckoutService(PaymentProcessor processor)
    {
        _processor = processor;
    }

    public void Checkout(decimal amount)
    {
        _processor.Process(amount);
    }
}
```

**Inheritance defines a family of payment processors; composition lets `CheckoutService` work with any processor in that family** without caring which concrete type it received.

## Tradeoffs & Common Mistakes

- **Using inheritance to reuse behavior between types that aren't really the same thing.** `SmsNotifier : EmailNotifier` compiles fine and "works," but it locks every future notifier into inheriting from whichever one happened to be written first — and it's semantically false, which tends to surface as confusion later, not a compiler error now.
- **Forgetting that inheritance is still the right tool sometimes.** .NET itself leans on inheritance constantly for framework base classes — `ControllerBase`, `BackgroundService` — because those really are stable "is-a" hierarchies you're meant to extend.
- **Reaching for composition + interface and then never actually swapping implementations.** If a type will only ever have one implementation forever, an interface adds indirection without a corresponding benefit — the payoff of composition is the ability to substitute, test, and extend without changing the depending class.

## Alternatives

| Question | Use |
|---|---|
| Is it an "is-a" relationship? | Inheritance |
| Is it a "has-a" or "uses-a" relationship? | Composition |
| Need interchangeable implementations? | Composition + Interface |
| Using Dependency Injection? | Composition + Interface |
| Modeling a stable hierarchy? | Inheritance |

**ASP.NET Core rule of thumb:** inheritance is commonly used for framework base classes such as `ControllerBase` and `BackgroundService`. Interfaces and composition are preferred for business logic, repositories, services, logging, payment gateways, email providers, and other application components — because they make applications easier to test, extend, and maintain. When runtime variation is involved rather than a stable hierarchy, see [Polymorphism vs Generics](/topics/oop-design/polymorphism-vs-generics) for the next layer of this decision.
