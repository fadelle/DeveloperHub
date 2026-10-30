---
title: "Pulsar Consumer: CancellationToken Layers and Graceful Shutdown"
slug: pulsar-cancellation-shutdown
summary: A real consumer has three different cancellation tokens in flight at once, each with a different responsibility — tracing exactly how a shutdown signal propagates through all three is what makes graceful shutdown actually graceful.
order: 6
sources: [Pulsar-Cancellation-Token-and-Shutdown-Flow.docx]
related: [concurrency-tool-comparison, pub-sub-messaging]
---

## Problem

A production Pulsar consumer built around `AddDotPulsarLibrary()`, a `SubscriberEngine`, and a bounded-channel worker pool doesn't have one cancellation token — it has three, each owned by a different layer, each triggered at a different moment. Treat them as interchangeable and shutdown either hangs (the host waits indefinitely for work that will never observe cancellation) or is too abrupt (a message that's mid-processing gets cancelled instead of finishing cleanly). Getting graceful shutdown right means tracing exactly which token cancels what, and in what order.

## When It Applies

This case applies to any hosted, long-running consumer — not just Pulsar — where processing has to survive `IIS` recycling, a Kubernetes `SIGTERM`, a Windows service stop, or `Ctrl+C`, without losing in-flight work or hanging the process past its shutdown budget. It's the natural next step after [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison) — once you have workers pulling from a bounded `Channel<T>`, this is exactly how those workers get told to stop, finish what they're doing, and exit.

## Solution

Three different cancellation concepts exist in this flow, each with a distinct responsibility:

1. **The host shutdown token** — represents the time budget .NET gives the whole application to shut down.
2. **The subscriber engine's stopping token** — tells every active subscription "stop receiving new work."
3. **The channel worker's processing token** — protects a message that's *already being processed* from being cancelled immediately.

### 1. Application startup

The service registers the library and a shutdown budget:

```csharp
builder.Services.AddDotPulsarLibrary();
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromMinutes(3);
});
```

`AddDotPulsarLibrary()` registers `SubscriberEngine`, `TenantsSubscriberEngine`, and `PulsarLifecycleService` — the lifecycle service is registered as `services.AddHostedService<PulsarLifecycleService>()`. The three-minute setting does nothing during normal execution; it only defines the maximum time the host will wait during shutdown.

### 2. Subscriptions are created

The subscriber engine owns its own cancellation source:

```csharp
private readonly CancellationTokenSource _stopping = new();
```

When it creates a subscription, it passes this token and saves the returned task:

```csharp
pulsarHandler.Subscribe(_stopping.Token);
_subscriptionTasks.Add(subscriptionTask);
```

```
SubscriberEngine
    │
    ├── owns _stopping CancellationTokenSource
    ├── passes _stopping.Token to subscription 1
    ├── passes _stopping.Token to subscription 2
    ├── passes _stopping.Token to subscription 3
    │
    └── stores every subscription Task
```

A `CancellationToken` does not cancel itself — only its owning source can trigger it: `await _stopping.CancelAsync();`.

### 3. The token enters the channel-worker handler

The handler receives the engine's token and creates a *linked* token for receiving:

```csharp
public async Task Subscribe(CancellationToken cancellationToken)
{
    using var pumpCancellation =
        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
}
```

```
SubscriberEngine _stopping.Token cancelled
    ↓
pumpCancellation.Token also cancelled
```

That linked token drives the Pulsar message stream and the channel writes:

```csharp
await foreach (IMessage<byte[]> message in consumer.Messages(pumpCancellation.Token))
{
    await writer.WriteAsync(message, pumpCancellation.Token);
}
```

It's also what workers wait on for the next channel message: `await reader.WaitToReadAsync(stoppingToken);`. So the same stopping signal reaches DotPulsar's receive stream, the channel writes, and workers waiting for messages — all from one `CancelAsync()` call upstream.

## Example: The Separate Processing Token — Why a Message Mid-Flight Isn't Cut Off

The channel-worker handler also creates a second, independent token, specifically for the message a worker is *currently* processing:

```csharp
using var processingCancellation = new CancellationTokenSource();
```

```csharp
await ProcessMessageAsync(message, processingToken);
```

This separation is intentional:

```
Stopping token    → stop receiving messages, stop workers taking new messages
Processing token  → allow the current message to finish, still controlled by MessageProcessingTimeout
```

**Without this separation, application shutdown would immediately cancel the message currently being processed** — turning a graceful shutdown into a data-loss or redelivery event for whatever happened to be in flight at that moment. With it, a worker that's idle stops immediately, while a worker mid-message finishes within its `MessageProcessingTimeout`, acknowledges if successful, and only then stops.

