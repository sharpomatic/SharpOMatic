---
title: Running Workflows
sidebar_position: 1
---

This section covers how to run workflows programmatically from your own code.

## Get `IEngineService`

Workflow execution starts from `IEngineService`.

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();
```

If you prefer workflow names instead of stored identifiers, use `GetWorkflowId`.
Workflow names are not guaranteed to be unique, so the helper throws if there is no match or more than one match.

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();
var workflowId = await engine.GetWorkflowId("Example Workflow");
```

You can also get the workflow identifier directly from the editor UI.

<img src="/img/programmatic_workflowid.png" alt="Workflow identifier" width="600" style={{ maxWidth: '100%', height: 'auto' }} />

## Run A Standard Workflow

Standard workflows are one-shot runs started directly from the workflow identifier.

### Await the result

Use `StartWorkflowRunAndWait` when you want the completed `Run` back in the current async flow.

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();
var workflowId = await engine.GetWorkflowId("Example Workflow");

var completed = await engine.StartWorkflowRunAndWait(workflowId);
if (completed.RunStatus == RunStatus.Failed)
{
    Console.WriteLine($"Failed with error {completed.Error}");
}
else if (completed.RunStatus == RunStatus.Success)
{
    var jsonConverters = serviceProvider.GetRequiredService<IJsonConverterService>();
    var context = ContextObject.Deserialize(completed.OutputContext, jsonConverters);
}
```

### Start and return immediately

Use `StartWorkflowRunAndNotify` when you want to begin execution and return the `runId` immediately.
Completion then arrives through `IEngineNotification`.

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();
var workflowId = await engine.GetWorkflowId("Example Workflow");

var runId = await engine.StartWorkflowRunAndNotify(workflowId);
```

### Synchronous execution

`StartWorkflowRunSynchronously` is available when you deliberately want blocking execution.
This is usually not recommended for web request paths.

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();
var workflowId = await engine.GetWorkflowId("Example Workflow");

var completed = engine.StartWorkflowRunSynchronously(workflowId);
```

## Pass Initial State

All standard workflow start methods accept an optional `ContextObject`.
That object becomes the initial run context.

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();
var workflowId = await engine.GetWorkflowId("Tenant Summary");

var context = new ContextObject();
context.Set("input.tenantId", "tenant-42");
context.Set("input.userId", "user-9");
context.Set("input.prompt", "Summarize the latest support tickets.");

var completed = await engine.StartWorkflowRunAndWait(workflowId, context);
```

If you want to mirror the editor's typed start-input experience, you can also pass `ContextEntryListEntity` through the `inputEntries` parameter.
That is useful when you want SharpOMatic to perform the same type conversions used by the **Start** node input editor.

## Run A Conversation Workflow

Conversation workflows use different methods because they carry state across turns.

Use:

- `StartOrResumeConversationAndWait`
- `StartOrResumeConversationAndNotify`
- `StartOrResumeConversationSynchronously`

These methods require both `workflowId` and `conversationId`.

### Start the first turn

If the conversation does not exist yet, SharpOMatic creates it and starts from the **Start** node.

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();
var workflowId = await engine.GetWorkflowId("Support Chat");
var conversationId = $"support-chat-{Guid.NewGuid():N}";

var firstTurn = await engine.StartOrResumeConversationAndWait(
    workflowId,
    conversationId);
```

### Continue after a completed turn

If the latest turn completed successfully, calling the same method again starts a new turn from the **Start** node.
The previous turn's output context becomes the base context for the next turn.

```csharp
var nextTurn = await engine.StartOrResumeConversationAndWait(
    workflowId,
    conversationId);
```

### Resume a suspended turn

If the latest turn suspended, SharpOMatic resumes from the waiting continuation point instead of the **Start** node.
The default resume input is `ContinueResumeInput`, so a plain call is enough when no extra resume data is needed.

```csharp
var resumed = await engine.StartOrResumeConversationAndWait(
    workflowId,
    conversationId,
    new ContinueResumeInput());
```

If the suspend point expects extra data, use `ContextMergeResumeInput` and merge a `ContextObject` into the resume operation.

```csharp
var resumeContext = new ContextObject();
resumeContext.Set("resume.answer", "final answer");
resumeContext.Set("resume.approved", true);

var resumed = await engine.StartOrResumeConversationAndWait(
    workflowId,
    conversationId,
    new ContextMergeResumeInput { Context = resumeContext });
