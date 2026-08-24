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

`sampleCount` is optional. If omitted, all runnable rows are run according to each row's Repeat value.
Rows with Repeat `0` are skipped.
If provided, it must be between `1` and the number of rows whose Repeat value is greater than `0`.
Rows are chosen randomly for each run, and sampled rows execute once regardless of their Repeat value.

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

## Supplying stored chat messages

When an evaluation has **Chat Messages** enabled, SharpOMatic asks the host application for the conversation to grade.
Implement `EvalChatMessages` on an `IEngineNotification`.
Every member of that interface has a default implementation, so an existing implementation only needs the new method
added and nothing else changes.

```csharp
public class EvaluationChatMessages(IConversationStore store) : IEngineNotification
{
  public async ValueTask<IList<ChatMessage>?> EvalChatMessages(
    EvalChatMessageContext context,
    CancellationToken cancellationToken = default)
  {
    // The row's column values are in the grader context, so an evaluation column carrying the identifier
    // is the usual way to point at a stored conversation.
    if (!context.GraderContext.TryGet<string>("input.conversationId", out var conversationId))
      return null;

    var stored = await store.GetMessagesAsync(conversationId, cancellationToken);
    return [.. stored];
  }
}
```

Register it like any other notification.

```csharp
  builder.Services.AddSingleton<IEngineNotification, EvaluationChatMessages>();
```

`EvalChatMessageContext` identifies the row being graded:

| Member | Purpose |
| --- | --- |
| `EvalConfigId` | the evaluation the row belongs to |
| `EvalRunId`, `EvalRunRowId` | the run and the row execution |
| `EvalRowId`, `RowName`, `RowOrder` | the configured row, its `Name` column, and its order |
| `ExecutionOrder` | distinguishes repeats of the same row |
| `RunId`, `WorkflowId`, `ConversationId` | the workflow run just completed |
| `GraderContext` | the row's column values merged with the workflow output, being what the graders are about to receive |

Return `null` to leave the lookup to another implementation; the first non-null result is used.
Return an empty list to state that the conversation was found and holds no messages, which writes an empty list rather
than leaving the path absent.
Treat `GraderContext` as read-only: return the messages instead of writing them into it, so the configured path and the
cloning rules are applied consistently.

Throwing fails that row only, with the exception wrapped in a `SharpOMaticException` naming the row.
Other rows in the run continue.

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
