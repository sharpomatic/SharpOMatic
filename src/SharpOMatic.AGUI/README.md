# SharpOMatic.AGUI

Optional SharpOMatic extension that exposes an AG-UI compatible endpoint for running SharpOMatic workflows over SSE.
It supports both stateless workflow runs and conversation-enabled workflows.

## Usage

Register the package in your ASP.NET Core host:

```csharp
builder.Services.AddSharpOMaticAgUi();
```

That defaults to `/sharpomatic/api/agui`.

In the DemoServer that is exposed as `https://localhost:9001/sharpomatic/api/agui`.

Choose a different base path if needed:

```csharp
builder.Services.AddSharpOMaticAgUi("/banana");
```

Or override both the base path and the AG-UI child path:

```csharp
builder.Services.AddSharpOMaticAgUi("/banana", "/integrations/chat");
```

The package adds `POST` on the combined path.

## Request requirements

- `threadId` is required.
- `forwardedProps` must contain exactly one of `workflowId` or `workflowName`.
- `workflowName` must match exactly one workflow.

The endpoint resolves the matching SharpOMatic workflow, chooses the correct execution mode, and streams SSE events translated from workflow stream events until the run completes, suspends, or fails.

## SSE event mapping

SharpOMatic translates workflow stream events into AG-UI SSE events:

- `TEXT_MESSAGE_START`, `TEXT_MESSAGE_CONTENT`, `TEXT_MESSAGE_END`
- `STEP_STARTED`, `STEP_FINISHED`
- `STATE_SNAPSHOT`, `STATE_DELTA`
- `REASONING_START`, `REASONING_MESSAGE_START`, `REASONING_MESSAGE_CONTENT`, `REASONING_MESSAGE_END`, `REASONING_END`
- `TOOL_CALL_START`, `TOOL_CALL_ARGS`, `TOOL_CALL_END`, `TOOL_CALL_RESULT`
- `ACTIVITY_SNAPSHOT`, `ACTIVITY_DELTA`
- `RUN_STARTED`, `RUN_FINISHED`, `RUN_ERROR`

`TOOL_CALL_RESULT` preserves both the result `messageId` and the linked `toolCallId`.
`TOOL_CALL_START` includes the tool name and can include `parentMessageId` when the model output supplies it.
Reasoning events use AG-UI-specific `messageId` values prefixed with `reason:` so they do not collide with assistant text messages when a model/provider reuses one response id for both.
Tool result messages use AG-UI-specific `messageId` values prefixed with `tool:` so tool messages stay distinct too, while the linked `toolCallId` remains unchanged.
Activity messages use AG-UI-specific `messageId` values prefixed with `activity:` so activity updates stay distinct from assistant, reasoning, and tool messages.
When batch-mode model calls are replayed without provider message ids, SharpOMatic synthesizes separate assistant `messageId` values for each distinct assistant text lifecycle and seeds them from the stream sequence so they remain unique across conversation turns.
Code-node stream-event helpers can mark events as `silent`, which keeps them in SharpOMatic stream history while suppressing their live AG-UI SSE emission.

## Workflow selection

The recommended request shape is:

```json
{
  "threadId": "support-chat-001",
  "messages": [],
  "state": {},
  "context": [],
  "forwardedProps": {
    "sharpomatic": {
      "workflowId": "11230021-5144-471a-8ec7-9b460354b745"
    }
  }
}
```

`workflowName` can be used instead of `workflowId`:

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

For compatibility, the selector can also live directly under `forwardedProps`, but `forwardedProps.sharpomatic` is the preferred convention.

## Execution modes

SharpOMatic resolves the target workflow first:

- non-conversation workflows are treated as stateless AG-UI targets
- conversation-enabled workflows use normal SharpOMatic conversation storage

For non-conversation workflows:

- the client must send the full AG-UI message history on every call
- SharpOMatic rebuilds `input.chat` from that history for the current run
- `threadId` remains protocol metadata only

For conversation-enabled workflows:

- the first call uses `threadId` as the SharpOMatic `conversationId`
- later calls with the same `threadId` continue or resume that conversation
- later AG-UI calls must be incremental only and send only new messages
- if a later call resends a previously seen AG-UI message id, SharpOMatic returns `RUN_ERROR`

## Workflow context

The controller maps selected request data into workflow context under `agent`:

