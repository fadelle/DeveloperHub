---
title: "SQL Interview Questions: Quick-Reference Answers"
slug: sql-interview-questions
summary: Rapid-fire answers to common SQL and relational database interview questions — keys, normalization, transactions, indexes, joins, and query optimization.
sources: [Sql database interview questions.docx]
---

A quick-reference list of core relational-database concepts, condensed to the point rather than written out as a full case.

**Database / SQL.** A database is an organized, electronically stored and accessed collection of structured data. SQL (Structured Query Language) is the standardized language for defining, querying, and modifying that data.

**The four categories of SQL statements.** DDL (Data Definition Language) — `CREATE`, `ALTER`, `DROP` — defines database structure. DML (Data Manipulation Language) — `INSERT`, `UPDATE`, `DELETE`, `SELECT` — manipulates the data itself. DCL (Data Control Language) — `GRANT`, `REVOKE` — controls access and permissions. TCL (Transaction Control Language) — `COMMIT`, `ROLLBACK`, `SAVEPOINT` — manages transactions.

**Primary key / foreign key.** A primary key uniquely identifies each row in a table and enforces that uniqueness. A foreign key is a column (or columns) in one table referencing another table's primary key, establishing and enforcing a relationship between the two.

**Normalization / denormalization.** Normalization organizes data to minimize redundancy — breaking a large table into smaller related ones connected by keys — improving integrity at the cost of needing more joins. Denormalization deliberately reintroduces redundancy to reduce the number of joins a query needs, trading storage and consistency risk for read performance.

**Indexes.** A structure built on one or more columns that lets the engine locate matching rows without scanning the whole table — dramatically speeding up searches, sorts, and joins at the cost of slower writes (every index has to be maintained on insert/update/delete) and additional storage.

**Transactions and ACID.** A transaction is a sequence of operations treated as one indivisible unit. ACID guarantees: **Atomicity** (all operations succeed or none do), **Consistency** (a transaction moves the database between valid states, respecting its integrity constraints), **Isolation** (concurrent transactions don't see each other's uncommitted changes), **Durability** (once committed, changes survive crashes or power loss).

**View vs. table.** A table physically stores rows and columns. A view is a virtual table — defined by a `SELECT` statement, computed from underlying tables on access — used to simplify complex queries, restrict visible columns or rows, or present a customized shape of the data without duplicating it.

**Stored procedure.** A named, precompiled collection of SQL statements stored in the database, callable on demand with parameters — used for encapsulating complex or frequently repeated logic, improving reusability, performance, and security (callers don't need direct table access, just execute permission on the procedure).

**`UNION` vs. `UNION ALL`.** Both combine the result sets of multiple `SELECT` statements. `UNION` removes duplicate rows from the combined result; `UNION ALL` keeps every row, including duplicates — and is faster, since it skips the deduplication work.

**Self-join.** A table joined with itself, typically via two aliases, to compare rows within the same table — common for hierarchical data (an `employee` table referencing a `manager_id` that's itself an `employee`) or other recursive relationships.

**`INNER JOIN` vs. `OUTER JOIN`.** `INNER JOIN` returns only rows with a match in both tables. `OUTER JOIN` (`LEFT`, `RIGHT`, or `FULL`) also returns unmatched rows from one or both tables, filling in `NULL` for columns that have no match.

**`DELETE` vs. `TRUNCATE` vs. `DROP`.** `DELETE` removes specific rows based on a condition, can be rolled back, and leaves the table structure intact. `TRUNCATE` removes all rows at once (faster, since it deallocates whole data pages instead of row-by-row), cannot be rolled back in most engines, and also leaves the structure intact. `DROP` removes the table (or other object) entirely, structure and all — permanent, no rollback.

**Clustered vs. non-clustered index.** A clustered index determines the physical storage order of a table's rows — a table can have only one, since rows can only be physically sorted one way. A non-clustered index is a separate structure with a copy of selected columns plus a pointer back to the actual row — a table can have several, each speeding up queries against different columns.

**Optimizing a slow query.** Index the columns actually used in `WHERE`/`JOIN` conditions; avoid unnecessary joins; keep predicates index-friendly (avoid wrapping indexed columns in functions or implicit type conversions); fetch only the columns and rows actually needed (`LIMIT`/`TOP`); normalize to reduce redundancy, denormalizing selectively only where it measurably helps; and use the engine's execution-plan tooling to find where time is actually being spent. See [SQL Query Execution Order](/topics/databases-sql/sql-query-execution-order) for the execution-order facts that explain *why* some of these levers work the way they do.

**Deadlocks.** Occur when two or more transactions each hold a lock the other needs, so neither can proceed. Mitigated by acquiring locks in a consistent order across the codebase, keeping transactions and lock durations short, using lock timeouts with retry logic, and choosing the least restrictive isolation level that still meets correctness requirements.

**`GROUP BY`.** Groups rows sharing values in one or more columns so aggregate functions (`COUNT`, `SUM`, `AVG`, …) can be computed per group rather than across the whole result set — the foundation of most reporting and summarization queries.
