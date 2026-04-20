---
title: Backend Tool Call Node
sidebar_position: 10
---

The **Backend Tool Call** node emits a complete AG-UI tool-call lifecycle during the current run and then immediately continues through its normal output.

## When To Use It

Use **Backend Tool Call** when the workflow already knows both the tool arguments and the tool result and you want the frontend to render that exchange as a tool call, for example:

- showing a backend integration result in AG-UI tool-call form
- replaying a workflow-produced tool result back to an AG-UI client
- adding a durable tool-call exchange to `input.chat` without suspending the workflow

## Behavior

The node:

- generates a new internal `toolCallId`
- resolves arguments from either a context path or fixed JSON
- resolves result content from either a context path or fixed JSON
- emits `TOOL_CALL_START`, `TOOL_CALL_ARGS`, `TOOL_CALL_END`, and `TOOL_CALL_RESULT`
- optionally writes an assistant function-call and tool-result message into `input.chat`
- continues through its single output

Unlike **Frontend Tool Call**, this node does not suspend, does not branch, and does not wait for later input.

## Settings

- `Function Name`: the AG-UI tool name to emit
- `Arguments Mode`
- `Arguments Path`: used when arguments come from workflow context
- `Arguments JSON`: used when arguments are fixed JSON
- `Result Mode`
- `Result Path`: used when the result comes from workflow context
- `Result JSON`: used when the result is fixed JSON
- `Chat Persistence`

## Result Sources

For `Result Mode = Context Path`:

- string values are emitted as raw tool result text
- non-string values are serialized to JSON
- `null` becomes an empty result string

For `Result Mode = Fixed JSON`:

- the value must be valid JSON
- the configured JSON is emitted exactly as the tool result content

## Chat Persistence

- `None`: do not add anything to `input.chat`
- `Function Call Only`: add only the assistant function-call message
- `Function Call And Result`: add both the assistant function call and the tool result

When chat persistence is enabled, arguments must decode to a JSON object so SharpOMatic can create provider-neutral `FunctionCallContent`.

## Notes

- The node works in both conversation and non-conversation workflows.
- The emitted stream events do not use `parentMessageId` because the tool call is workflow-originated rather than model-originated.
- A common AG-UI pattern is `Code/Edit -> Backend Tool Call -> ModelCall` when the workflow wants to preserve a backend-generated tool exchange in `input.chat`.
