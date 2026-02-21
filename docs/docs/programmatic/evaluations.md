---
title: Running Evaluations
sidebar_position: 4
---

This section covers running evaluations programmatically.
For evaluation concepts and editor flows, see [Evaluations](../core-concepts/evaluations.md).

## Editor vs Programmatic

Use the editor to design and maintain evaluation configurations (columns, rows, and graders).
Use programmatic APIs when your application needs to start runs, monitor progress, and process run outcomes as part of automation.

## Start an evaluation run

Use `IEngineService.StartEvalRun`:

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();

// Start and run all the evaluation runs
var evalRun = await engine.StartEvalRun(evalConfigId);
```

Optional parameters:

- `name`: custom display name for the run
- `sampleCount`: random row sample size

`sampleCount` is optional. If omitted, all rows are run.
If provided, it must be between `1` and the evaluation row count.
Rows are chosen randomly for each run.

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();

// Start with explicit name and a random sample of 25 rows
var evalRun = await engine.StartEvalRun(
    evalConfigId,
    name: "Nightly prompt regression",
    sampleCount: 25);
```

## Completion notifications

If you register `IEngineNotification`, evaluation completion arrives through `EvalRunCompleted`.

```csharp
public class EngineNotification : IEngineNotification
{
    public Task RunCompleted(
        Guid runId,
        Guid workflowId,
        RunStatus runStatus,
        string? outputContext,
        string? error)
    {
        return Task.CompletedTask;
    }

    public Task EvalRunCompleted(
        Guid evalRunId,
        EvalRunStatus runStatus,
        string? error)
    {
        Console.WriteLine($"Eval run {evalRunId} finished with status {runStatus}");
        return Task.CompletedTask;
    }

    public void ConnectionOverride(
        Guid runId,
        Guid workflowId,
        string connectorId,
        AuthenticationModeConfig authenticationModel,
        Dictionary<string, string?> parameters)
    {
    }
}
```

## Progress notifications

If you register `IProgressService`, eval progress updates arrive through `EvalRunProgress`.

```csharp
public class ProgressService : IProgressService
{
    public Task RunProgress(Run run)
    {
        return Task.CompletedTask;
    }

    public Task TraceProgress(Trace trace)
    {
        return Task.CompletedTask;
    }

    public Task EvalRunProgress(EvalRun evalRun)
    {
        Console.WriteLine(
            $"Eval {evalRun.EvalRunId}: {evalRun.CompletedRows}/{evalRun.TotalRows}, status={evalRun.Status}");
        return Task.CompletedTask;
    }
}
```

## API-driven evaluation flows

If you are building outside the editor UI, you can call the evaluation endpoints exposed under `/api/eval` by the editor host.
