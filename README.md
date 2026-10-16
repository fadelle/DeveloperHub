# Dev Knowledge Hub

An ASP.NET Core 10 (Razor Pages) site that organizes a senior .NET engineer's working notes —
concurrency, async streams, OOP design, and performance — into navigable, cross-linked cases.

## Running it

Requires the .NET 10 SDK.

```bash
dotnet run
```

Then open `http://localhost:5171` (or whatever port the console prints). The app also has a
`.claude/launch.json` entry (`devknowledgehub`) for opening it in the Claude Code browser pane.

## Deploying to GitHub Pages

This is a real ASP.NET Core server (Razor Pages, server-side Markdown parsing, a live search
endpoint) — GitHub Pages only serves static files, so it can't run the app directly. Instead,
[`tools/export-static.js`](tools/export-static.js) boots the real app on a scratch port, crawls
every page via a `/__routes.json` discovery endpoint, and saves each page's actual rendered HTML
to disk — same CSS, same JS, same layout, nothing reimplemented. The one functional difference:
`/api/search` (a live server endpoint) is replaced by a static `search-index.json` that `site.js`
fetches once and filters client-side — same debounced search box, same results, just client-side.

**It's wired to deploy automatically**: [`.github/workflows/deploy-pages.yml`](.github/workflows/deploy-pages.yml)
runs the export and publishes to GitHub Pages on every push to `main`. One-time setup needed in
the repo: **Settings → Pages → Source → GitHub Actions** (instead of "Deploy from a branch") —
I can't flip that setting myself, it's a repo-settings change.

To export and preview it locally instead — two ways, depending on what you're checking:

```bash
# Quick sanity check — no subpath, open http://localhost:8080 directly
node tools/export-static.js dist
node tools/static-serve.js dist 8080

# Exact rehearsal of the real GitHub Pages URL structure — open
# http://localhost:8080/DeveloperHub/ (note the path: the export bakes
# "/DeveloperHub" into every link, so the site only resolves correctly
# under that same subpath locally too, not at the server root)
node tools/export-static.js dist /DeveloperHub
node tools/static-serve.js dist 8080
```

(`tools/static-serve.js` is a zero-dependency static file server for exactly this — smoke-testing
the exported `dist/` folder before trusting it to a real deploy. The Claude Code browser pane also
has a `devknowledgehub-static-preview` launch entry wired to it, serving `dist/` on port 8080.)

The base-path argument matters because a GitHub Pages *project* site (not a
`<user>.github.io` root site) is served at `https://<user>.github.io/<repo>/`, a subpath, so every
root-relative link (`href="/topics/..."`, the JS's `fetch("/search-index.json")`, even the values
*inside* `search-index.json`) needs that prefix rewritten in — the export script does this as a
string-rewrite pass over the rendered output, documented in the script's own comments. Omit the
argument (or pass `""`) if hosting at a domain root instead (a custom domain, or a `<user>.github.io`
user/org page).

**One real gotcha the hard way, in case the export tool ever needs touching again:** ASP.NET
Core's `MapStaticAssets()` serves CSS/JS under content-hashed URLs (`site.34dw87a5u2.css`) that
only resolve through the live server's own middleware — there's no file by that name in `wwwroot`
to just copy. The export script instead *fetches* each asset from the live server at its real
rendered URL and saves it under that exact name. It also must run the scratch server with
`ASPNETCORE_ENVIRONMENT=Development` — in Production, without a full `dotnet publish`, that
fingerprinting middleware silently serves empty bodies instead of erroring.

This is a snapshot, not a live mirror of `Content/` — the GitHub Actions workflow regenerates it
on every push, so day-to-day you don't need to think about it; only run the export manually when
previewing offline.

## Build verification

```bash
dotnet build
```

Last verified: **Build succeeded, 0 Warnings, 0 Errors.**

Manually verified in-browser: home page, all 7 topic index pages, representative case detail
pages from each topic, all 4 reference pages, the flagged-content banner (on both a reference
page and a case page), dark/light mode toggle, mobile layout (375px width, no horizontal
overflow), tab switching on multi-example cases, cross-topic related-case links, code copy
buttons, and the `/api/search` endpoint against newly-added content.

## How content is organized

**Content is completely separate from UI code.** Every case and reference article is a plain
Markdown file under [`Content/`](Content), with a small front-matter header and a fixed set of
`## `-level section headings. The [`ContentRepository`](Services/ContentRepository.cs) service
parses all of them once at startup into strongly-typed pages — nothing about adding a case
requires touching a `.cshtml` or `.cs` file.

