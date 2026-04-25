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
- later AG-UI calls must be incremental only and send only the new message
- the controller exposes only the latest incoming AG-UI message at `agent.messages` and does not append it into `input.chat`

## Workflow context

The controller maps selected request data into workflow context under `agent`:

- `agent.latestUserMessage`: the final item in `messages`, but only when that item is a user text message
- `agent.latestToolResult`: the final item in `messages`, but only when that item is a tool result message. Its `content` stays as the original string, and if that string is non-empty JSON then SharpOMatic also stores the parsed payload in `agent.latestToolResult.value`.
- `agent.messages`: for non-conversation workflows, the full incoming AG-UI `messages` array; for conversation-enabled workflows, only the latest incoming message
- `agent.state`: the incoming AG-UI `state` value
- `agent.context`: the incoming AG-UI `context` value
- `agent._hidden.state`: a hidden deep copy of the incoming AG-UI `state`, used as the baseline for `AddStateSyncAsync()` and the `State Sync` node

These values are passed through as structured JSON-compatible data, not as a single raw JSON string.
On each AG-UI start or resume, SharpOMatic updates the `agent` object.
If the workflow context already contains `agent`, the incoming AG-UI `agent` object replaces it entirely.

For non-conversation workflows, the controller converts supported AG-UI messages into provider-neutral `ChatMessage` entries at `input.chat` for the current run.
For conversation workflows, `input.chat` is canonical model history owned by workflow nodes.
Use it as the recommended source for `ModelCall.ChatInputPath`, and set `ModelCall.ChatOutputPath` to `input.chat` when model responses should be replayed across turns.
Incoming user text is stored as silent stream history for conversation turns, but it is not appended to `input.chat`.
Frontend tool results are persisted by the **Frontend Tool Call** node according to its chat persistence setting, which defaults to `None` for new frontend tool-call nodes.

Supported `input.chat` conversion in this version:

- `system` -> `ChatRole.System`
- `developer` -> `ChatRole.System`
- `user` -> `ChatRole.User`, except the latest user text message when it is exposed as `agent.latestUserMessage`
- `assistant` text and tool calls -> `ChatRole.Assistant`
- `tool` results -> `ChatRole.Tool`

AG-UI `reasoning` and `activity` messages are ignored for chat conversion in this version.
Unsupported multimodal or non-string message content is rejected with `RUN_ERROR`.

SharpOMatic also includes protocol-aware workflow nodes for common AG-UI patterns:

- **Frontend Tool Call** for frontend-driven tool results that suspend and later resume the workflow
- **Backend Tool Call** for tool-call rendering when the workflow already knows the result
- **Activity Sync** and **State Sync** for structured activity or state updates
- **Step Start** and **Step End** for simple progress markers

Use the dedicated node and AG-UI docs for the detailed behavior of each node.

For conversation turns, SharpOMatic automatically stores `agent.latestUserMessage` as user text stream events before workflow nodes run.
Those events are marked with the transient `silent` flag for the current live AG-UI SSE stream, so the caller does not render its submitted message twice.
The underlying stream events are still persisted in SharpOMatic history, and the flag itself is not stored in the database.

Code nodes can also emit AG-UI tool-call, activity, state, step, and custom events through the `Events.Add*` helpers.
For the higher-level activity/state sync helpers and the full helper surface, see the AG-UI and Code Node docs.
