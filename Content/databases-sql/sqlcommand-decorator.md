---
title: "Wrapping ADO.NET SqlCommand: A Decorator for Timing and Diagnostics"
slug: sqlcommand-decorator
summary: A production wrapper around ADO.NET's SqlCommand that adds per-call timing and parameter logging without modifying SqlCommand itself or breaking any code that expects a real SqlCommand.
order: 2
sources: [SqlCommand Decorator and timelogger.docx]
related: [sql-query-execution-order, extending-a-class-you-cant-modify]
---

## Problem

`SqlCommand` is `sealed` and comes from the framework — there's no way to subclass it to add behavior like automatic timing or parameter logging around every command execution. But every call site in a codebase already constructs and uses `SqlCommand` directly, so whatever solution gets chosen needs to slot in without rewriting every call site, and without losing the ability to pass the result anywhere a real `SqlCommand` is expected.

## When It Applies

This is the concrete, production version of [Extending a Class You Can't Modify](/topics/oop-design/extending-a-class-you-cant-modify) — specifically the Decorator approach, applied to a real framework type. Reach for this shape whenever cross-cutting behavior (timing, logging, retry, diagnostics) needs to wrap every call to a sealed or framework type, and an implicit conversion back to the original type is needed so existing APIs that expect that type keep working unchanged.

## Solution

Wrap `SqlCommand` in a class that exposes the same surface (`CommandText`, `Parameters`, `Connection`, `Transaction`, `ExecuteNonQuery`, `ExecuteScalar`) by delegating to an internal `SqlCommand` instance, and add an **implicit conversion operator back to `SqlCommand`** — this is what lets the wrapper be passed anywhere a real `SqlCommand` is still expected, without every call site needing to know a wrapper exists.

```csharp
public class CustomSqlCommand : IDisposable
{
    private SqlCommand sqlCommand;

    public string CommandText
    {
        get => sqlCommand.CommandText;
        set => sqlCommand.CommandText = value;
    }

    public CommandType CommandType
    {
        get => sqlCommand.CommandType;
        set => sqlCommand.CommandType = value;
    }

    public SqlParameterCollection Parameters => sqlCommand.Parameters;
    public SqlConnection Connection
    {
        get => sqlCommand.Connection;
        set => sqlCommand.Connection = value;
    }
    public SqlTransaction Transaction
    {
        get => sqlCommand.Transaction;
        set => sqlCommand.Transaction = value;
    }

    public CustomSqlCommand(SqlCommand cmd) => sqlCommand = cmd;
    public CustomSqlCommand() => sqlCommand = new SqlCommand();
    public CustomSqlCommand(string cmdText) => sqlCommand = new SqlCommand(cmdText);
    public CustomSqlCommand(string cmdText, SqlConnection connection)
        => sqlCommand = new SqlCommand(cmdText, connection);
    public CustomSqlCommand(string cmdText, SqlConnection connection, SqlTransaction transaction)
        => sqlCommand = new SqlCommand(cmdText, connection, transaction);

    // The key to drop-in compatibility: anywhere a SqlCommand is expected,
    // a CustomSqlCommand can be passed directly.
    public static implicit operator SqlCommand(CustomSqlCommand connection)
        => connection.sqlCommand;

    public void Dispose() => sqlCommand.Dispose();
}
```

**Expected behavior:** `CustomSqlCommand` can be constructed exactly like a `SqlCommand` (same constructor overloads), used with the same property names, and — because of the implicit operator — passed directly to any API expecting a `SqlCommand`, with the compiler inserting the conversion automatically. Nothing about `SqlCommand` itself changes; the wrapper is the only thing that's new.

## Example: Adding Timing Around Execution, Not Just Delegation

The point of the wrapper isn't just to forward calls — it's to add behavior *around* them. `ExecuteNonQuery` and `ExecuteScalar` wrap the real `SqlCommand` call in a timing scope, with an opt-out flag for callers that don't want it:

```csharp
public int ExecuteNonQuery(bool skipTimeLog = false)
{
    if (skipTimeLog)
        return sqlCommand.ExecuteNonQuery();

    using (new SPTimeLogger(this))
        return sqlCommand.ExecuteNonQuery();
}

public object ExecuteScalar(bool skipTimeLog = false)
{
    if (skipTimeLog)
        return sqlCommand.ExecuteScalar();

    using (new SPTimeLogger(this))
        return sqlCommand.ExecuteScalar();
}
```

A parameter-dumping helper supports whatever `SPTimeLogger` logs alongside the duration — printing each parameter's name and value (or just the name, for binary parameters, to avoid dumping large blobs into a log):

```csharp
internal string GetParameterList()
{
    var str = new StringBuilder();

    if (sqlCommand?.Parameters != null)
    {
        foreach (SqlParameter item in sqlCommand.Parameters)
        {
            switch (item.DbType)
            {
                case DbType.Binary:
                    str.AppendLine(item.ParameterName);
                    break;
                default:
                    str.AppendLine(item.Value == null
                        ? $"{item.ParameterName}: (null)"
                        : $"{item.ParameterName}:{item.Value}");
                    break;
            }
        }
    }

    return str.ToString();
}
```

<div class="site-note">
<strong>Site note:</strong> <code>SPTimeLogger</code>'s own implementation wasn't in the source material — based on how it's used here (constructed with <code>this</code>, disposed at the end of a <code>using</code> block), it's an <code>IDisposable</code> whose constructor presumably starts a stopwatch and whose <code>Dispose()</code> logs the elapsed time plus whatever <code>GetParameterList()</code> returns. That implementation isn't reproduced here since it wasn't supplied.
</div>

**Expected behavior:** every call to `ExecuteNonQuery()` or `ExecuteScalar()` — unless `skipTimeLog: true` is passed explicitly — is automatically timed, with the timing scope exactly bounded by the `using` block around the real `SqlCommand` call. Because the opt-out is a parameter with a default value, every *existing* call site that doesn't know about timing gets it automatically; only call sites that need to suppress it have to say so explicitly.

## Tradeoffs & Common Mistakes

- **Forgetting the implicit operator, and losing drop-in compatibility.** Without `implicit operator SqlCommand(CustomSqlCommand)`, every API that specifically requires a `SqlCommand` (not an interface, not a wrapper) would need its own call site changed to unwrap the decorator manually — the whole value of this shape depends on that one operator existing.
- **Forwarding the surface incompletely.** `SqlCommand` has a larger API than what's shown here — any property or method not forwarded is simply unavailable on `CustomSqlCommand`, which surfaces as a compile error at the call site, not a silent behavior change. Worth tracking deliberately as the wrapper's surface grows, rather than discovering gaps one missing member at a time.
- **Timing every call unconditionally, with no opt-out.** Some call sites genuinely don't want per-call logging noise — the `skipTimeLog` parameter exists specifically so high-frequency, low-value calls can opt out without losing timing everywhere else.
- **Mixing timing/logging concerns into the data-access logic itself**, instead of isolating it in the decorator. The whole benefit of this shape is that nothing about how `SqlCommand` is actually used has to change — the timing is bolted on from outside, not threaded through every query.

## Alternatives

- **Need this exact shape for a different sealed or framework type?** The general pattern — wrap, delegate the surface, add an implicit conversion back to the original type — is covered generically in [Extending a Class You Can't Modify](/topics/oop-design/extending-a-class-you-cant-modify).
- **Only need to time a block of code once, not wrap a whole type's API?** A plain `Stopwatch` in a `using`-less scope, or a smaller single-purpose `IDisposable` timer, is simpler when there's no need for drop-in compatibility with an existing type's call sites.
