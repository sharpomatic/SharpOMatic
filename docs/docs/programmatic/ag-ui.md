---
title: AG-UI
sidebar_position: 2
---

SharpOMatic has optional AG-UI support for clients that want to drive workflows over a single SSE endpoint.
It supports both stateless workflow runs and conversation-enabled workflows.

## Install the package

```powershell
dotnet add package SharpOMatic.AGUI
```

## Register the endpoint

Register the package in your ASP.NET Core host and choose the route you want to expose:

```csharp
builder.Services.AddSharpOMaticAgUi();
```

By default that adds:

- AG-UI endpoint: `/sharpomatic/api/agui`
- DemoServer endpoint: `https://localhost:9001/sharpomatic/api/agui`

If you want a different base path, share the same base path variable with the editor, transfer, and AG-UI registration:

```csharp
var sharpOMaticBasePath = "/example/path";

builder.Services.AddSharpOMaticEditor(sharpOMaticBasePath);
builder.Services.AddSharpOMaticTransfer(sharpOMaticBasePath);
builder.Services.AddSharpOMaticAgUi(sharpOMaticBasePath);

app.MapSharpOMaticEditor(sharpOMaticBasePath);
```

`MapSharpOMaticEditor` automatically adds `/editor` to the base path.

If you also want to override the AG-UI child path, pass a second argument:

```csharp
builder.Services.AddSharpOMaticAgUi(sharpOMaticBasePath, "/myChatBot");
```

## Request contract

SharpOMatic uses the AG-UI request to identify the target workflow and the AG-UI thread:

- `threadId` is required.
- `forwardedProps` must specify exactly one of `workflowId` or `workflowName`.
- If `workflowName` is used, it must match exactly one workflow. Zero matches and multiple matches are both errors.

The recommended selector shape is `forwardedProps.sharpomatic`:

```json
{
  "threadId": "support-chat-001",
  "runId": "run-001",
  "messages": [
    {
      "id": "msg-1",
      "role": "user",
      "content": "Summarize the latest customer issue."
    }
  ],
  "state": {
    "example": {
      "first": "Sharpy Demo",
      "second": "test"
    },
  },
  "context": [],
  "forwardedProps": {
    "sharpomatic": {
      "workflowId": "11230021-5144-471a-8ec7-9b460354b745"
    }
  }
}
```

You can also use a workflow name:

```json
{
  "threadId": "support-chat-001",
  "forwardedProps": {
    "sharpomatic": {
      "workflowName": "Support Chat"
    }
  }
}
```

For compatibility, SharpOMatic also accepts `workflowId` or `workflowName` directly under `forwardedProps`, but the nested `sharpomatic` object is the preferred convention.

## Execution modes

SharpOMatic resolves the target workflow first and then chooses the execution mode automatically:

- non-conversation workflows are treated as stateless AG-UI targets
- conversation-enabled workflows keep their normal SharpOMatic conversation behavior

For non-conversation workflows:

- the AG-UI client must send the full message history on every request
- SharpOMatic rebuilds `input.chat` from that incoming history for the current run only, excluding the latest user text message when it is exposed as `agent.latestUserMessage`
- `threadId` remains required AG-UI metadata, but it does not create a SharpOMatic conversation

For conversation-enabled workflows:

- the first request for a `(workflow, threadId)` pair starts a SharpOMatic conversation using `threadId` as the real `conversationId`
- later requests with the same `threadId` continue or resume that conversation
- when the controller can load and merge the stored conversation context, it updates `agent` but does not append incoming AG-UI messages into `input.chat`
- when the controller cannot load repository-backed conversation state and falls back to `AgUiAgentResumeInput`, it does not build or update `input.chat`
- the controller exposes only the latest incoming AG-UI message at `agent.messages`, so a normal user turn has exactly one user text message there
- later requests should be incremental only and send the new AG-UI message for the current turn

Because conversation identifiers are strings, your AG-UI client can use any stable identifier that fits your application.

