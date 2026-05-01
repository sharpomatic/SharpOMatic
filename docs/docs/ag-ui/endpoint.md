---
title: Endpoint Reference
sidebar_position: 3
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

app.MapControllers();
app.MapSharpOMaticEditor(sharpOMaticBasePath);
```

`MapSharpOMaticEditor` automatically adds `/editor` to the base path.
`AddSharpOMaticAgUi` registers an MVC controller route, so the host must call `app.MapControllers()`.
There is no separate `MapSharpOMaticAgUi` call.

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
    }
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
- the controller loads the stored conversation context, replaces `agent` with the latest AG-UI request mapping, and sets or clears `agent._hidden.state` from the incoming `state`
- the controller does not append incoming AG-UI messages into `input.chat`
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

`input.chat` is the conventional context path for provider-neutral `ChatMessage` history used by `ModelCall` nodes.
It is not a transcript mirror of every AG-UI event.
Use these settings when a workflow should pass that history into a model:

- `ChatInputPath = "input.chat"`
- `ChatOutputPath = "input.chat"` for conversation-enabled workflows that want the model request and response to become the next turn's replay history

### Non-conversation workflows

For non-conversation workflows, the AG-UI client must send the full relevant message history on every request.
The controller creates `input.chat` from that incoming history before the workflow starts.

Supported conversion rules:

- `system` -> `ChatRole.System`
- `developer` -> `ChatRole.System`
- `user` -> `ChatRole.User`, except the latest user text message when it is exposed as `agent.latestUserMessage`
- `assistant` messages with string `content`, `toolCalls`, or both -> `ChatRole.Assistant`
- `tool` results with string `content` and `toolCallId` -> `ChatRole.Tool`

The latest user text message is deliberately kept out of `input.chat`.
Use `{{$agent.latestUserMessage.content}}` as the current model prompt instead.
If a `ModelCall` node also writes `ChatOutputPath = "input.chat"`, the model call output becomes the `input.chat` value for the rest of that run.
Because the run is stateless, the next AG-UI request starts from the next incoming `messages` array again.

### Conversation workflows

For conversation-enabled workflows, `input.chat` is canonical model history owned by workflow nodes.
The AG-UI controller does not rebuild it from incoming AG-UI messages on every turn.

On a new turn, SharpOMatic loads the stored workflow context from the previous checkpoint.
If that context contains `input.chat`, it is reused.
The incoming AG-UI message is exposed under `agent`, and a `ModelCall` usually appends it to `input.chat` by using `Prompt = "{{$agent.latestUserMessage.content}}"` and `ChatOutputPath = "input.chat"`.
Each AG-UI turn uses the AG-UI-specific resume input and replaces the root `agent` context for that turn, so `latestUserMessage` and `latestToolResult` reflect only the latest incoming AG-UI message.
Other workflow-owned context, including `input.chat`, continues from the previous checkpoint unless a workflow node writes a new value.
Generic context-merge resume inputs still merge context recursively; the atomic `agent` replacement is specific to AG-UI resume handling.

Incoming AG-UI stream-event echoes from prior model output are not appended back into `input.chat`.
Incoming frontend tool results are also not appended by the controller; the waiting `Frontend Tool Call` node owns any optional chat persistence for that result.

### Workflow-owned writers

`ModelCall` reads **Chat Input Path** to build the provider request, then writes **Chat Output Path** only after the model call has produced responses.
When that output path is `input.chat`, the written list includes portable input chat, prompt text, image messages, assistant responses, and synthetic assistant messages for model tool results.
If **Drop Tool Calls** is enabled on the model call, model tool calls and tool results are omitted from the written chat history.

`Frontend Tool Call` and `Backend Tool Call` are the only non-model nodes that can write tool-call `ChatMessage` entries into `input.chat`.
Both are controlled by **Chat Persistence**:

- `None`: no chat messages are written
- `Function Call Only`: an assistant message with `FunctionCallContent` is written
- `Function Call And Result`: the assistant function-call message and the matching tool-result message are written

The two tool-call nodes always emit AG-UI tool-call stream events for display.
Those stream events are separate from chat persistence.

### Conversion details

- `developer` message `name` is preserved as `AuthorName`
- assistant `toolCalls` must be a JSON array
- each assistant tool call must contain a `function` object with a non-empty `name`
- assistant tool call `function.arguments` must be a JSON string that decodes to a JSON object
- assistant messages with neither non-empty string `content` nor any tool calls are skipped rather than added to `input.chat`
- AG-UI `reasoning` and `activity` messages are ignored when building `input.chat`
- unsupported roles are rejected with `RUN_ERROR`
- multimodal or other non-string message `content` is rejected with `RUN_ERROR`

## Frontend tool calls

SharpOMatic also includes a **Frontend Tool Call** node for AG-UI workflows that need to suspend, wait for one frontend tool result, and then branch.

This is useful when the frontend interaction is workflow control flow rather than durable model history, for example:

- asking the user for approval
- collecting a UI choice
- waiting for a browser-side action before continuing

The node:

- emits AG-UI `TOOL_CALL_START`, `TOOL_CALL_ARGS`, and `TOOL_CALL_END` stream events before suspending
- suspends the conversation
- expects exactly one incoming AG-UI message on resume
- emits `TOOL_CALL_RESULT` when that message is the expected tool result
- routes to `toolResult` after storing the result at the configured result output path
- routes to `otherInput` for any other single valid incoming message

It can optionally keep or remove the function call and tool result from `input.chat`.
New Frontend Tool Call nodes default this chat persistence setting to `None`, which keeps frontend control-flow tool calls out of model chat history unless a workflow explicitly opts in.
If **Hide From Stream** is enabled, handled frontend tool-call stream events are marked with `HideFromReply` so future AG-UI replay does not ask the same frontend question again.

The AG-UI controller does not append the incoming frontend tool result to `input.chat`.
On resume, the node compares the single incoming `tool` message to the stored expected `toolCallId`.
If it matches, the node owns the optional chat persistence and the `TOOL_CALL_RESULT` stream event.
If it does not match, the node abandons the pending frontend tool call, removes any chat messages it created for that pending call, hides the original tool-call stream events from future replay, and follows `otherInput`.
Invalid resume payloads, such as missing message ids or non-string `content`, fail the run rather than branching.

## Backend tool calls

SharpOMatic also includes a **Backend Tool Call** node for workflows that already know the tool result during the current run.

This node:

- emits `TOOL_CALL_START`, `TOOL_CALL_ARGS`, `TOOL_CALL_END`, and `TOOL_CALL_RESULT` immediately
- does not suspend or branch
- can optionally add the assistant function call and tool result into `input.chat`

Use it when the workflow wants AG-UI tool-call rendering for backend-generated data without waiting for a later frontend response.
The emitted stream events are created regardless of **Chat Persistence**.
New Backend Tool Call nodes default **Chat Persistence** to `Function Call And Result`.
When **Chat Persistence** is `None`, the frontend can still render the tool call in the live stream and stored stream history, but no `ChatMessage` is added for future model input.

## Suspend resume behavior

The **Suspend** node pauses a conversation-enabled workflow and stores a checkpoint.
The node itself does not write to `input.chat` and does not emit any stream events.

On resume:

- an empty resume continues from the checkpoint without changing context
- a context-merge resume overwrites or adds the provided context values before execution continues
- an AG-UI conversation request is normally converted into a context-merge resume that loads checkpoint context, replaces `agent`, and sets or clears the hidden state baseline

For a user reply, the resumed workflow reads the text from `agent.latestUserMessage.content`.
SharpOMatic does not store that incoming user text as stream history during conversation resume, and it is not appended to `input.chat`.
If the workflow needs a user message in stream history without a model call, add it explicitly from a **Code** node with `Events.AddTextMessageAsync(StreamMessageRole.User, messageId, text, silent: true)`.

For a frontend tool result, the resumed workflow reads the result from `agent.latestToolResult` or from the waiting **Frontend Tool Call** node.
The suspend resume mechanism does not append that tool result to `input.chat` and does not create a `TOOL_CALL_RESULT` event by itself.
The **Frontend Tool Call** node creates the tool-result stream event and any optional chat messages when the resumed message matches its pending `toolCallId`.

## SSE behavior

The endpoint streams AG-UI SSE events from SharpOMatic workflow stream events:

- `RUN_STARTED` is emitted after the workflow turn starts
- text stream events become `TEXT_MESSAGE_START`, `TEXT_MESSAGE_CONTENT`, and `TEXT_MESSAGE_END`
- step stream events become `STEP_STARTED` and `STEP_FINISHED`
- state stream events become `STATE_SNAPSHOT` and `STATE_DELTA`
- visible reasoning stream events become `REASONING_START`, `REASONING_MESSAGE_START`, `REASONING_MESSAGE_CONTENT`, `REASONING_MESSAGE_END`, and `REASONING_END`
- tool-call stream events become `TOOL_CALL_START`, `TOOL_CALL_ARGS`, `TOOL_CALL_END`, and `TOOL_CALL_RESULT`
- activity stream events become `ACTIVITY_SNAPSHOT` and `ACTIVITY_DELTA`
- custom stream events become `CUSTOM`
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
When a conversation turn includes `agent.latestUserMessage`, SharpOMatic exposes that value in workflow context but does not automatically store it as user text stream events.
Model Call nodes store their resolved prompt as silent user text stream events immediately before the provider call unless **Disable User Event** is enabled on the node's **Stream** tab.
If a workflow needs to store the incoming user text without a model call, add it explicitly from a **Code** node:

```csharp
var messageId = Context.Get<string>("agent.latestUserMessage.id");
var text = Context.Get<string>("agent.latestUserMessage.content");
await Events.AddTextMessageAsync(StreamMessageRole.User, messageId, text, silent: true);
```

The `silent` flag only affects the live AG-UI SSE output for the current run.
The stored run or conversation stream history still contains manually emitted user message events, and no persisted stream-event field is added for the flag.

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

If the state already lives in `agent.state`, prefer the higher-level sync helper. It compares `agent.state` to the hidden `agent._hidden.state` baseline, emits a JSON Patch delta when possible and smaller, and falls back to a full snapshot for root replacements or when the snapshot is cheaper:

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
