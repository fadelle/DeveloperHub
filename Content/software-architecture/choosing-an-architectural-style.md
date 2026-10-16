---
title: Choosing a Software Architectural Style
slug: choosing-an-architectural-style
summary: A survey of the common architectural styles applications are built from, followed by a close look at microservices — the style with the most principles to get right and the most ways to get wrong.
order: 1
sources: [Software Architectural Styles.docx, Microservices architecture.docx]
related: [pub-sub-messaging]
---

## Problem

"Architecture" covers a lot of different, sometimes overlapping styles — monolithic, client-server, service-oriented, microservices, event-driven, layered, and more — and picking one (or a combination) shapes how a system is built, deployed, and scaled for its entire lifetime. Reaching for microservices by default, because it's the style most discussed, is a particularly common mistake: it solves specific problems at a specific cost, and applying it to a system that doesn't have those problems yet mostly just adds the cost without the benefit.

## When It Applies

Reach for this case at the point an application's overall structure is being decided — not for every feature, but for the system-level question of how components are divided, deployed, and allowed to depend on each other. It's also useful when an existing system's architecture is being questioned ("should this be microservices?") and the answer needs to be grounded in which actual problem is being solved, not momentum or trend.

## Solution

**Monolithic Architecture** — the entire application built as a single, self-contained, deployed-together unit. Straightforward initially; can become complex and hard to scale as it grows, since all components are tightly coupled.

**Client-Server Architecture** — a separation of concerns between clients (web, mobile) making requests and servers handling processing, resources, and responses. Enables scalability and lets different clients interact with the same server.

**Service-Oriented Architecture (SOA)** — an application organized as a collection of self-contained, modular services communicating via standardized protocols (SOAP, REST). Promotes reusability, flexibility, and interoperability.

**Microservices Architecture** — an evolution of SOA emphasizing smaller, autonomous services, each focused on a specific business capability, independently developed, deployed, and scaled. Communication is typically lightweight (HTTP, messaging). Covered in depth below.

**Event-Driven Architecture** — components communicate by producing and consuming events; an event occurring triggers notifications consumed by interested components. Enables loose coupling and flexibility for complex workflows and integrations.

**Layered Architecture** — an application organized into horizontal layers (presentation, business logic, data access), each interacting only with adjacent layers. Promotes separation of concerns and maintainability.

**Peer-to-Peer Architecture** — direct communication between nodes without a central server; peers act as both clients and servers. Common in decentralized systems, file sharing, and distributed computing.

**Event Sourcing and CQRS** — Event Sourcing captures every state change as a sequence of events, with state rebuilt by replaying them. CQRS (Command Query Responsibility Segregation) separates read and write operations into different components, optimizing each independently.

**Hexagonal Architecture (Ports and Adapters)** — isolates business logic from external dependencies via clearly defined ports and adapters between the application core and its external components, aiding testability and flexibility.

Most real applications combine multiple styles rather than adopting exactly one — a monolith with an internal layered structure, or a system that's mostly client-server with an event-driven component for async notifications, are both normal.

## Example: Microservices Architecture in Depth

Microservices structure an application as a collection of small, loosely-coupled, independently deployable services, each responsible for a specific business capability. Each service runs as a separate process, communicating with others through lightweight mechanisms — typically APIs.

The key principles:

- **Single Responsibility** — each microservice focuses on one specific business capability, "doing one thing and doing it well."
- **Independence** — services are developed, deployed, and managed independently, letting teams work autonomously and choose technology suited to their specific service.
- **Communication via APIs** — services talk to each other through lightweight protocols, often RESTful APIs or message queues, enabling loose coupling and the flexibility to evolve services independently.
- **Decentralized Data Management** — each service owns its own dedicated database or data store, ensuring data isolation and preventing data coupling between services.
- **Fault Isolation** — failures in one service don't propagate to others, since they're isolated — improving fault tolerance and overall system resilience.
- **Scalability and Resilience** — services scale independently based on demand, allowing efficient resource utilization and better overall performance.

**Expected behavior of a well-structured microservices system:** a failure or a deployment in one service doesn't take down or require redeploying the others; a service under heavy load can be scaled up on its own without scaling the entire application; and a team can change their service's internal implementation (including its database schema or language) without needing to coordinate that change with every other team.

**What microservices costs in exchange:** additional complexity in service coordination, deployment, and monitoring. Adopting this style means deliberately taking on decisions about service discovery, load balancing, inter-service communication, and cross-service data consistency — none of which exist in a monolith, where everything shares one process and one database by default.

## Tradeoffs & Common Mistakes

- **Choosing microservices before the problems it solves actually exist.** Independent scaling, independent deployment, and team autonomy are real benefits — but only if the system actually needs them. A small application or a small team pays the full coordination and operational cost of microservices without the corresponding benefit if those problems aren't present yet.
- **Splitting services along the wrong boundaries.** Services split by technical layer (a "database service," a "validation service") instead of by business capability tend to reintroduce tight coupling through chatty inter-service calls — defeating the independence microservices is meant to provide.
- **Sharing a database across services "just this once."** This is the single fastest way to lose the fault isolation and independent deployability microservices is supposed to deliver — once two services share a schema, neither can change it without coordinating with the other, which is exactly the coupling this architecture exists to avoid.
- **Treating monolithic as inherently inferior.** A monolith is "straightforward initially" for a reason — for a simpler or smaller application, it's usually the right choice precisely because it avoids all of the coordination costs above, and it can always be decomposed later once (and if) the problems that justify it actually appear.

## Alternatives

**General guidance for choosing:** use monolithic for simpler or smaller applications; consider microservices specifically when scaling, agility, and independent team ownership are needed — not simply because microservices is the more discussed style. A layered or hexagonal structure *inside* a monolith often captures much of the separation-of-concerns benefit without the deployment and coordination overhead of splitting into separate services. Event-driven and CQRS/Event Sourcing are complementary rather than competing choices — they're commonly layered on top of either a monolith or a microservices architecture, addressing a different axis of the design (how components communicate and how state changes are tracked) rather than how the system is decomposed and deployed.
