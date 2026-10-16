---
title: "Test-Driven Development: Red, Green, Refactor"
slug: tdd
summary: Writing the test before the code it tests isn't about testing more — it's a design discipline that forces every piece of functionality to start from a concrete definition of "done."
order: 1
sources: [TDD.docx]
related: [mocking-with-moq]
---

## Problem

Writing tests after the implementation is finished tends to produce tests that describe what the code *does*, not what it was actually *supposed* to do — it's easy to unconsciously write a test that confirms the implementation rather than one that would have caught a wrong implementation. Test-Driven Development inverts the order specifically to avoid this: the test is written first, while the only thing that exists is the requirement, not yet any implementation to be influenced by.

## When It Applies

TDD fits best for well-understood, testable units of behavior — a method, a class, a small piece of business logic — where "what should this do" can be stated concretely before writing any code. It's a discipline to reach for deliberately, not a universal replacement for all testing; exploratory spikes, UI-heavy work, or genuinely unclear requirements often don't fit the red-green-refactor rhythm well, and forcing it there usually just slows down the exploration without the usual design benefit.

## Solution

The cycle has three steps, repeated for each small piece of functionality:

1. **Write a test** for a specific behavior you want to implement, in a testing framework like NUnit or xUnit. The test should fail initially — if it passes before any implementation exists, it isn't actually testing anything new.
2. **Run the test and watch it fail.** This confirms the test is actually exercising something that doesn't exist yet, not silently passing for the wrong reason.
3. **Write the minimum code needed to make the test pass** — no more. This keeps the implementation scoped exactly to what's been specified so far, rather than anticipating future requirements that may never arrive.
4. **Run all tests**, not just the new one, to make sure the change didn't break anything that was already passing.
5. **Refactor** once everything is green — clean up design, naming, and structure now that there's a passing test acting as a safety net for the change.
6. **Repeat** for the next piece of behavior.

```csharp
using NUnit.Framework;

[TestFixture]
public class MyTestClass
{
    [Test]
    public void MyTestMethod()
    {
        // Arrange
        var myObject = new MyClass();

        // Act
        var result = myObject.MyMethod();

        // Assert
        Assert.AreEqual(10, result);
    }
}

public class MyClass
{
    public int MyMethod()
    {
        return 10;
    }
}
```

**Expected behavior:** `Assert.AreEqual(10, result)` is written *before* `MyClass.MyMethod()` exists in a form that would satisfy it — the test is written against the intended behavior, then just enough code (`return 10;`) is added to make it pass. In a real scenario `MyMethod()` would contain actual logic rather than a hardcoded literal, but the discipline is the same regardless of complexity: the test comes first, and the implementation is written to satisfy it, not the other way around.

## Tradeoffs & Common Mistakes

- **Skipping the "watch it fail" step.** A test that's never been seen to fail might be passing for an unrelated reason — a typo that makes the assertion trivially true, a test that isn't actually wired up to run. Seeing red before green is what confirms the test is doing real work.
- **Writing more code than the current test requires.** TDD's discipline is specifically "the minimum to pass" — implementing ahead of the tests reintroduces the exact problem TDD exists to avoid: code shaped by assumptions rather than by a concrete, verified requirement.
- **Treating TDD as a substitute for integration or end-to-end testing.** TDD is naturally suited to unit-level behavior; it doesn't replace tests that verify components actually work together, or that real infrastructure behaves as expected — see [Unit Testing with Mocks](/topics/testing-quality/mocking-with-moq) for how to keep unit tests isolated from infrastructure, which is a related but separate concern.
- **Forcing the cycle onto genuinely unclear requirements.** If "what should this do" can't yet be stated concretely, writing the test first just moves the uncertainty into the test instead of resolving it — some exploration or a spike often needs to happen before TDD's rhythm is productive.

## Alternatives

- **Requirements that are genuinely exploratory or UI-heavy**, where "the right behavior" is discovered by building and looking, not specified up front — test-after (writing tests once the shape of the solution is clearer) can fit better than test-first for this kind of work.
- **Need the tests to isolate a unit from its real dependencies** (a database, an external service) so TDD's fast red-green-refactor loop doesn't get slowed down by infrastructure? See [Unit Testing with Mocks](/topics/testing-quality/mocking-with-moq) for mocking dependencies with Moq — TDD and mocking are frequently used together, but they answer different questions (when to write the test vs. what the test depends on).