## Workflow context values

The AG-UI controller does not dump the entire request into workflow context.
Instead it maps a focused subset into `agent`:

- `agent.latestUserMessage`: the final item in `messages`, but only when that item is a user text message
- `agent.latestToolResult`: the final item in `messages`, but only when that item is a tool result message. Its `content` stays as the original string, and if that string is non-empty JSON then SharpOMatic also stores the parsed payload in `agent.latestToolResult.value`.
- `agent.messages`: for non-conversation workflows, the full incoming `messages` array; for conversation-enabled workflows, only the latest incoming message
- `agent.state`: the incoming AG-UI `state`
- `agent.context`: the incoming AG-UI `context`
- `agent._hidden.state`: a hidden deep copy of the incoming AG-UI `state`, used as the baseline for `AddStateSyncAsync()` and the `State Sync` node

These values are preserved as structured JSON-compatible data.
For example, `agent.state` remains an object or array tree inside SharpOMatic context rather than becoming one large JSON string.
On each AG-UI start or resume, SharpOMatic updates `agent`.
If `agent` already exists in workflow context, the incoming AG-UI `agent` object replaces it entirely.

## input.chat behavior

`input.chat` is important for `ModelCall` nodes, but AG-UI does not populate it in exactly the same way for every run mode.

Use these settings when a workflow should pass AG-UI chat into a `ModelCall`:

- `ChatInputPath = "input.chat"`
- `ChatOutputPath = "input.chat"` for conversation-enabled workflows that want persisted replay on later turns

### When SharpOMatic populates input.chat

- non-conversation workflows:
  the controller sets `input.chat` from the current incoming AG-UI `messages` array for that run, excluding `agent.latestUserMessage`
- conversation-enabled workflows with repository-backed checkpoint loading:
  the controller loads the stored workflow context and leaves `input.chat` unchanged; workflow nodes such as `ModelCall` and `Frontend Tool Call` update it when they intentionally persist canonical model chat
- conversation-enabled workflows without repository access:
  the controller resumes with `AgUiAgentResumeInput` and does not build or update `input.chat`

### What is written to input.chat

When `input.chat` is populated, SharpOMatic converts supported AG-UI messages into provider-neutral `ChatMessage` objects there.

Supported conversion rules:

- `system` -> `ChatRole.System`
- `developer` -> `ChatRole.System`
- `user` -> `ChatRole.User`, except the latest user text message when it is exposed as `agent.latestUserMessage`
- `assistant` messages with string `content`, `toolCalls`, or both -> `ChatRole.Assistant`
- `tool` results with string `content` and `toolCallId` -> `ChatRole.Tool`

### Conversion details

- `developer` message `name` is preserved as `AuthorName`
- assistant `toolCalls` must be a JSON array
- each assistant tool call must contain a `function` object with a non-empty `name`
- assistant tool call `function.arguments` must be a JSON string that decodes to a JSON object
- assistant messages with neither non-empty string `content` nor any tool calls are skipped rather than added to `input.chat`
- the latest user text message is not added to `input.chat`; use `agent.latestUserMessage` as the current turn prompt instead
- AG-UI `reasoning` and `activity` messages are ignored when building `input.chat`
- unsupported roles are rejected with `RUN_ERROR`
- multimodal or other non-string message `content` is rejected with `RUN_ERROR`

### Conversation chat ownership

For conversation-enabled workflows:

- `input.chat` is canonical model history owned by workflow nodes
- a `ModelCall` node appends its model response messages when `ChatOutputPath` is set
- a `Frontend Tool Call` node appends a returned frontend tool result only when its chat persistence mode is `Function Call And Result`
- AG-UI stream-event echoes from prior model output are not appended back into `input.chat`
- incoming user text is stored as stream history for conversation turns, but it is not added to `input.chat`

## Frontend tool calls

SharpOMatic also includes a **Frontend Tool Call** node for AG-UI workflows that need to suspend, wait for one frontend tool result, and then branch.

