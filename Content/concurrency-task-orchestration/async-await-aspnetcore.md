---
title: "async/await in ASP.NET Core: Where It Actually Helps"
slug: async-await-aspnetcore
summary: async/await doesn't make a single database call or HTTP request faster — its real payoff in a web app is releasing the worker thread while that call is pending, which shows up as throughput under concurrent load, not latency on one request.
order: 2
sources: [Async_Await_in_DotNet_Guide.docx]
related: [threads-tpl-async, await-inside-loop, task-whenall-vs-semaphoreslim]
---

## Problem

`async`/`await` gets reached for reflexively in ASP.NET Core without always being clear on *why* — and the honest answer is specific: async does not necessarily make a single operation faster. Its primary benefit in web applications is better throughput and more efficient thread usage under concurrent load. Synchronous I/O ties up a worker thread for the entire duration a database, network, or file operation is pending; asynchronous I/O lets the request suspend while that operation is in progress, freeing the thread to handle other work in the meantime, with continuation code resuming on an available thread once the operation completes.

## When It Applies

This applies to any ASP.NET Core action (or similar server-side request handler) performing I/O — a database query, an HTTP call to another service, a file read or write — specifically under *concurrent load*, where the number of simultaneous requests is what actually benefits from freed-up worker threads. It does not apply to CPU-bound work, covered explicitly in Tradeoffs below, and the benefit is genuinely conditional — see the quick comparison table in Solution for exactly which scenarios qualify.

## Solution

The quick comparison table this case builds around:

| Scenario | Use async? | Why |
|---|---|---|
| Database query | Yes | Avoid blocked request threads |
| HTTP API request | Yes | Network I/O wait |
| Reading/writing files | Usually | Potential non-blocking I/O |
| Independent HTTP calls | Yes + `WhenAll` | Overlap independent waits |
| Pure arithmetic | Usually no | No I/O wait to release |
| Heavy CPU work | Different approach | Async alone doesn't reduce CPU cost |

The six examples below work through each of the "Yes" rows concretely, in ASP.NET Core terms, then the common-mistakes section covers the "no"/"different approach" rows and the ways this gets implemented incorrectly even when the scenario is right.

## Example: Blocking vs Non-Blocking Wait

The clearest minimal illustration of what async actually changes:

```csharp
// Without async — worker thread is blocked
[HttpGet("status-sync")]
public IActionResult GetStatusSync()
{
    Thread.Sleep(2000); // Simulated wait; blocks a thread
    return Ok("Ready");
}

// With async — worker thread can be released
[HttpGet("status-async")]
public async Task<IActionResult> GetStatusAsync()
{
    await Task.Delay(2000); // Simulated asynchronous wait
    return Ok("Ready");
}
```

