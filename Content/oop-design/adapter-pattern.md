---
title: "Adapter Pattern: Bridging an Incompatible Interface"
slug: adapter-pattern
summary: Two things need to work together but were never designed to — the Adapter pattern translates one interface into another, so the code that depends on the interface never has to know a translation is happening.
order: 3
sources: [Adapter example.docx]
related: [extending-a-class-you-cant-modify, polymorphism-vs-generics, func-as-behavior-parameter]
---

## Problem

A codebase depends on one interface — a payment gateway contract, say — and a new integration needs to plug in, but its API shape is different: different method names, different parameter types, no shared ancestor. Changing the existing interface to accommodate the new one would ripple through every class that already depends on it; changing the new integration isn't an option if it's a third-party library. Something has to translate between the two shapes without either side knowing the translation exists.

## When It Applies

Reach for Adapter specifically when the mismatch is in *shape* — method names, parameter types, calling conventions — not in *behavior*. If two things genuinely do the same job but are described differently, Adapter is the fit. (If instead the question is "I need to add behavior to something I can't modify," that's a related but different problem — see [Extending a Class You Can't Modify](/topics/oop-design/extending-a-class-you-cant-modify) for the decorator and extension-method versions of that question.)

## Solution

Define the adapter as an implementation of the interface the *rest of the codebase already depends on*, and have it hold the incompatible type internally, translating each call:

```csharp
// The interface the rest of the codebase already depends on
interface IPaymentGatewayA
{
    void Connect();
    void ProcessPayment(decimal amount);
}

// A third-party gateway with an incompatible shape
interface IPaymentGatewayC
{
    void EstablishConnection();
    void SubmitPaymentRequest(double amount);
}

// The adapter: implements A, wraps C, translates between them
class PaymentGatewayCAdapter : IPaymentGatewayA
{
    private IPaymentGatewayC paymentGatewayC;

    public PaymentGatewayCAdapter(IPaymentGatewayC gatewayC)
    {
        paymentGatewayC = gatewayC;
    }

    public void Connect()
    {
        paymentGatewayC.EstablishConnection();
    }

    public void ProcessPayment(decimal amount)
    {
        double convertedAmount = Convert.ToDouble(amount);
        paymentGatewayC.SubmitPaymentRequest(convertedAmount);
    }
}
```

```csharp
IPaymentGatewayC gatewayC = new PaymentGatewayC();
IPaymentGatewayA gatewayCAdapter = new PaymentGatewayCAdapter(gatewayC);

gatewayCAdapter.Connect();
gatewayCAdapter.ProcessPayment(300.0m);
```

**Expected behavior:** code that only knows about `IPaymentGatewayA` — existing payment logic, tests, anything written before Gateway C existed — can use `PaymentGatewayCAdapter` exactly like any other `IPaymentGatewayA` implementation. `Connect()` calls through to `EstablishConnection()`; `ProcessPayment(decimal)` converts to `double` and calls `SubmitPaymentRequest`. Neither `IPaymentGatewayA`'s consumers nor `IPaymentGatewayC`'s own implementation need to change — all the translation lives in one small class whose only job is translation.

## Tradeoffs & Common Mistakes

- **Reaching for Adapter when the real need is new behavior, not translation.** If the goal is "add a method `Foo()` to something," not "make this fit an existing contract," Adapter is the wrong shape — see the Decorator and extension-method alternatives in [Extending a Class You Can't Modify](/topics/oop-design/extending-a-class-you-cant-modify).
- **Losing information in the translation.** Converting `decimal` to `double` (as above) can lose precision — for money specifically, this is a real risk worth flagging explicitly in review, not just an incidental type conversion. An adapter is a good place to *notice* this kind of lossy conversion, since it's the one place the two type systems meet.
- **Letting the adapter accumulate unrelated logic.** An adapter's whole value is that its job is narrow and obvious — once it starts validating, logging, or making business decisions beyond pure translation, it's no longer "an adapter," it's a service that happens to also adapt, and should probably be named and reviewed as one.

## Alternatives

- **The mismatch is about missing behavior, not an incompatible shape** — see [Extending a Class You Can't Modify](/topics/oop-design/extending-a-class-you-cant-modify) for Decorator and extension methods.
- **The variation is in which concrete implementation to use at runtime, not in bridging two different interfaces** — see [Polymorphism vs Generics](/topics/oop-design/polymorphism-vs-generics) for the factory/resolver shape that handles runtime selection between several same-shaped implementations.
- Per the [Programming Vocabulary Cheat Sheet](/reference/vocabulary-cheat-sheet)'s shape catalog, this is the canonical **Adapter shape**: `A → translate → B`.
