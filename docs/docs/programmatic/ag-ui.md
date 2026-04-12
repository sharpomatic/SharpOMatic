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
builder.Services.AddSharpOMaticAgUi("sharpomatic/api/agui");
```

The string you provide is the exact `POST` route added to the host.
It is separate from `MapSharpOMaticEditor("/sharpomatic/editor")`, which only exposes the editor UI.

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
      "workflowId": "384382a6-afd0-4627-af6d-df7c0f2853ec"
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

- text stream events become `TEXT_MESSAGE_*`
- successful runs emit `RUN_FINISHED`
- suspended runs also emit `RUN_FINISHED`
- failed runs emit `RUN_ERROR`

The SSE request ends when the underlying workflow run finishes, suspends, or fails.
