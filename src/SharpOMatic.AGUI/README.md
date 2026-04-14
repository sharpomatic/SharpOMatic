# SharpOMatic.AGUI

Optional SharpOMatic extension that exposes an AG-UI compatible endpoint for starting or resuming conversation workflows over SSE.

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

- `threadId` is required and is used as the SharpOMatic conversation id.
- `forwardedProps` must contain exactly one of `workflowId` or `workflowName`.
- `workflowName` must match exactly one workflow.

The endpoint starts or resumes the matching SharpOMatic conversation workflow and streams SSE events translated from workflow stream events until the run completes, suspends, or fails.

## SSE event mapping

SharpOMatic translates workflow stream events into AG-UI SSE events:

- `TEXT_MESSAGE_START`, `TEXT_MESSAGE_CONTENT`, `TEXT_MESSAGE_END`
- `REASONING_START`, `REASONING_MESSAGE_START`, `REASONING_MESSAGE_CONTENT`, `REASONING_MESSAGE_END`, `REASONING_END`
- `TOOL_CALL_START`, `TOOL_CALL_ARGS`, `TOOL_CALL_END`, `TOOL_CALL_RESULT`
- `RUN_STARTED`, `RUN_FINISHED`, `RUN_ERROR`

`TOOL_CALL_RESULT` preserves both the result `messageId` and the linked `toolCallId`.
`TOOL_CALL_START` includes the tool name and can include `parentMessageId` when the model output supplies it.
When batch-mode model calls are replayed without provider message ids, SharpOMatic synthesizes separate assistant `messageId` values for each distinct assistant text lifecycle and seeds them from the stream sequence so they remain unique across conversation turns.

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

## Conversation identity

AG-UI `threadId` and SharpOMatic `conversationId` are the same value.
If the specified workflow already has a conversation with that id then SharpOMatic resumes or continues it.
If no conversation exists yet, SharpOMatic creates a new conversation with that id.

## Workflow context

The controller maps selected request data into workflow context under `agent`:

- `agent.latestUserMessage`: the last message in `messages` whose `role` is `user`
- `agent.allMessages`: the full incoming AG-UI `messages` array
- `agent.state`: the incoming AG-UI `state` value
- `agent.context`: the incoming AG-UI `context` value

These values are passed through as structured JSON-compatible data, not as a single raw JSON string.