```
Content/
  topics.json                                  — topic metadata (title, summary, icon, order)
  <topic-slug>/
    <case-slug>.md                             — one file per parent case
  reference/
    <article-slug>.md                          — glossary / notes, outside the case format
```

### Adding a new case

1. Pick (or create) a topic folder under `Content/` matching a `slug` in `topics.json`.
2. Create `Content/<topic-slug>/<new-case-slug>.md` with this front matter:

   ```yaml
   ---
   title: Your Case Title
   slug: your-case-slug
   summary: One or two sentences — shown on the topic page and the home page search index.
   order: 5
   sources: [SourceFile.docx]
   related: [some-other-case-slug]
   ---
   ```

3. Write the body using exactly these `## ` headings, in this order (any can be omitted except
   you need at least one `## Example: ...`):

   ```markdown
   ## Problem
   ## When It Applies
   ## Solution
   ## Example: First Example Title
   ## Example: Second Example Title
   ## Tradeoffs & Common Mistakes
   ## Alternatives
   ```

   Each `## Example: <title>` becomes its own tab on the page — add as many as the case needs.
   Everything is plain Markdown: fenced code blocks (```csharp) get syntax highlighting and a
   copy button automatically; pipe tables, bold/italic, and inline code all work normally.

4. **Wrap any inline code reference that contains `<T>` in backticks** (`` `Channel<T>` ``) —
   unescaped angle brackets outside a code span will be parsed as literal (invalid) HTML.

5. Restart `dotnet run` — content is loaded once at startup, not hot-reloaded.

### Adding a new reference article

Same idea, under `Content/reference/<slug>.md`, with front matter `title`, `slug`, `summary`,
`sources`, and optionally `flagged: true` + `flagNote: "..."` to show the orange flagged-content
banner used for `senior-technical-expertise-subjects.md`.

### The flagged-content convention (works on cases too)

`flagged: true` + `flagNote: "..."` in front matter works on **case** files as well as reference
articles — see `sql-query-execution-order.md` for an example. Use it when a source file's content
is substantially someone else's published material (a tutorial site, a LinkedIn post) rather than
the author's own first-hand notes: condense and substantially reword it instead of reproducing it
at length, and flag the page so a reader knows it's reference material being organized here, not
an original case. The `threads-tpl-async.md` case shows the lighter-weight version of the same
idea — it's mostly original material with one `.site-note`-wrapped paragraph disclosing that two
of its five sources were condensed tutorial content, rather than flagging the whole page.

### The "site note" convention

Per the brief, any explanatory text added by the site that goes beyond reorganizing the
author's own material is wrapped in a visually distinct block, used so far once, in
[`readonlymemory.md`](Content/performance-resource-management/readonlymemory.md):

```html
<div class="site-note">
<strong>Site note:</strong> explanation of what was added and why.
</div>
```

Everything else on every case page is the author's own material, reorganized into the
Problem / When It Applies / Solution / Examples / Tradeoffs / Alternatives structure and lightly
copy-edited for flow — not invented.

## Architecture

- **`Pages/Index`** — home page, topic cards.
- **`Pages/Topics/Index`** (`/topics/{slug}`) — one topic's case list.
- **`Pages/Cases/Details`** (`/topics/{topicSlug}/{caseSlug}`) — the case template: header with
  source badges → Problem/When It Applies/Solution callouts → tabbed examples → tradeoffs →
  alternatives → related cases → prev/next pager.
- **`Pages/Reference/Index`** / **`Pages/Reference/Details`** — the reference section.
- **`Services/ContentRepository.cs`** — loads and parses every Markdown file at startup; also
  builds the in-memory search index served by `GET /api/search?q=`.
- **`Services/IconHelper.cs`** — small inline-SVG icon set (no icon font dependency).
- **`wwwroot/css/site.css`** — the entire design system (CSS custom properties for theming,
  light/dark via `prefers-color-scheme` + a manual toggle persisted in `localStorage`).
- **`wwwroot/js/site.js`** — theme toggle, mobile sidebar, tab switching, code copy buttons,
  debounced search.

## Checklist: source files → pages

### Batch 1 — `Downloads/cases/` (17 files)

| Source file | Page(s) it became |
|---|---|
| `Await_Inside_Loops_Three_Scenarios.docx` | [Does await Inside a Loop Block the Caller?](Content/concurrency-task-orchestration/await-inside-loop.md) |
| `Task.WhenAll and SemaphoreSlim.docx` | [Task.WhenAll vs SemaphoreSlim: Waiting vs Limiting](Content/concurrency-task-orchestration/task-whenall-vs-semaphoreslim.md) |
| `Semaphore_ParallelForEach_Channel_Guide.docx` | [Choosing a Concurrency Tool](Content/concurrency-task-orchestration/concurrency-tool-comparison.md) |
| `Queue_vs_Channel (1).docx` | [Choosing a Concurrency Tool](Content/concurrency-task-orchestration/concurrency-tool-comparison.md) — "ConcurrentQueue vs Channel" example |
| `Pulsar-Cancellation-Token-and-Shutdown-Flow (1).docx` | [Pulsar Consumer: CancellationToken Layers and Graceful Shutdown](Content/concurrency-task-orchestration/pulsar-cancellation-shutdown.md) |
| `DotNet_Inheritance_vs_Composition_CSharp (1).docx` | [Inheritance vs Composition](Content/oop-design/inheritance-vs-composition.md) |
| `Polymorphism limitations.docx` | [Polymorphism vs Generics](Content/oop-design/polymorphism-vs-generics.md) — Problem/Solution framing |
| `common interface for multiple repos.docx` | [Polymorphism vs Generics](Content/oop-design/polymorphism-vs-generics.md) — "One Interface for Multiple Concrete Repos" example |
| `Generic vs Polymorphism (abstract classes and interfaces).docx` | [Polymorphism vs Generics](Content/oop-design/polymorphism-vs-generics.md) — "Factory + Resolver" example |
| `Generics vs Polymorphism (Abstraction).docx` | [Polymorphism vs Generics](Content/oop-design/polymorphism-vs-generics.md) — "Func-Based Resolver" example |
| `C# ReadOnlyMemory usage.docx` | [ReadOnlyMemory\<T\>: Read-Only Views Without Copying](Content/performance-resource-management/readonlymemory.md) |
| `Lazy loading from db class.docx` | [Lazy-Loading and Caching Per-Request Data with ??=](Content/performance-resource-management/lazy-loading-caching.md) |
| `IEnumerable Vs List Example.docx` | [Lazy Sequences](Content/sequences-iterators-async-streams/lazy-sequences-iterators-async-streams.md) — deferred execution example |
| `Iterators (yield return).docx` | [Lazy Sequences](Content/sequences-iterators-async-streams/lazy-sequences-iterators-async-streams.md) — yield return example |
| `CSharp-Async-Streams-Guide.docx` | [Lazy Sequences](Content/sequences-iterators-async-streams/lazy-sequences-iterators-async-streams.md) — IAsyncEnumerable example |
| `Programming Vocabulary Cheat Sheet (plain English) (1).docx` | [Programming Vocabulary Cheat Sheet](Content/reference/vocabulary-cheat-sheet.md) (Reference) |
| `Senior Technical Expertise Subjects.docx` | [Senior Technical Expertise Subjects](Content/reference/senior-technical-expertise-subjects.md) (Reference, flagged) |

### Batch 2 — `Downloads/cases2/` (21 files)

| Source file | Page(s) it became |
|---|---|
| `Thread library vs TPL.docx` | [Threads, the TPL, and async/await](Content/concurrency-task-orchestration/threads-tpl-async.md) |
| `TPL library.docx` | [Threads, the TPL, and async/await](Content/concurrency-task-orchestration/threads-tpl-async.md) |
| `Async await vs TPL.docx` | [Threads, the TPL, and async/await](Content/concurrency-task-orchestration/threads-tpl-async.md) |
| `Introduction to Concurrency.docx` | [Threads, the TPL, and async/await](Content/concurrency-task-orchestration/threads-tpl-async.md) — condensed/flagged (dotnettutorials.net) |
| `Task-Based Asynchronous Programming in C#.docx` | [Threads, the TPL, and async/await](Content/concurrency-task-orchestration/threads-tpl-async.md) — "Three Ways to Create a Task" example, condensed/flagged (dotnettutorials.net) |
| `Adapter example.docx` | [Adapter Pattern](Content/oop-design/adapter-pattern.md) |
| `Best way to add functionality to library classes.docx` | [Extending a Class You Can't Modify](Content/oop-design/extending-a-class-you-cant-modify.md) |
| `extending sealed classes.docx` | [Extending a Class You Can't Modify](Content/oop-design/extending-a-class-you-cant-modify.md) — Extension Methods & Decorator examples |
| `Boxing and Unboxing in C#.docx` | [Boxing and Unboxing](Content/performance-resource-management/boxing-unboxing.md) |
| `Reference Vs Value Types Explained.docx` | [Value Types vs Reference Types](Content/performance-resource-management/value-types-vs-reference-types.md) |
| `Reference Type Passing Explained.docx` | [Value Types vs Reference Types](Content/performance-resource-management/value-types-vs-reference-types.md) — mutate-vs-reassign example |
| `Sql query execution order.docx` | [SQL Query Execution Order](Content/databases-sql/sql-query-execution-order.md) — condensed/flagged (external post) |
| `SqlCommand Decorator and timelogger.docx` | [Wrapping ADO.NET SqlCommand](Content/databases-sql/sqlcommand-decorator.md) |
| `TDD.docx` | [Test-Driven Development](Content/testing-quality/tdd.md) |
| `Mock database get.docx` | [Unit Testing with Mocks](Content/testing-quality/mocking-with-moq.md) |
| `Mock Example.docx` | [Unit Testing with Mocks](Content/testing-quality/mocking-with-moq.md) — Calculator/Verify example |
| `Software Architectural Styles.docx` | [Choosing a Software Architectural Style](Content/software-architecture/choosing-an-architectural-style.md) |
| `Microservices architecture.docx` | [Choosing a Software Architectural Style](Content/software-architecture/choosing-an-architectural-style.md) — microservices deep-dive example |
| `C# interview questions.docx` | [C# Interview Questions](Content/reference/csharp-interview-questions.md) (Reference) |
| `Sql database interview questions.docx` | [SQL Interview Questions](Content/reference/sql-interview-questions.md) (Reference) |
| `CIL intermediate language.docx` | [Why C# Compiles to CIL Instead of Machine Code](Content/performance-resource-management/cil-and-jit.md) |

**38 of 38 source files mapped.** 20 parent cases across 7 topics, plus 4 reference articles.

### Batch 3 — `Downloads/case3/` (10 files)

| Source file | Page(s) it became |
|---|---|
| `Calling async fxn inside a timer.docx` | [A Timer Calling Async Work Can Exhaust the Thread Pool](Content/concurrency-task-orchestration/timer-async-thread-pool-exhaustion.md) |
| `Concurrent Dictionary C# (Thread Safe).docx` | [ConcurrentDictionary](Content/concurrency-task-orchestration/concurrent-dictionary.md) |
| `is singleton methods thread safe .docx` | [Is a Singleton's Method Thread-Safe?](Content/concurrency-task-orchestration/is-singleton-thread-safe.md) — condensed/flagged (Stack Overflow) |
| `Dynamic Func.docx` | [Passing Behavior as Data with Func\<T\>](Content/oop-design/func-as-behavior-parameter.md) |
| `PubSub Mechanism (Messaging System).docx` | [Pub/Sub: The Mechanism Behind Event-Driven Messaging](Content/software-architecture/pub-sub-messaging.md) |
| `async await c#.docx` | folded into [Threads, the TPL, and async/await](Content/concurrency-task-orchestration/threads-tpl-async.md) — suspension-point mechanics |
| `AsyncAwait VS Task.Run.docx` | folded into [Threads, the TPL, and async/await](Content/concurrency-task-orchestration/threads-tpl-async.md) — fire-and-forget vs fire-then-wait |
| `IO Threads VS Worker Threads.docx` | folded into [Threads, the TPL, and async/await](Content/concurrency-task-orchestration/threads-tpl-async.md) — further-reading link |
| `Design Patterns Links.docx` | folded into [Programming Vocabulary Cheat Sheet](Content/reference/vocabulary-cheat-sheet.md) — further-reading links, no original prose to build a page from |
| `Projects Main Things.docx` | *(not incorporated — see below)* |

**47 of 48 source files mapped** across all three batches (one — `Projects Main Things.docx` —
has no usable prose; see below). 25 parent cases across 7 topics, plus 4 reference articles.

### Not yet incorporated

- **`Projects Main Things.docx`** — a bare list of topic names (db connection, exception
  handling, factory design, distributed cache vs. memory cache, message broker/RabbitMQ,
  AutoMapper, logger, app configs in a database) with no explanatory prose attached to any of
  them. There's nothing here to condense or reorganize into a case — it reads as an outline of
  topics the author might want to write up later, not content itself. Several of these (message
  broker patterns, cache-aside vs. distributed caching, config-in-database vs. config files) would
  make good future cases if elaborated — flagging this as a possible to-do list rather than
  silently dropping it or forcing a thin page out of nine bullet points.

## Known limitations / next steps

- Content is loaded once at process startup — editing a `.md` file requires restarting
  `dotnet run` to see the change (no file-watching hot reload is wired up yet).
- No full-text relevance ranking in search — it's a case-insensitive substring match over title
  and body text, capped at 20 results. Fine at this content volume; would want a real search
  index if the case count grows substantially.
- `Senior Technical Expertise Subjects.docx` is flagged on its page as broader/shallower than the
  rest of the content, per the original brief — it reads as general interview-prep notes rather
  than a first-hand case, and could use a deeper rewrite from the author if it's meant to carry
  the same weight as the other pages.