```

### Conversation notifications

The notify variant works the same way but returns immediately with the new `runId`.

```csharp
var runId = await engine.StartOrResumeConversationAndNotify(
    workflowId,
    conversationId);
```

## `IEngineNotification`

`IEngineNotification` is used for workflow and evaluation completion plus connection overrides.
For standard one-shot runs, `conversationId` is `null`.
For conversation turns, `conversationId` is populated and `RunCompleted` can arrive with `RunStatus.Suspended` when the turn stops at a **Suspend** node.

```csharp
public class EngineNotification(IServiceProvider serviceProvider) : IEngineNotification
{
    public Task RunCompleted(
        Guid runId,
        Guid workflowId,
        string? conversationId,
        RunStatus runStatus,
        string? outputContext,
        string? error)
    {
        if (runStatus == RunStatus.Failed)
        {
            Console.WriteLine($"Failed with error {error ?? ""}");
        }
        else if (runStatus == RunStatus.Success)
        {
            var jsonConverters = serviceProvider.GetRequiredService<IJsonConverterService>();
            var context = ContextObject.Deserialize(outputContext, jsonConverters);
        }

        return Task.CompletedTask;
    }

    public Task EvalRunCompleted(
        Guid evalRunId,
        EvalRunStatus runStatus,
        string? error)
    {
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
        if (connectorId == "azure_openai" && authenticationModel.Id == "api_key")
        {
            parameters["endpoint"] = "myEndpoint";
            parameters["api_key"] = "mySecret";
        }
    }
}
```

## `IProgressService`

If you need run-state updates while execution is in progress, implement `IProgressService`.
`RunProgress` is also raised for conversation turns, including the final persisted `RunStatus.Suspended` state when a turn pauses for resume.
For OpenAI, Azure OpenAI, and Google model calls, `StreamEventProgress` and `InformationsProgress` are also raised while the node is still running.
That means partial assistant text, visible reasoning, tool-call lifecycle events, tool-call results, and assistant/reasoning/tool-call trace entries can all be forwarded immediately.
If you want live model-call output, use `IProgressService` rather than polling the repository during the call.
The engine buffers these model-call stream events and persists them when the call completes successfully.

```csharp
public class ProgressService : IProgressService
{
    public Task RunProgress(Run run)
    {
        Console.WriteLine($"Workflow {run.WorkflowId} is now {run.RunStatus}");
        return Task.CompletedTask;
    }

    public Task TraceProgress(Run run, Trace trace)
    {
        Console.WriteLine(
            $"Workflow {trace.WorkflowId} Node {trace.NodeEntityId} is now {trace.Message}");
        return Task.CompletedTask;
    }

    public Task InformationsProgress(Run run, List<Information> informations)
    {
        Console.WriteLine($"Workflow {run.WorkflowId} published {informations.Count} information updates");
        return Task.CompletedTask;
    }

    public Task StreamEventProgress(Run run, List<StreamEventProgressItem> events)
    {
        Console.WriteLine($"Workflow {run.WorkflowId} published {events.Count} stream events");
        foreach (var item in events)
            Console.WriteLine($"  {item.Event.EventKind} silent={item.Silent}");
        return Task.CompletedTask;
    }

    public Task EvalRunProgress(EvalRun evalRun)
    {
        Console.WriteLine($"Eval run {evalRun.EvalRunId} is now {evalRun.Status}");
        return Task.CompletedTask;
    }
}
```

If you are hosting the embedded editor, live browser updates are normally only sent for runs created with `needsEditorEvents: true`.

For model calls, `StreamEventProgress` can now include:

- assistant text start, content, and end events
- visible reasoning lifecycle events
- tool-call start, args, end, and result events

For code-node stream helpers, `StreamEventProgress` also carries a transient `Silent` flag.
That flag is only for live progress consumers such as AG-UI SSE translation and is not stored in the persisted `StreamEvent` rows.
Code nodes can emit text, step, reasoning, tool-call, activity, state, and custom lifecycles through the `Events.Add*` helper methods.

`InformationsProgress` can include the corresponding assistant, reasoning, and tool-call trace entries that appear in the trace viewer.

## Notes

- Use standard workflow methods for one-shot workflows.
- Use conversation methods only for conversation-enabled workflows.
- Conversation-enabled workflows cannot be started through the standard workflow methods.
- Suspended conversations cannot accept start input entries during resume.
- `conversationId` is a string and can be any identifier your application controls.

For more detail on state handling, see [Managing State](./managing-state.md).
For protocol-based conversation clients, see [AG-UI](../ag-ui/endpoint.md).
