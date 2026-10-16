---
title: "Pub/Sub: The Mechanism Behind Event-Driven Messaging"
slug: pub-sub-messaging
summary: One publisher, many subscribers, no direct knowledge of each other — Pub/Sub is the concrete mechanism most event-driven architectures are actually built from, whether or not the platform calls itself a "messaging system."
order: 2
sources: [PubSub Mechanism (Messaging System).docx]
related: [choosing-an-architectural-style, pulsar-cancellation-shutdown]
---

## Problem

Event-Driven Architecture (one of the styles in [Choosing a Software Architectural Style](/topics/software-architecture/choosing-an-architectural-style)) is a design-level idea — "components communicate by producing and consuming events." Pub/Sub is the concrete mechanism that idea is usually implemented with: a publisher pushes a message into a named channel, without knowing or caring who (if anyone) is listening, and any number of subscribers listening to that channel receive it. Getting the vocabulary and the mechanics right matters because "Pub/Sub" shows up both as the headline feature of dedicated messaging systems and as a side feature of platforms that aren't primarily about messaging at all.

## When It Applies

Reach for Pub/Sub when a publisher's work (saving to a database, completing a job, detecting a change) needs to notify an unknown or variable number of interested parties — multiple application instances, multiple independent subscribers — without the publisher needing to know who they are or how many there are. It's the natural fit for one-producer-many-consumer, broadcast-style notification; it isn't the right tool when a message needs to go to *exactly one* consumer, or when delivery needs to be guaranteed even if no subscriber is listening at the moment of publication (see Alternatives).

## Solution

The core mechanism, regardless of platform:

- **SUBSCRIBE** listens to a channel.
- **PUBLISH** pushes a message into a channel.
- Both the publisher and every subscriber must be connected to the same messaging server — a Redis instance, a RabbitMQ broker, a Kafka cluster — identified by host and port.
- Every time a publish happens, it supplies the channel name and the message; if a listener already exists on that channel, it receives the published message.

**Terminology, since the same roles get different names on different platforms:** producer = publisher = sender. Receiver = subscriber = consumer.

**Not every platform with Pub/Sub is primarily a messaging system.** RabbitMQ and Kafka are *mainly* messaging systems — moving and routing messages is their whole purpose. Redis and Angular both have Pub/Sub as *one of their features*, not their primary identity — Redis is a data store with Pub/Sub built in; Angular's Pub/Sub-like patterns (e.g. an event emitter) support in-application communication, not cross-process messaging.

## Example: Redis Pub/Sub, One Publisher and Many Subscribers

A typical shape: a web API saves something to the database, then publishes a notification so every other interested process finds out — without the API needing to know how many processes are listening, or who they are.

```
Web API (publisher)
    │
    │ 1. Save to DB
    │ 2. PUBLISH "orders.created" { orderId: 123 }
    ▼
Redis channel: "orders.created"
    │
    ├──▶ App instance A (subscriber) — SUBSCRIBE "orders.created"
    ├──▶ App instance B (subscriber) — SUBSCRIBE "orders.created"
    └──▶ App instance C (subscriber) — SUBSCRIBE "orders.created"
```

A Redis client can subscribe to any number of channels, and a single publish reaches every currently-subscribed client at once — the publishing app and every subscribing app connect to the same Redis server (for example, host `127.0.0.1`, port `6379`) to make this work.

**Expected behavior:** when the web API calls `PUBLISH "orders.created" ...`, every app instance currently subscribed to `"orders.created"` receives the message at roughly the same time — there's no queueing of the message for a subscriber that wasn't listening yet, and no coordination required between the publisher and any individual subscriber beyond both being connected to the same channel on the same server.

## Tradeoffs & Common Mistakes

- **Assuming Pub/Sub guarantees delivery.** In the model described here, a subscriber that wasn't connected and listening at the moment of publication simply never receives that message — there's no built-in replay or persistence unless the specific platform adds it on top (which moves it toward being more of a full messaging/streaming system, not plain Pub/Sub).
- **Treating "has Pub/Sub" and "is a messaging system" as the same claim.** Redis having Pub/Sub doesn't make it a message broker in the way RabbitMQ or Kafka are — reaching for Redis Pub/Sub for workloads that need durable queuing, retries, or guaranteed-once delivery is reaching for the wrong tool because the name matched, not because the guarantees did.
- **Using Pub/Sub where exactly-one-consumer delivery was actually needed.** Pub/Sub's whole model is broadcast-to-all-current-subscribers — if a message is meant to be handled by exactly one worker among several (a work queue, not a broadcast), that's a different shape entirely, closer to the bounded-channel/worker-pool pattern in [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison) than to Pub/Sub.
- **Forgetting both sides need the same server.** Publisher and subscriber(s) connecting to *different* Redis instances (easy to do by accident across environments or misconfigured connection strings) means the subscriber never sees anything published — a silent failure with no error on either side, since each side genuinely succeeded at talking to *a* server, just not the same one.

## Alternatives

- **Need guaranteed, durable delivery — including to a subscriber that wasn't listening yet?** A real message broker (RabbitMQ, Kafka, or a managed equivalent) is built for exactly this, with persistence and redelivery semantics Pub/Sub alone doesn't provide — see [Pulsar Consumer: CancellationToken Layers and Graceful Shutdown](/topics/concurrency-task-orchestration/pulsar-cancellation-shutdown) for a deep, concrete look at what durable message consumption actually involves once acknowledgment and redelivery enter the picture.
- **Need exactly one consumer to handle each message, not every subscriber?** That's a work-queue shape, not Pub/Sub — see [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison) for the bounded-channel-plus-worker-pool pattern that fits this instead.
- **The "subscribers" are all within one application process**, not separate processes or services? An in-process event (a C# `event`, or an event-aggregator/mediator pattern) gets the same one-to-many notification shape without needing an external messaging server at all.
