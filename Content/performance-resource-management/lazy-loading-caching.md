---
title: "Lazy-Loading and Caching Per-Request Data with ??="
slug: lazy-loading-caching
summary: Caching an expensive per-tenant lookup on the object that needs it, using the null-coalescing assignment operator, so repeated calls on the same instance hit the database at most once.
order: 2
sources: [Lazy loading from db class.docx]
related: [readonlymemory, func-as-behavior-parameter]
---

## Problem

A request handler needs several pieces of tenant configuration — settings, feature flags, limits — each backed by its own database call. If each piece is fetched every time it's accessed, and it's accessed multiple times while handling one request (once to check a feature flag, again to apply a limit, again to log it), the same data gets fetched from the database repeatedly for no reason within a single logical unit of work.

## When It Applies

This applies whenever an object is constructed once per logical scope — once per tenant, once per request — and some of its data is expensive to fetch (a database call, a remote API) but only needs to be fetched once *within that scope*, however many times it's actually read. It's the right fit when the caching lifetime should match the object's own lifetime exactly — not shorter (which brings back the repeated-fetch problem) and not longer (which would mean stale data leaking across requests or tenants).

## Solution

Cache the data per object using a nullable backing field and the null-coalescing assignment operator (`??=`): the first call populates the field from the database, and every subsequent call on the *same instance* returns the cached value without hitting the database again.

```csharp
public sealed class TenantContext
{
    private readonly string _tenantKey;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IFeaturesRepository _featuresRepo;
    private readonly ILimitsRepository _limitsRepo;

    private Settings? _settings;
    private Features? _features;
    private Limits? _limits;

    public TenantContext(
        string tenantKey,
        ISettingsRepository settingsRepo,
        IFeaturesRepository featuresRepo,
        ILimitsRepository limitsRepo)
    {
        _tenantKey = tenantKey;
        _settingsRepo = settingsRepo;
        _featuresRepo = featuresRepo;
        _limitsRepo = limitsRepo;
    }

    public async Task<Settings> GetSettingsAsync()
    {
        return _settings ??= await _settingsRepo.GetByTenantAsync(_tenantKey);
    }

    public async Task<Features> GetFeaturesAsync()
    {
        return _features ??= await _featuresRepo.GetByTenantAsync(_tenantKey);
    }

    public async Task<Limits> GetLimitsAsync()
    {
        return _limits ??= await _limitsRepo.GetByTenantAsync(_tenantKey);
    }
}
```

Each of the three properties caches independently — calling `GetSettingsAsync()` three times issues one database call; calling `GetFeaturesAsync()` separately issues its own single call the first time it's touched, not before. If the function is called multiple times using the *same* `TenantContext` instance, it will not call the database again after the first time — only the first call per property, per instance, reaches the database; every call after that is served from the cached field.

## Example: Using TenantContext Across One Request

```csharp
public async Task HandleRequestAsync(
    string tenantKey,
    IEnumerable<Item> items)
{
    var tenant = new TenantContext(
        tenantKey,
        _settingsRepo,
        _featuresRepo,
        _limitsRepo);

    foreach (var item in items)
    {
        var settings = await tenant.GetSettingsAsync();
        var features = await tenant.GetFeaturesAsync();
        var limits   = await tenant.GetLimitsAsync();

        Process(item, settings, features, limits);
    }
}
```

**Expected behavior:** no matter how many items are in `items`, `GetSettingsAsync()`, `GetFeaturesAsync()`, and `GetLimitsAsync()` each hit the database at most once for the life of this `tenant` instance — the first iteration through the loop populates all three cached fields, and every later iteration reads them back in memory. A loop processing 500 items makes 3 database calls total for this tenant's configuration, not 1,500.

## Tradeoffs & Common Mistakes

- **Reusing the same `TenantContext` instance across requests or tenants.** The cache lives exactly as long as the instance does — if a `TenantContext` is accidentally shared across requests (e.g. registered as a singleton instead of scoped, or cached in a static field), the first tenant's data gets served to every later caller.
- **Expecting the cache to ever refresh.** `??=` populates the field once and never again for the life of the instance — if settings can change mid-request and that matters, this per-instance cache is the wrong lifetime; a time-boxed or externally-invalidated cache (e.g. a distributed cache with a TTL) is needed instead.
- **Forgetting this only caches per-instance, not globally.** Two different `TenantContext` instances for the *same* tenant still each make their own database call — this pattern is purely about avoiding redundant calls within one object's lifetime, not about cross-request or cross-process caching.
- **Relying on `??=` for a cache that must be thread-safe.** `??=` on a plain field is not atomic under concurrent access from multiple threads touching the same instance at once — if `TenantContext` can legitimately be accessed concurrently (not just sequentially within one request), this needs a thread-safe variant (e.g. `Lazy<T>`) instead of a bare nullable field.

## Alternatives

- **Need the cache to survive beyond one object's lifetime** — across requests, or across process restarts? Use a proper cache layer (in-memory `IMemoryCache` or a distributed cache like Redis) with an explicit expiration policy, rather than stretching this per-instance pattern to do a job it isn't designed for.
- **Need thread-safe lazy initialization on a single field** rather than a full per-instance object like `TenantContext`? `Lazy<T>` gives the same "compute once, cache forever" behavior as `??=` but with built-in thread safety for the initialization itself.
