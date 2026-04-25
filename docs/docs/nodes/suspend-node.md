---
title: Suspend Node
sidebar_position: 8
---

The **Suspend** node pauses a conversation-enabled workflow and creates a checkpoint so the workflow can continue on a later turn.

## When To Use It

Use **Suspend** when a workflow needs to wait for something that will arrive later, for example:

- a user reply in a chat-like workflow
- a review or approval decision
- a later payload that should be merged into workflow context

## Behavior

On the first pass, the node does not continue to downstream nodes immediately.
Instead it marks the current turn as suspended and stores the checkpoint needed for the next turn.

When the conversation is resumed:

- the same continuation point is invoked again
- resume input can be empty, merge a `ContextObject` into the current node context, or update the AG-UI `agent` context
- execution then continues to downstream nodes

## Resume Input

The resume payload controls what is available to downstream nodes:

- empty resume: continue from the checkpoint without changing context
- context merge resume: overwrite or add the supplied context values, then continue
- AG-UI conversation resume: replace the `agent` object with the latest mapped AG-UI request values, then continue

For AG-UI user replies, downstream nodes should read the current user text from `agent.latestUserMessage.content`.
For AG-UI frontend tool results, downstream nodes can read `agent.latestToolResult`, and a waiting **Frontend Tool Call** node will validate the expected `toolCallId`.

## Messages And Stream Events

The Suspend node does not create `ChatMessage` entries, does not update `input.chat`, and does not emit stream events.
It only records the checkpoint and later continues from it.

During an AG-UI conversation resume with a user message, SharpOMatic stores the incoming user text as silent stream history before the workflow nodes continue.
That makes refreshed conversation history complete, but it still does not append the user text to `input.chat`.

During an AG-UI resume with a frontend tool result, the suspend/resume mechanism does not create a `TOOL_CALL_RESULT` event and does not append the result to `input.chat`.
The waiting **Frontend Tool Call** node creates the result stream event and any optional chat messages when the incoming `tool` message matches its pending `toolCallId`.

## Notes

- The suspend node only makes sense in conversation-enabled workflows.
- Suspended turns appear in the workflow editor with a `Resume` tab in the trace panel.
- Resume can be done without extra input, or with JSON that is converted into a context object and merged before continuing.
- A completed conversation can still start another turn later, but that new turn starts from the **Start** node rather than resuming the suspend node.
