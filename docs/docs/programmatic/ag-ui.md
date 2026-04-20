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
    "profile": {
      "name": "Sharpy Demo",
      "tier": "test"
    },
    "tags": ["one", "two", "three"]
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
- SharpOMatic rebuilds `input.chat` from that incoming history for the current run only
- `threadId` remains required AG-UI metadata, but it does not create a SharpOMatic conversation

For conversation-enabled workflows:

- the first request for a `(workflow, threadId)` pair starts a SharpOMatic conversation using `threadId` as the real `conversationId`
- later requests with the same `threadId` continue or resume that conversation
- the first request can initialize `input.chat` from the incoming AG-UI messages
- later requests must be incremental only and send only new messages that should be appended to the end of chat history
- if a later request resends a previously seen AG-UI message id, SharpOMatic fails the run with `RUN_ERROR`

Because conversation identifiers are strings, your AG-UI client can use any stable identifier that fits your application.

## Workflow context values

The AG-UI controller does not dump the entire request into workflow context.
Instead it maps a focused subset into `agent`:

- `agent.latestUserMessage`: the final item in `messages`, but only when that item is a user text message
- `agent.latestToolResult`: the final item in `messages`, but only when that item is a tool result message. Its `content` stays as the original string, and if that string is non-empty JSON then SharpOMatic also stores the parsed payload in `agent.latestToolResult.value`.
- `agent.messages`: the full incoming `messages` array
- `agent.state`: the incoming AG-UI `state`
- `agent.context`: the incoming AG-UI `context`

These values are preserved as structured JSON-compatible data.
For example, `agent.state` remains an object or array tree inside SharpOMatic context rather than becoming one large JSON string.
On each AG-UI start or resume, SharpOMatic updates `agent`.
If `agent` already exists in workflow context, the incoming AG-UI `agent` object replaces it entirely.

## Chat history conversion

SharpOMatic also converts AG-UI messages into provider-neutral `ChatMessage` objects and stores them at `input.chat`.
This lets `ModelCall` nodes consume AG-UI history without requiring a dedicated helper node.

Recommended `ModelCall` setup:

- `ChatInputPath = "input.chat"`
- `ChatOutputPath = "input.chat"` for conversation-enabled workflows that want persisted replay on later turns

Supported conversion rules in this version:

- `system` -> `ChatRole.System`
- `developer` -> `ChatRole.System`
- `user` -> `ChatRole.User`
- `assistant` text and `toolCalls` -> `ChatRole.Assistant`
- `tool` results -> `ChatRole.Tool`

SharpOMatic ignores AG-UI `reasoning` and `activity` messages when building `input.chat`.
Unsupported multimodal or non-string message `content` is rejected with `RUN_ERROR`.

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
It can also mark handled frontend tool-call stream events with `HideFromReply` so future AG-UI replay does not ask the same frontend question again.

## SSE behavior

The endpoint streams AG-UI SSE events from SharpOMatic workflow stream events:

- `RUN_STARTED` is emitted after the workflow turn starts
- text stream events become `TEXT_MESSAGE_START`, `TEXT_MESSAGE_CONTENT`, and `TEXT_MESSAGE_END`
- step stream events become `STEP_STARTED` and `STEP_FINISHED`
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

## Silent replay for incoming user messages

AG-UI clients already know about the incoming user message they just submitted.
If your workflow also wants that message recorded in SharpOMatic stream history, add it through a code node with `silent: true` so the chat client does not render the same user message twice.

```csharp
var latestUserMessage = Context.Get<ContextObject>("agent.latestUserMessage");
var messageId = latestUserMessage.Get<string>("id");
var text = latestUserMessage.Get<string>("content");

await Events.AddTextMessageAsync(StreamMessageRole.User, messageId, text, silent: true);
```

The `silent` flag only affects the live AG-UI SSE output for the current run.
The stored run or conversation stream history still contains the event, and no persisted stream-event field is added for the flag.

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

Use `AddStepStartAsync` and `AddStepEndAsync` when the frontend should render simple AG-UI step lifecycle markers:

```csharp
await Events.AddStepStartAsync("Search");
await Events.AddStepEndAsync("Search");
```