This is useful when the frontend interaction is workflow control flow rather than durable model history, for example:

- asking the user for approval
- collecting a UI choice
- waiting for a browser-side action before continuing

The node:

- emits AG-UI tool-call stream events
- suspends the conversation
- expects exactly one incoming AG-UI message on resume
- routes to `toolResult` when that message is the expected tool result
- routes to `otherInput` for anything else

It can optionally keep or remove the function call and tool result from `input.chat`.
New Frontend Tool Call nodes default this chat persistence setting to `None`, which keeps frontend control-flow tool calls out of model chat history unless a workflow explicitly opts in.
It can also mark handled frontend tool-call stream events with `HideFromReply` so future AG-UI replay does not ask the same frontend question again.

## Backend tool calls

SharpOMatic also includes a **Backend Tool Call** node for workflows that already know the tool result during the current run.

This node:

- emits `TOOL_CALL_START`, `TOOL_CALL_ARGS`, `TOOL_CALL_END`, and `TOOL_CALL_RESULT` immediately
- does not suspend or branch
- can optionally add the assistant function call and tool result into `input.chat`

Use it when the workflow wants AG-UI tool-call rendering for backend-generated data without waiting for a later frontend response.

## SSE behavior

The endpoint streams AG-UI SSE events from SharpOMatic workflow stream events:

- `RUN_STARTED` is emitted after the workflow turn starts
- text stream events become `TEXT_MESSAGE_START`, `TEXT_MESSAGE_CONTENT`, and `TEXT_MESSAGE_END`
- step stream events become `STEP_STARTED` and `STEP_FINISHED`
- state stream events become `STATE_SNAPSHOT` and `STATE_DELTA`
- visible reasoning stream events become `REASONING_START`, `REASONING_MESSAGE_START`, `REASONING_MESSAGE_CONTENT`, `REASONING_MESSAGE_END`, and `REASONING_END`
- tool-call stream events become `TOOL_CALL_START`, `TOOL_CALL_ARGS`, `TOOL_CALL_END`, and `TOOL_CALL_RESULT`
- activity stream events become `ACTIVITY_SNAPSHOT` and `ACTIVITY_DELTA`
- `TOOL_CALL_RESULT` preserves both the tool result `messageId` and the linked `toolCallId`
- `TOOL_CALL_START` includes the tool name and can include `parentMessageId` when the underlying model output supplies it
- reasoning events use AG-UI-specific `messageId` values prefixed with `reason:` so they cannot collide with assistant text messages when a provider reuses one underlying response id for both
- tool result messages use AG-UI-specific `messageId` values prefixed with `tool:` so tool messages also stay distinct from assistant and reasoning messages, while the linked `toolCallId` remains unchanged
- activity messages use AG-UI-specific `messageId` values prefixed with `activity:` so activity updates stay distinct from assistant, reasoning, and tool messages
- when a model call runs in batch mode and the provider does not supply message ids, SharpOMatic synthesizes distinct assistant `messageId` values for each separate assistant text lifecycle, seeded from the stream sequence so they remain unique across conversation turns
- model-call nodes can suppress assistant text, reasoning, or tool-call stream categories, in which case the AG-UI endpoint simply emits fewer events for that run
- code-node stream-event helpers can mark events as `silent`, in which case SharpOMatic still stores them in stream history but skips the live AG-UI SSE translation for that event
- some workflow nodes, such as **Frontend Tool Call**, can mark persisted stream events with `HideFromReply`, in which case those events are excluded from future AG-UI replay/history even though they remain stored in the database
- successful runs emit `RUN_FINISHED`
- suspended runs also emit `RUN_FINISHED`
- failed runs emit `RUN_ERROR`

The SSE request ends when the underlying workflow run finishes, suspends, or fails.

## Incoming user message history

AG-UI clients already know about the incoming user message they just submitted.
When a conversation turn includes `agent.latestUserMessage`, SharpOMatic automatically stores that message as user text stream events before the workflow nodes run.
Those stored events make run and conversation history complete after a page refresh, but they are sent to the current live AG-UI stream with `silent: true` so the caller does not render the submitted message twice.

