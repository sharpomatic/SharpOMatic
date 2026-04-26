---
title: Running Evaluations
sidebar_position: 4
---

This section covers running evaluations programmatically.
For evaluation concepts and editor flows, see [Evaluations](../core-concepts/evaluations.md).

## Editor vs Programmatic

Use the editor to design and maintain evaluation configurations (columns, rows, and graders).
Use programmatic APIs when your application needs to start runs, monitor progress, and process run outcomes as part of automation.
Evaluation runs must reference non-conversation workflows for both the main workflow and all graders.
Conversation-enabled workflows are excluded from the editor selectors because evaluation execution cannot respond to suspend events.

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

## Score calculation

Grader workflows should write their score to the output context path `score`.
The score can be numeric, or a string value that can be parsed as a number.

Each grader's pass threshold is used for pass-rate summaries: a scored grader result passes when `score >= passThreshold`.
Failed grader runs and grader runs without a numeric score are excluded from score statistics and pass-rate denominators.

`EvalConfig.RowScoreMode` controls row scores:

- `FirstGrader`: use the first grader's score, based on grader order.
- `Average`: average all available grader scores for the row.
- `Minimum`: use the lowest available grader score for the row.
- `Maximum`: use the highest available grader score for the row.

`EvalConfig.RunScoreMode` controls the metric stored in `EvalRun.Score`.
Only graders with `IncludeInScore` enabled contribute to this run score:

- `AverageScore`: average the selected graders' average scores.
- `MinimumScore`: average the selected graders' minimum scores.
- `MaximumScore`: average the selected graders' maximum scores.
- `PassRate`: average the selected graders' pass rates.

## Completion notifications

If you register `IEngineNotification`, evaluation completion arrives through `EvalRunCompleted`.

```csharp
public class EngineNotification : IEngineNotification
{
    public Task RunCompleted(
        Guid runId,
        Guid workflowId,
        string? conversationId,
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
        string? conversationId,
        string connectorId,
        AuthenticationModeConfig authenticationModel,
        Dictionary<string, string?> parameters)
    {
    }
}
```

## Progress notifications

If you register `IProgressService`, eval progress updates arrive through `EvalRunProgress`.
Evaluation rows and graders also create underlying workflow runs, but editor live workflow updates are typically reserved for explicit editor-started runs.
For evaluation monitoring, rely on `EvalRunProgress` for the aggregate progress signal.

```csharp
public class ProgressService : IProgressService
{
    public Task RunProgress(Run run)
    {
        return Task.CompletedTask;
    }

    public Task TraceProgress(Run run, Trace trace)
    {
        return Task.CompletedTask;
    }

    public Task InformationsProgress(Run run, List<Information> informations)
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

If you are building outside the editor UI, you can call the evaluation endpoints exposed under `/sharpomatic/api/eval` by the editor host.
