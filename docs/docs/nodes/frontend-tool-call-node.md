---
title: Frontend Tool Call Node
sidebar_position: 9
---

The **Frontend Tool Call** node sends an AG-UI tool call to the frontend, suspends the conversation, and then resumes through one of two outputs on the next turn.

If the workflow already knows the tool result during the current run and does not need to suspend, use **Backend Tool Call** instead.

## When To Use It

Use **Frontend Tool Call** when the workflow needs a frontend action that should not always become durable model chat history, for example:

- asking the user for confirmation before a side effect
- collecting UI-only data from a browser control
- waiting for a frontend integration to finish and then branching

## Behavior

On the first pass, the node:

- generates a new internal `toolCallId`
- resolves the arguments from either a context path or fixed JSON
- emits AG-UI `TOOL_CALL_START`, `TOOL_CALL_ARGS`, and `TOOL_CALL_END`
- optionally writes an assistant function-call `ChatMessage` into `input.chat`
- suspends the conversation

On resume, the node is strict:

- `agent.messages` must contain exactly one incoming message
- that single message must be a JSON object with a non-empty `id`
- if the message includes `content`, it must be a string
- if that single valid message is a `tool` result with the expected `toolCallId`, the node emits `TOOL_CALL_RESULT` and follows `toolResult`
- any other single valid message follows `otherInput`

The AG-UI controller does not add the incoming tool-result message to `input.chat`.
The node owns any chat persistence for the frontend tool exchange.

## Outputs

- `toolResult`: the expected frontend tool result arrived
- `otherInput`: any other single incoming message arrived instead

The `otherInput` branch always abandons the pending frontend tool call and cleans up its temporary state.

## Settings

- `Tool Name`: the AG-UI tool name sent to the frontend
- `Arguments Mode`
- `Arguments Path`: used when arguments come from workflow context
- `Arguments JSON`: used when arguments are fixed JSON
- `Result Output Path`: where the returned tool result is stored
- `Chat Persistence`
- `Hide From Stream`

When **Arguments Mode** is **Context Path**, the context value is serialized to JSON and sent as the tool-call arguments.
When **Arguments Mode** is **Fixed JSON**, the configured value must be valid JSON.
If **Chat Persistence** is enabled, the resolved arguments must decode to a JSON object so SharpOMatic can create a provider-neutral function-call chat message.

## Result Output

When the expected tool result is received:

- if the returned `content` is valid JSON, the parsed JSON structure is written to `Result Output Path`
- otherwise the raw text is written there
- the raw returned `content` is used as the tool result text if the node is configured to persist the result into `input.chat`

## Chat Persistence

- `None`: do not keep the frontend tool call or result in `input.chat`
- `Function Call Only`: keep only the assistant function call
- `Function Call And Result`: keep both the assistant function call and the tool result

New Frontend Tool Call nodes default to `None`.
Use the other modes only when the frontend tool exchange should become durable model chat history.

If `otherInput` is taken, the node removes any chat entries that it created for the frontend tool call.
When `toolResult` is taken, any incoming frontend tool-result message is treated as transient resume input and replaced by the node's configured canonical chat persistence.
The AG-UI controller does not append frontend tool results to conversation `input.chat`; this node owns that persistence so backend model-call stream events cannot be replayed as duplicate tool results.

If `input.chat` does not exist and chat persistence is enabled, the node creates it before adding the configured messages.
If chat persistence is `None`, the tool call can still appear in AG-UI stream history, but it will not be sent to later `ModelCall` nodes through `input.chat`.

## Stream Events

On the first pass, the node stores tool-call stream events for AG-UI display:

- `TOOL_CALL_START`
- `TOOL_CALL_ARGS`
- `TOOL_CALL_END`

On a matching tool-result resume, the node stores:

- `TOOL_CALL_RESULT`

These stream events are independent from `input.chat`.
Changing **Chat Persistence** changes model chat history only; it does not stop the node from emitting the AG-UI tool-call lifecycle.

## Replay Visibility

Pending frontend tool calls stay visible in AG-UI replay until they are handled.

- if **Hide From Stream** is enabled and `toolResult` is taken, the node marks its tool-call and tool-result stream events as hidden from future replay
- if `otherInput` is taken, the node always hides its tool-call stream events from future replay

The underlying stream events are still stored in the database.

## Notes

- The node only works in conversation-enabled workflows.
- Reaching this node in a normal non-conversation workflow fails the run.
- A common AG-UI pattern is `ModelCall -> Frontend Tool Call -> ModelCall`, with `ModelCall.ChatInputPath` and `ChatOutputPath` set to `input.chat` when you want persisted replay.
