---
title: "Why C# Compiles to CIL Instead of Machine Code"
slug: cil-and-jit
summary: C# doesn't compile straight to machine code — it compiles to an intermediate language that the runtime then JIT-compiles, and that extra step is what buys .NET most of its cross-platform and tooling story.
order: 5
sources: [CIL intermediate language.docx]
related: [boxing-unboxing]
---

## Problem

C# source code doesn't compile directly to machine code the way a language like C does — it compiles to Common Intermediate Language (CIL, also called MSIL), which is only translated into actual machine instructions later, at run time. That extra step can look like pure overhead from the outside. Understanding what it actually buys — and when it costs something real — is what separates "why does .NET have a JIT step" from a real grasp of the tradeoff.

## When It Applies

This is background worth having whenever a decision touches startup time, cross-platform deployment, or anything that depends on reflection or dynamic code generation — AOT (ahead-of-time) compilation decisions, understanding why a .NET assembly can run unmodified on different operating systems, or reasoning about why the first call to a method is sometimes slower than later calls.

## Solution

Compiling to CIL instead of directly to machine code buys several things at once, all stemming from the same idea: CIL is a platform-independent, language-agnostic intermediate representation, only turned into real machine code by the Common Language Runtime (CLR) on the machine that actually runs it.

- **Platform independence.** CIL is not tied to any particular CPU architecture or operating system — the same compiled assembly can run on any platform with a compatible CLR, without recompilation.
- **Language-agnosticism.** CIL isn't specific to C# — C#, VB.NET, F#, and other .NET languages all compile to the same intermediate representation, which is what lets them interoperate seamlessly within one CLR process: a C# project can consume an F# library with no special bridging.
- **Just-in-time (JIT) compilation.** When a .NET program runs, the CLR compiles CIL into machine code specific to the actual hardware and OS it's running on — at run time, not ahead of time. This lets the JIT optimize based on the real execution environment rather than a guess made at build time.
- **Security and memory management.** The CLR verifies CIL for type safety before executing it, and provides automatic memory management (garbage collection) on top — both are easier to enforce uniformly on a controlled intermediate representation than on arbitrary native machine code.
- **Reflection and metadata.** CIL assemblies carry rich metadata describing their own structure, which is what makes reflection possible — inspecting and manipulating code at run time, dynamically loading assemblies, and generating code dynamically.

## Tradeoffs & Common Mistakes

- **Assuming JIT compilation is a one-time, negligible cost.** It isn't free — a method is JIT-compiled the first time it's actually called, which is part of why a cold start (the very first calls into a .NET application) is typically slower than steady-state execution, once everything that's going to run has already been compiled.
- **Confusing JIT with interpretation.** CIL isn't interpreted line-by-line at run time the way some scripting languages are — it's compiled to real machine code by the JIT, then executed natively from then on. The overhead is paid once per method (per process run), not on every execution of that method.
- **Forgetting that type-safety verification has a cost too**, usually negligible, but part of what the CLR does on top of JIT compilation — the uniform verification this buys (every CIL assembly gets the same safety guarantees from the same runtime) is a tradeoff against raw speed, not a free benefit.

## Alternatives

- **Startup time is a genuine constraint** (a CLI tool, a serverless cold start, a container that needs to be ready immediately)? .NET's Ahead-of-Time (AOT) compilation path compiles straight to native code at build time instead of relying on the JIT at run time — trading some of CIL's platform-independence and dynamic-code flexibility for faster startup and a smaller runtime footprint.
- **Need to understand a specific performance characteristic tied to boxing specifically**, rather than the compilation model in general? See [Boxing and Unboxing](/topics/performance-resource-management/boxing-unboxing) — a related but distinct runtime cost, about how value types are represented on the heap rather than about compilation strategy.
