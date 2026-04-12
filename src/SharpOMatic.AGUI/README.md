# SharpOMatic.AGUI

Optional SharpOMatic extension that exposes an AG-UI compatible endpoint for starting or resuming conversation workflows over SSE.

## Usage

Register the package in your ASP.NET Core host:

```csharp
builder.Services.AddSharpOMaticAgUi("sharpomatic/api/agui");
```

Choose any path that fits your host:

```csharp
builder.Services.AddSharpOMaticAgUi("integrations/chat/agui");
```

The package adds `POST` on the exact path you provide.

## Request requirements

- `threadId` is required and is used as the SharpOMatic conversation id.
- `forwardedProps` must contain exactly one of `workflowId` or `workflowName`.
- `workflowName` must match exactly one workflow.

The endpoint starts or resumes the matching SharpOMatic conversation workflow and streams SSE events translated from workflow stream events until the run completes, suspends, or fails.

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
      "workflowId": "384382a6-afd0-4627-af6d-df7c0f2853ec"
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
