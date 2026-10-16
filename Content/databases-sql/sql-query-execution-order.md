---
title: SQL Query Execution Order
slug: sql-query-execution-order
summary: SQL reads top to bottom, but it doesn't execute that way — the gap between written order and actual execution order explains several "why doesn't this work" surprises at once.
order: 1
sources: [Sql query execution order.docx]
related: [sqlcommand-decorator]
flagged: true
flagNote: This page reworks and substantially condenses a short public post on SQL execution order rather than reproducing it — included because the underlying fact (SQL's logical execution order) is genuinely useful reference material, not because the original post is the author's own case study.
---

## Problem

A query reads `SELECT ... FROM ... WHERE ... GROUP BY ... HAVING ... ORDER BY`, top to bottom — but that's the order it's *written* in, not the order it *executes* in. The database engine processes clauses in a different sequence, and several common "why doesn't this work" moments — referencing a `SELECT`-defined alias inside a `WHERE` clause, or being surprised that `HAVING` can filter on an aggregate but `WHERE` can't — come directly from assuming written order equals execution order.

## When It Applies

Reach for this whenever a query behaves unexpectedly around aliases, aggregates, or filtering — specifically: "why can't I use this column alias here," "why does this filter belong in `HAVING` instead of `WHERE`," or when optimizing a slow query and needing to reason about what the engine actually does first.

## Solution

The logical execution order, regardless of how the query is written:

| Step | Clause | What happens |
|---|---|---|
| 1 | `FROM` / `JOIN` | Determines the base data of interest — which tables, joined how. |
| 2 | `WHERE` | Filters the base data down to rows matching the condition, before any grouping happens. |
| 3 | `GROUP BY` | Groups the filtered rows by the specified column(s), setting up for aggregate calculations. |
| 4 | `HAVING` | Filters *again*, this time on the grouped data — constraints on aggregates belong here, not in `WHERE`. |
| 5 | `SELECT` | Selects the final columns from the filtered, grouped result — window functions are evaluated here too. |
| 6 | `ORDER BY` | Orders the result by one or more columns. |
| 7 | `LIMIT` | Keeps only the top N rows of the ordered result. |

**The two facts that resolve most of the confusion:** `WHERE` runs *before* `GROUP BY`, so it can't filter on an aggregate (the aggregate doesn't exist yet at that point) — that's what `HAVING` is for, since it runs *after* grouping. And `SELECT` — where column aliases are defined — runs *after* `WHERE`, `GROUP BY`, and `HAVING`, which is why an alias defined in `SELECT` generally can't be referenced in those earlier clauses; it doesn't exist yet when they execute.

## Example: Why an Alias Can't Be Used in WHERE

```sql
SELECT
    order_id,
    total_amount * 1.1 AS total_with_tax
FROM orders
WHERE total_with_tax > 100   -- fails: total_with_tax doesn't exist yet here
```

**Expected behavior:** this fails (or is rejected outright, depending on the database engine) because `WHERE` (step 2) executes before `SELECT` (step 5) — `total_with_tax` is a `SELECT`-time alias that simply doesn't exist when `WHERE` runs. The fix is to repeat the expression itself in `WHERE`, or move the filter to `HAVING` if it depends on an aggregate:

```sql
SELECT
    order_id,
    total_amount * 1.1 AS total_with_tax
FROM orders
WHERE total_amount * 1.1 > 100   -- works: no dependency on the alias
```

## Tradeoffs & Common Mistakes

- **Filtering on an aggregate in `WHERE` instead of `HAVING`.** `WHERE total > 100` where `total` is a `SUM(...)` fails for the same reason as the alias example — aggregates don't exist until after `GROUP BY` (step 3), and `WHERE` runs at step 2.
- **Assuming `ORDER BY` can only reference `SELECT`ed columns.** Because `ORDER BY` runs after `SELECT`, it actually *can* reference a `SELECT`-defined alias in most engines — the opposite direction from the `WHERE` case, and a frequent source of "wait, that one works but this one doesn't" confusion.
- **Optimizing by rewriting clauses without understanding what they cost.** Knowing the execution order is what makes it possible to reason about *why* adding an index on a `WHERE`-filtered column helps more than indexing a `SELECT`-only column — the engine narrows the working set as early as step 2, long before `SELECT` ever runs.

## Alternatives

This is close to an immutable fact about how SQL executes rather than a design decision with alternatives — but it's worth knowing that **window functions are evaluated during the `SELECT` step (step 5)**, after `WHERE`/`GROUP BY`/`HAVING` have already narrowed the data, which is exactly why a window function's result can't be filtered directly in the same query's `WHERE` clause (a subquery or CTE wrapping the window function is the usual workaround).