**Expected behavior:** both endpoints take roughly 2 seconds to respond to *this one caller* — async doesn't make this single request faster. The difference is invisible from one request's perspective and only shows up under concurrent load: the synchronous version holds a worker thread hostage for the full 2 seconds per request, while the asynchronous version releases its thread back to the pool during the wait, letting that thread serve other requests in the meantime. (`Task.Delay` here is only a teaching stand-in — it's not a replacement for a real database or HTTP call, just a way to simulate "waiting" without needing either.)

## Example: Database Calls with EF Core

```csharp
// Without async
[HttpGet("products-sync")]
public IActionResult GetProductsSync()
{
    var products = _db.Products.ToList();
    return Ok(products);
}

// With async
[HttpGet("products")]
public async Task<IActionResult> GetProductsAsync(
    CancellationToken cancellationToken)
{
    var products = await _db.Products
        .ToListAsync(cancellationToken);
    return Ok(products);
}
```

**Expected behavior:** use EF Core's async methods — `ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`, `AnyAsync` — directly; they exist specifically so the request thread isn't blocked while the database executes the query. The database still has to run the query at whatever speed it runs at — async doesn't make the query itself faster — it just means the ASP.NET Core thread isn't sitting idle, blocked, while waiting for the result.

## Example: Calling Another HTTP API

```csharp
// Without async — blocking a response read
public string GetCustomerSync(HttpClient client)
{
    using var response = client.GetAsync("customers/42")
        .GetAwaiter().GetResult(); // Blocks the caller
    response.EnsureSuccessStatusCode();
    return response.Content.ReadAsStringAsync()
        .GetAwaiter().GetResult();
}

// With async — recommended
public async Task<string> GetCustomerAsync(
    HttpClient client, CancellationToken ct)
{
    using var response = await client.GetAsync("customers/42", ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync(ct);
}
```

**Expected behavior:** network waiting is one of the clearest, most common cases for async — the synchronous version calls `.GetAwaiter().GetResult()` on an inherently asynchronous API specifically to force it to block, which is almost always a sign something's wrong (this is the "sync-over-async" anti-pattern, covered again below). In real applications, get the `HttpClient` from `IHttpClientFactory` or dependency injection rather than constructing one directly, for reasons unrelated to this case (socket exhaustion, DNS staleness) but equally important to get right.

## Example: File Operations

```csharp
// Without async
public string ReadReportSync(string path)
{
    return System.IO.File.ReadAllText(path);
}

// With async
public async Task<string> ReadReportAsync(
    string path, CancellationToken ct)
{
    return await System.IO.File.ReadAllTextAsync(path, ct);
}
```

**Expected behavior:** file I/O *can* benefit from asynchronous APIs, especially in server workflows — hence "Usually" rather than an unconditional "Yes" in the comparison table above. The actual gain depends on the platform, the underlying storage, file sizes, and the specific workload; this is one to measure in a given environment rather than assume.

## Example: Sequential vs Concurrent I/O with Task.WhenAll

Async alone does not mean "running in parallel" — two awaited operations are still sequential if the second one only starts after the first finishes:

```csharp
// Sequential: about 4 seconds if each call takes 2 seconds
var users = await GetUsersAsync();
var orders = await GetOrdersAsync();
return Ok(new { users, orders });
```

```csharp
// Concurrent: about 2 seconds if independent calls overlap
Task<User[]> usersTask = GetUsersAsync();
Task<Order[]> ordersTask = GetOrdersAsync();
await Task.WhenAll(usersTask, ordersTask);
return Ok(new
{
    users = await usersTask,
    orders = await ordersTask
});
```

**Expected behavior:** `Task.WhenAll` can meaningfully reduce elapsed time specifically when the operations being awaited are genuinely independent — it does not create a dedicated thread per task; both tasks are simply started before either is awaited, so their waiting periods overlap instead of stacking. **One sharp edge specific to EF Core:** `DbContext` instances are not safe for concurrent operations — running two queries concurrently against the *same* `DbContext` is unsupported and will throw or corrupt state; genuinely concurrent EF Core queries need separate `DbContext` instances, not just separate `Task`s.

## Example: Cancellation of Long-Running I/O

```csharp
[HttpGet("inventory")]
public async Task<IActionResult> GetInventory(
    CancellationToken cancellationToken)
{
    var data = await _db.Inventory
        .ToListAsync(cancellationToken);
    return Ok(data);
}
```

**Expected behavior:** in ASP.NET Core, an action method's `CancellationToken` parameter is automatically tied to the request's own cancellation signal — passing it through to supported async APIs lets that work actually stop early when the client disconnects or cancels, rather than running to completion for no one. Cancellation here is cooperative, not forceful — passing the token doesn't *guarantee* every operation stops immediately; it only works for APIs that actually check the token.

## Tradeoffs & Common Mistakes

**What async does not solve — CPU-bound work is a different problem:**

```csharp
// This calculation is CPU-bound, not I/O waiting:
long total = CalculateLargeReport();
return Ok(total);
```

Changing this method's signature to `async` does not make `CalculateLargeReport` asynchronous — there's no I/O wait here for the thread to be released during. `Task.Run` can move CPU work onto a thread-pool worker (which may help keep a desktop UI responsive), but offloading CPU-heavy work with `Task.Run` *inside an ASP.NET Core request* generally does not improve server throughput — it just moves the same CPU cost to a different thread-pool thread, one still needed to actually do the work. For genuinely expensive CPU work, consider background processing or a queue instead of trying to make the request handler itself absorb the cost.

- **Using `async void` outside of event handlers.** Prefer `Task` or `Task<T>` so callers can actually `await` completion and observe errors — `async void` has no `Task` for either of those to happen through.
- **Calling `.Result` or `.Wait()` on a `Task`.** Blocks the calling thread and can cause deadlocks in some environments (notably ones with a synchronization context) — this is the same sync-over-async anti-pattern as `GetAwaiter().GetResult()` in the HTTP example above.
- **Assuming `await` automatically starts work concurrently.** It doesn't, by itself — start independent tasks *before* awaiting either of them (as in the `Task.WhenAll` example) if the goal is to overlap their waits.
- **Using `Task.Run` to "make" EF Core or HTTP calls asynchronous.** Use their native async APIs (`ToListAsync`, `GetAsync`, etc.) directly — wrapping a synchronous call in `Task.Run` doesn't make the underlying I/O asynchronous, it just burns a thread-pool thread blocking on it instead of the request thread.
- **Running multiple operations at the same time on the same EF Core `DbContext`.** Unsupported, covered above — a frequent source of confusing runtime exceptions when `Task.WhenAll` gets reached for without this caveat in mind.
- **Ignoring cancellation and exceptions.** Propagate the `CancellationToken` through to every async call that accepts one, and handle failures appropriately — a `Task` that's never awaited or observed can fail silently (see [Threads, the TPL, and async/await](/topics/concurrency-task-orchestration/threads-tpl-async) for exactly why).

## Alternatives

- **Need the precise semantics of what `await` does and doesn't block** — suspension points, `async void` risk, fire-and-forget vs fire-then-wait? See [Threads, the TPL, and async/await](/topics/concurrency-task-orchestration/threads-tpl-async) for the mechanics this case builds on top of.
- **Looping over many items and deciding whether to await each one in sequence, fire-and-forget, or all at once?** See [Does await Inside a Loop Block the Caller?](/topics/concurrency-task-orchestration/await-inside-loop) for that specific decision.
- **Need `Task.WhenAll` bounded to a concurrency limit, rather than two or three fixed named tasks?** See [Task.WhenAll vs SemaphoreSlim](/topics/concurrency-task-orchestration/task-whenall-vs-semaphoreslim) and [Choosing a Concurrency Tool](/topics/concurrency-task-orchestration/concurrency-tool-comparison) for controlling how many concurrent operations run when the count isn't fixed at two.

**Key takeaways to carry forward:** use async/await for I/O-bound operations — database calls, HTTP calls, file reads. It usually improves server *scalability*, not the intrinsic speed of any one query or request. Use `Task.WhenAll` only for genuinely independent operations where overlapping their waits is safe. Async is not CPU parallelism, and it is not automatically faster in every situation — prefer "async all the way" through a call chain, and test the actual throughput claim under realistic concurrent load rather than assuming it.
