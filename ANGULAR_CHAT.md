# Angular Chat Notes

## Goal

Build a CopilotKit-style chat in Angular that can talk directly from the browser to an AG-UI endpoint, without requiring CopilotRuntime.

## Reusable Pieces

- `@ag-ui/client`
  - Lowest-level browser transport and protocol handling.
  - Handles AG-UI request bodies, SSE parsing, event application, and `HttpAgent`.
  - This is the main reusable layer for direct browser-to-AG-UI chat.

- `@copilotkit/core`
  - Framework-agnostic orchestration on top of AG-UI.
  - Adds:
    - `runAgent()` / `connectAgent()`
    - agent registry
    - frontend tool registration and execution
    - context injection
    - state/run tracking
    - runtime/proxied-runtime support

- `@copilotkitnext/angular`
  - Angular wrapper package already present in this repo.
  - Uses `@copilotkit/core` internally.

- Not reusable for Angular:
  - `@copilotkit/react-core`
  - `@copilotkit/react-ui`
  - other React-only hooks/components

## Direct AG-UI from Angular

Yes, Angular can do the same direct AG-UI pattern as the `sharpy` sample.

Use a plain `HttpAgent` and register it as a local agent in Angular config.

Conceptually:

```ts
provideCopilotKit({
  agents: {
    sharpy: new HttpAgent({
      url: "https://localhost:9001/agui",
    }),
  },
});
```

Then:

```html
<copilot-chat
  [agentId]="'sharpy'"
  [threadId]="'sharpy-demo-thread'"
></copilot-chat>
```

How to handle having one HttpAgent instance per threadId so that we can load the history and add it into the agent so it renders past history. Rather than
having one agent and then multiple copilot-chat instances.


What works:

- direct POST to the AG-UI endpoint
- SSE response streaming
- normal chat turns via `core.runAgent({ agent })`

Important caveat:

- The Angular chat does not do the same connect-on-mount behavior for a plain `HttpAgent` that the React chat does.
- It only calls `connectToAgent()` for CopilotKit-specific agents.
- For a plain direct `HttpAgent`, think of Angular chat as "run-only direct AG-UI chat".

## AG-UI Request Payload

The browser sends AG-UI `RunAgentInput`.

Useful fields:

- `threadId`
  - logical conversation key
- `runId`
  - unique ID for one execution
- `messages`
  - prior chat history being sent to the server
- `state`
  - arbitrary structured state
- `tools`
  - available frontend tools
- `context`
  - model-facing context entries
- `forwardedProps`
  - app-specific metadata for the server

Guidance:

- Use URL path or query string when workflow selection is part of server routing.
- Use `forwardedProps` for app metadata like workflow selection when staying on one endpoint.
- Avoid using `context` for routing unless the model should also see that value as prompt context.

## Workflow Selection Options

- URL path, e.g. `/agui/{workflowId}`
  - clean routing
  - best when workflows map to different server handlers

- query string, e.g. `/agui?workflowId=x`
  - simple, still routing-oriented

- header, e.g. `X-Workflow-Id`
  - good for gateway-level routing
  - hidden from the AG-UI body

- `forwardedProps.workflowId`
  - good for same-endpoint request metadata
  - structured and semantically appropriate

- `state.workflowId`
  - good when workflow should persist across turns in a thread

- avoid `context` for routing unless it is intentionally prompt-visible

## History Loading

There is no built-in "load old thread history into the Angular chat UI" API on the component itself.

The supported approach is to inject history into the underlying agent.

This works because:

- the chat renders from the agent store's `messages`
- the agent store updates from `onMessagesChanged`
- AG-UI agents expose:
  - `setMessages(...)`
  - `addMessages(...)`
  - `setState(...)`

### Recommended history flow

1. fetch saved thread history yourself
2. convert it into AG-UI `Message[]`
3. call:
   - `agent.setMessages(history)`
   - `agent.setState(savedState)` if needed
4. let future turns continue with normal `runAgent()`

## Message Format

AG-UI `Message` is a union, not a single flat type.

Main variants:

- `developer`
- `system`
- `assistant`
- `user`
- `tool`
- `activity`
- `reasoning`

Important shapes:

- assistant messages may include `toolCalls[]`
- tool results are separate `role: "tool"` messages
- reasoning is a separate `role: "reasoning"` message
- user content can be plain text or multimodal content parts

## Converting AG-UI Events to Messages

This is feasible and not especially hard for the common subset.

Common mappings:

- `TEXT_MESSAGE_START/CONTENT/END` -> assistant message
- `REASONING_MESSAGE_START/CONTENT/END` -> reasoning message
- `TOOL_CALL_START/ARGS/END` -> assistant `toolCalls[]`
- `TOOL_CALL_RESULT` -> tool message
- `MESSAGES_SNAPSHOT` -> full message replacement/merge

Practical recommendation:

- if you control persistence, store normalized `Message[]` plus optional `state`
- if you already store raw AG-UI events, replay or reduce them into `Message[]` before injecting them into the agent

For the current SharpOMatic demo event stream, conversion is easy because it only uses:

- reasoning
- tool calls
- text messages
- run lifecycle events

## Does Injected History Render?

Yes.

If you call `agent.setMessages(...)` or `agent.addMessages(...)`, those messages will render in the Angular chat.

Make sure the restored messages are valid AG-UI message objects:

- assistant messages with `toolCalls` where needed
- matching `tool` result messages
- reasoning messages as `role: "reasoning"`

## Recommended Angular Approach

For a pragmatic direct-browser implementation:

- use Angular chat UI
- use a plain `HttpAgent`
- use direct AG-UI POST/SSE to your endpoint
- load history yourself and inject it with `setMessages(...)`
- use `forwardedProps` or URL path for workflow selection

This gives a simple direct architecture without needing CopilotRuntime.