- `agent.latestUserMessage`: the final item in `messages`, but only when that item is a user text message
- `agent.latestToolResult`: the final item in `messages`, but only when that item is a tool result message. Its `content` stays as the original string, and if that string is non-empty JSON then SharpOMatic also stores the parsed payload in `agent.latestToolResult.value`.
- `agent.messages`: the full incoming AG-UI `messages` array
- `agent.state`: the incoming AG-UI `state` value
- `agent.context`: the incoming AG-UI `context` value
- `agent._hidden.state`: a hidden deep copy of the incoming AG-UI `state`, used as the baseline for `AddStateSyncAsync()` and the `State Sync` node

These values are passed through as structured JSON-compatible data, not as a single raw JSON string.
On each AG-UI start or resume, SharpOMatic updates the `agent` object.
If the workflow context already contains `agent`, the incoming AG-UI `agent` object replaces it entirely.

The controller also converts supported AG-UI messages into provider-neutral `ChatMessage` entries and stores them at `input.chat`.
That is the recommended source for `ModelCall.ChatInputPath`.
For conversation workflows that want replay across turns, also set `ModelCall.ChatOutputPath` to `input.chat`.

Supported `input.chat` conversion in this version:

- `system` -> `ChatRole.System`
- `developer` -> `ChatRole.System`
- `user` -> `ChatRole.User`
- `assistant` text and tool calls -> `ChatRole.Assistant`
- `tool` results -> `ChatRole.Tool`

AG-UI `reasoning` and `activity` messages are ignored for chat conversion in this version.
Unsupported multimodal or non-string message content is rejected with `RUN_ERROR`.

For AG-UI workflows that need a frontend action and then a later resume, SharpOMatic also provides a **Frontend Tool Call** node.
It emits AG-UI tool-call events, suspends the conversation, and resumes through `toolResult` or `otherInput`.
That node can keep or remove the frontend tool exchange from `input.chat`, and it can mark handled stream events with `HideFromReply` so future AG-UI replay does not render the same pending frontend request again.

SharpOMatic also provides a **Backend Tool Call** node for workflows that already know the tool result during the current run.
It emits the full tool-call lifecycle immediately, does not suspend, and can optionally persist the function call and tool result into `input.chat`.

SharpOMatic also provides a **State Sync** node for workflows that want AG-UI state updates without custom code.
It always syncs from `agent.state`, compares against hidden baseline state stored at `agent._hidden.state`, and emits either `STATE_DELTA` or `STATE_SNAPSHOT`.

For simple progress markers, SharpOMatic also provides **Step Start** and **Step End** nodes.
They emit `STEP_STARTED` and `STEP_FINISHED` without suspending or touching `input.chat`.

If a workflow wants to add the incoming AG-UI user message into SharpOMatic stream history without sending it back to the AG-UI caller, use a code node and the transient `silent` flag:

```csharp
var latestUserMessage = Context.Get<ContextObject>("agent.latestUserMessage");
var messageId = latestUserMessage.Get<string>("id");
var text = latestUserMessage.Get<string>("content");

await Events.AddTextMessageAsync(StreamMessageRole.User, messageId, text, silent: true);
```

The `silent` flag only affects the current live AG-UI SSE stream.
The underlying stream event is still persisted in SharpOMatic history, and the flag itself is not stored in the database.

Code nodes can also emit AG-UI activity messages directly:

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

State snapshots and deltas are supported as well:

```csharp
await Events.AddStateSnapshotAsync(
    new { mode = "assistant", count = 1 }
);

await Events.AddStateDeltaAsync(
    new object[] { new { op = "replace", path = "/mode", value = "review" } }
);
```

If state already lives in `agent.state`, prefer the higher-level sync helper:

```csharp
Context.Set("agent.state.mode", "assistant");
await Events.AddStateSyncAsync();

Context.Set("agent.state.mode", "review");
await Events.AddStateSyncAsync();
```

They can also emit AG-UI step lifecycle events directly. The `stepName` is persisted in SharpOMatic stream history using `TextDelta` and replayed as AG-UI step events:

```csharp
await Events.AddStepStartAsync("Search");
await Events.AddStepEndAsync("Search");
```

For AG-UI `CUSTOM` events, use:

```csharp
await Events.AddCustomEventAsync(
    "weather_progress",
    "{\"stage\":\"fetch\"}"
);
```

SharpOMatic stores the custom event name in `TextDelta`, the custom value string in `Metadata`, and replays them as AG-UI `CUSTOM` events with `name` and `value`.

If you do not need code, use the dedicated **Step Start** and **Step End** workflow nodes to emit the same step lifecycle declaratively.