The `silent` flag only affects the live AG-UI SSE output for the current run.
The stored run or conversation stream history still contains the user message events, and no persisted stream-event field is added for the flag.

## Emitting tool calls from code nodes

Code nodes can also publish tool-call stream events directly.
Use `AddToolCallAsync` when the frontend should execute the tool and return the result later:

```csharp
await Events.AddToolCallAsync(
    "call-1",
    "lookup_weather",
    "{\"city\":\"Sydney\"}",
    "assistant-1"
);
```

Use `AddToolCallWithResultAsync` when the workflow already knows the result and should emit the full lifecycle immediately:

```csharp
await Events.AddToolCallWithResultAsync(
    "call-1",
    "lookup_weather",
    "{\"city\":\"Sydney\"}",
    "tool-result-1",
    "Sunny",
    "assistant-1"
);
```

Use `AddActivitySnapshotAsync` and `AddActivityDeltaAsync` when the frontend should render structured activity updates:

```csharp
await Events.AddActivitySnapshotAsync(
    "plan-1",
    "PLAN",
    new { steps = new[] { new { title = "Search", status = "in_progress" } } },
    replace: false
);

await Events.AddActivityDeltaAsync(
    "plan-1",
    "PLAN",
    new object[] { new { op = "replace", path = "/steps/0/status", value = "done" } }
);
```

If the activity state already lives in workflow context, prefer the higher-level sync helper that persists a hidden baseline snapshot, computes the delta automatically, and falls back to a replacement snapshot when that is smaller:

```csharp
await Events.AddActivitySyncFromContextAsync(
    "plan-1",
    "PLAN",
    "activity.plan",
    replace: false
);

await Events.AddActivitySyncFromContextAsync(
    "plan-1",
    "PLAN",
    "activity.plan"
);
```

If you want the higher-level activity helper to emit snapshots only and skip delta generation entirely, call:

```csharp
await Events.AddActivitySyncFromContextAsync(
    "plan-1",
    "PLAN",
    "activity.plan",
    snapshotsOnly: true
);
```

Use `AddStateSnapshotAsync` and `AddStateDeltaAsync` when the frontend should render AG-UI state changes directly:

```csharp
await Events.AddStateSnapshotAsync(
    new { mode = "assistant", count = 1 }
);

await Events.AddStateDeltaAsync(
    new object[] { new { op = "replace", path = "/mode", value = "review" } }
);
```

If the state already lives in `agent.state`, prefer the higher-level sync helper. It compares `agent.state` to the hidden `agent._hidden.state` baseline, emits a JSON Patch delta when that is smaller, and falls back to a full snapshot when that is cheaper:

```csharp
Context.Set("agent.state.mode", "assistant");
await Events.AddStateSyncAsync();

Context.Set("agent.state.mode", "review");
await Events.AddStateSyncAsync();
```

If you want the higher-level helper to emit snapshots only and skip delta generation entirely, call:

```csharp
await Events.AddStateSyncAsync(snapshotsOnly: true);
```

Use `AddStepStartAsync` and `AddStepEndAsync` when the frontend should render simple AG-UI step lifecycle markers:

```csharp
await Events.AddStepStartAsync("Search");
await Events.AddStepEndAsync("Search");
```

Use `AddCustomEventAsync` for AG-UI `CUSTOM` events:

```csharp
await Events.AddCustomEventAsync(
    "weather_progress",
    "{\"stage\":\"fetch\"}"
);
```

SharpOMatic maps the custom event name to persisted `TextDelta` and the custom value string to `Metadata`, then emits:

- `type = "CUSTOM"`
- `name = TextDelta`
- `value = Metadata`

If you do not need custom code, use the dedicated **Step Start**, **Step End**, **Activity Sync**, and **State Sync** workflow nodes to emit the same protocol-aware updates declaratively.
