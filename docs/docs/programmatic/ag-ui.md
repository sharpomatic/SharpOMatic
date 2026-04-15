---
title: AG-UI
sidebar_position: 2
---

SharpOMatic has optional AG-UI support for clients that want to start or resume conversation workflows over a single SSE endpoint.

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

SharpOMatic uses the AG-UI request to identify both the target workflow and the target conversation:

- `threadId` is required.
- `threadId` becomes the SharpOMatic `conversationId`.
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

## Conversation behavior

AG-UI `threadId` is the real SharpOMatic `conversationId`.
That means:

- the first request for a `(workflow, threadId)` pair starts a new conversation
- the same `threadId` on later requests continues a completed conversation as a new turn
- the same `threadId` on later requests resumes a suspended conversation from its suspend point

Because conversation identifiers are strings, your AG-UI client can use any stable identifier that fits your application.

## Workflow context values

The AG-UI controller does not dump the entire request into workflow context.
Instead it maps a focused subset into `agent`:

- `agent.latestUserMessage`: the last message in `messages` whose `role` is `user`
- `agent.allMessages`: the full incoming `messages` array
- `agent.state`: the incoming AG-UI `state`
- `agent.context`: the incoming AG-UI `context`

These values are preserved as structured JSON-compatible data.
For example, `agent.state` remains an object or array tree inside SharpOMatic context rather than becoming one large JSON string.

## SSE behavior

The endpoint streams AG-UI SSE events from SharpOMatic workflow stream events:

- `RUN_STARTED` is emitted after the workflow turn starts
- text stream events become `TEXT_MESSAGE_START`, `TEXT_MESSAGE_CONTENT`, and `TEXT_MESSAGE_END`
- visible reasoning stream events become `REASONING_START`, `REASONING_MESSAGE_START`, `REASONING_MESSAGE_CONTENT`, `REASONING_MESSAGE_END`, and `REASONING_END`
- tool-call stream events become `TOOL_CALL_START`, `TOOL_CALL_ARGS`, `TOOL_CALL_END`, and `TOOL_CALL_RESULT`
- `TOOL_CALL_RESULT` preserves both the tool result `messageId` and the linked `toolCallId`
- `TOOL_CALL_START` includes the tool name and can include `parentMessageId` when the underlying model output supplies it
- reasoning events use AG-UI-specific `messageId` values prefixed with `reason:` so they cannot collide with assistant text messages when a provider reuses one underlying response id for both
- tool result messages use AG-UI-specific `messageId` values prefixed with `tool:` so tool messages also stay distinct from assistant and reasoning messages, while the linked `toolCallId` remains unchanged
- when a model call runs in batch mode and the provider does not supply message ids, SharpOMatic synthesizes distinct assistant `messageId` values for each separate assistant text lifecycle, seeded from the stream sequence so they remain unique across conversation turns
- model-call nodes can suppress assistant text, reasoning, or tool-call stream categories, in which case the AG-UI endpoint simply emits fewer events for that run
- successful runs emit `RUN_FINISHED`
- suspended runs also emit `RUN_FINISHED`
- failed runs emit `RUN_ERROR`

The SSE request ends when the underlying workflow run finishes, suspends, or fails.