## Example: Walking the Full Shutdown Sequence

Shutdown can be triggered by IIS stopping or recycling, Kubernetes sending `SIGTERM`, a Windows service shutdown, `Ctrl+C`, `IHostApplicationLifetime.StopApplication()`, or normal host disposal. From there:

**1. The host creates its shutdown token**, representing the configured three-minute budget:

```csharp
using var hostShutdown = new CancellationTokenSource(TimeSpan.FromMinutes(3));
```

That token is passed to `PulsarLifecycleService.StoppingAsync(hostShutdown.Token)`. This host token is distinct from the subscriber engine's `_stopping.Token` — the host token limits how long .NET *waits*; it doesn't itself tell Pulsar subscriptions to stop.

**2. The lifecycle service starts Pulsar shutdown:**

```csharp
await Task.WhenAll(
    _subscriberEngine.StopAsync(),
    _tenantsSubscriberEngine.StopAsync());
```

Each subscriber engine then executes `await _stopping.CancelAsync(); await Task.WhenAll(_subscriptionTasks);` — this is the moment the subscription token actually becomes cancelled.

**3. The cancellation reaches the message pump.** `consumer.Messages(pumpCancellation.Token)` stops waiting for the next message and throws an expected `OperationCanceledException`, which the pump catches:

```csharp
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    writer.TryComplete();
}
```

The channel writer is completed — no more messages will be written.

**4. Channel workers stop taking new messages.** Workers waiting at `await reader.WaitToReadAsync(stoppingToken)` are cancelled and stop; a worker already processing a message uses the separate processing token, so it finishes within `MessageProcessingTimeout`, acknowledges if successful, and only then stops. Messages still sitting in the bounded channel were received but never acknowledged, and remain unacknowledged.

**5. Consumer disposal releases unacknowledged work for redelivery:**

```csharp
finally
{
    await pumpCancellation.CancelAsync();
    await ObservePumpCompletionSafely(...);
    await DisposeSafelyAsync(consumer, ConsumerConfig.DisposeTimeout);
}
```

Closing the consumer lets Pulsar redeliver the unacknowledged messages to another consumer, or to this service after a restart.

**6. Subscription tasks finish**, and the engine's `await Task.WhenAll(_subscriptionTasks)` completes:

```
All consumers disposed
    ↓
SubscriberEngine.StopAsync completes
    ↓
PulsarLifecycleService.StoppingAsync completes
    ↓
.NET continues the remaining host shutdown
    ↓
DI container is disposed
```

**Expected behavior if the three-minute budget expires first:** the lifecycle service is waiting via `shutdownTask.WaitAsync(hostCancellationToken)`. If Pulsar shutdown finishes before three minutes, the host continues immediately. If the budget expires first, the host stops waiting and proceeds toward DI disposal and process termination regardless of whether the internal Pulsar shutdown task has finished — which is exactly why Kubernetes, IIS, or the operating system must be configured to allow *at least* the same shutdown duration, or the process gets killed mid-shutdown anyway.

## Tradeoffs & Common Mistakes

- **Using one token for everything.** Collapsing the host token, the engine's stopping token, and the processing token into a single `CancellationToken` is the single most common way to accidentally cancel an in-flight message's processing the instant shutdown begins.
- **Forgetting the shutdown budget has to match the infrastructure.** Setting `ShutdownTimeout` to three minutes does nothing if Kubernetes' `terminationGracePeriodSeconds` (or IIS's equivalent) is shorter — the orchestrator will kill the process before the host's own budget expires.
- **Not distinguishing "stop receiving" from "finish what you started."** If workers check only one shared token, there's no way to express "accept no new work, but let the current message finish" — which is the entire point of the processing-token split.
- **Assuming unacknowledged messages are lost.** They aren't — closing the consumer releases them back to Pulsar for redelivery to another consumer or the restarted service. The real risk is *double processing* on redelivery, not data loss, and that needs its own idempotency handling.

## Alternatives

- **A simpler service with no mid-flight protection need** (fast, idempotent, sub-second operations) may not need a separate processing token at all — a single stopping token is simpler and sufficient when there's nothing meaningfully "in flight" to protect.
- **Services with strict ordering or exactly-once requirements** may need to track full `MessageId` and redelivery count on top of this shutdown flow, since graceful shutdown alone doesn't prevent redelivery after a crash (only after a *clean* shutdown) — see the ordering notes in [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison) for the related point about multi-worker ordering.
