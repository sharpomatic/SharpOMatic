---
title: Running Evaluations
sidebar_position: 4
---

This section covers running evaluations programmatically.
For evaluation concepts and editor flows, see [Evaluations](../core-concepts/evaluations.md).

## Start an evaluation run

Use `IEngineService.StartEvalRun`:

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();

// Start against all rows
var evalRun = await engine.StartEvalRun(evalConfigId);
```

Optional parameters:

- `name`: custom display name for the run
- `sampleCount`: random row sample size

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

If you are building outside the editor UI, the evaluation HTTP endpoints are documented in [Evaluations](../core-concepts/evaluations.md).

