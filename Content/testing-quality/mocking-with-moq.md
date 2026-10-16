---
title: "Unit Testing with Mocks: Isolating Dependencies with Moq"
slug: mocking-with-moq
summary: A unit test for a class with a database or service dependency shouldn't need a real database or service — Moq lets you substitute a fake implementation whose behavior you control completely.
order: 2
sources: [Mock database get.docx, Mock Example.docx]
related: [tdd]
---

## Problem

A class depends on an interface — `IDatabase`, `ICalculatorService`, anything injected rather than constructed directly — and testing it against the *real* implementation means the test now depends on a real database connection, a real network call, or whatever else that implementation does. That makes the test slow, flaky, and coupled to infrastructure that has nothing to do with the logic actually being tested. The fix is to substitute a fake implementation of the dependency — one that returns exactly what the test needs, with no real side effects.

## When It Applies

This applies to any unit test for a class that takes a dependency through its constructor (or a property) as an interface — which, if [Inheritance vs Composition](/topics/oop-design/inheritance-vs-composition) was followed, is most business logic in a well-structured .NET codebase. It's specifically for *unit* tests, where the goal is isolating one class's logic — not for integration tests, where exercising the real dependency is the point.

## Solution

Moq creates a fake implementation of an interface at test time, with each method's behavior configured explicitly via `Setup`. The class under test receives the mock instead of a real implementation — it has no way to tell the difference, since it only ever depends on the interface.

```csharp
public interface IDatabase
{
    string Get(string key);
}

public class DataService
{
    private readonly IDatabase _database;

    public DataService(IDatabase database)
    {
        _database = database;
    }

    public string GetData(string key)
    {
        return _database.Get(key);
    }
}
```

```csharp
using Moq;
using Xunit;

public class DataServiceTests
{
    [Fact]
    public void GetData_WhenKeyExists_ReturnsValue()
    {
        // Arrange
        string expectedValue = "mocked value";
        string key = "testKey";
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase.Setup(d => d.Get(key)).Returns(expectedValue);
        var dataService = new DataService(mockDatabase.Object);

        // Act
        string result = dataService.GetData(key);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetData_WhenKeyDoesNotExist_ReturnsNull()
    {
        // Arrange
        string key = "nonExistentKey";
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase.Setup(d => d.Get(key)).Returns((string)null);
        var dataService = new DataService(mockDatabase.Object);

        // Act
        string result = dataService.GetData(key);

        // Assert
        Assert.Null(result);
    }
}
```

**Expected behavior:** neither test touches a real database — `mockDatabase.Setup(d => d.Get(key)).Returns(expectedValue)` tells Moq exactly what `Get("testKey")` should return when called, and `mockDatabase.Object` is the fake `IDatabase` instance actually passed into `DataService`. The two tests can run in any order, in parallel, with no shared state and no network or disk I/O — the entire behavior of the dependency is defined inline, in the test itself.

## Example: Verifying a Mock Was Called, Not Just Its Return Value

Beyond configuring what a mock returns, Moq can assert that a method was actually *called*, and with what arguments — useful when the thing being tested is that a dependency was invoked correctly, not just what came back from it.

```csharp
public class Calculator
{
    private readonly ICalculatorService calculatorService;

    public Calculator(ICalculatorService calculatorService)
    {
        this.calculatorService = calculatorService;
    }

    public int Add(int a, int b)
    {
        return calculatorService.Add(a, b);
    }
}
```

```csharp
using Moq;
using Xunit;

public class CalculatorTests
{
    [Fact]
    public void Add_Should_Return_Sum_Of_Two_Numbers()
    {
        // Arrange
        Mock<ICalculatorService> calculatorServiceMock = new Mock<ICalculatorService>();
        calculatorServiceMock.Setup(c => c.Add(2, 3)).Returns(5);
        Calculator calculator = new Calculator(calculatorServiceMock.Object);

        // Act
        int result = calculator.Add(2, 3);

        // Assert
        Assert.Equal(5, result);
        calculatorServiceMock.Verify(c => c.Add(2, 3), Times.Once);
    }
}
```

**Expected behavior:** `Assert.Equal(5, result)` confirms `Calculator.Add` returned what the mocked `ICalculatorService.Add(2, 3)` was set up to return. `calculatorServiceMock.Verify(c => c.Add(2, 3), Times.Once)` goes further — it confirms `Add(2, 3)` was actually called on the mock exactly once, which `Assert.Equal` alone wouldn't catch if `Calculator.Add` had, say, returned a hardcoded `5` without calling the dependency at all. `Verify` is what distinguishes "the output was right" from "the output was right *because* the dependency was used correctly."

## Tradeoffs & Common Mistakes

- **Mocking a type the class under test doesn't actually depend on through an interface.** Moq mocks interfaces (and virtual members of classes) — a dependency that's a concrete, non-virtual class constructed directly inside the class under test can't be swapped out this way, which is itself a sign the class needs to depend on an interface instead, per [Inheritance vs Composition](/topics/oop-design/inheritance-vs-composition).
- **Over-specifying a mock's setup.** Setting up every possible call a dependency could receive, including ones unrelated to the behavior being tested, makes tests brittle — they start failing when unrelated code changes how it calls the dependency, even if the actual behavior under test is unaffected.
- **Using `Verify` everywhere, including when it isn't the point of the test.** `Verify` is for asserting *interaction* happened correctly — reach for it when the test is specifically about whether and how a dependency was called, not as a reflexive addition to every mock-based test.
- **Mocking so much that the test stops testing real logic.** If every dependency a class touches is mocked, including trivial, pure helper logic, the test risks only confirming that the mocks were wired up correctly — not that the class's own logic behaves correctly.

## Alternatives

- **Need to verify the class actually integrates correctly with a real database or service**, not just that it calls an interface correctly? That's the job of an integration test, not a unit test — mocks are specifically for isolating a unit from its dependencies, not for validating those dependencies themselves.
- **The dependency is simple enough that a hand-written fake class is clearer than a configured mock?** A small manually-written stub implementing the interface is a reasonable alternative to Moq for very simple cases — Moq earns its keep once setup, verification, or varying return values per test start adding real value over a hand-written fake.
