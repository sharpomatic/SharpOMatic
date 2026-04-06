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
- resume input can either be empty or merge a `ContextObject` into the current node context
- execution then continues to downstream nodes

## Notes

- The suspend node only makes sense in conversation-enabled workflows.
- Suspended turns appear in the workflow editor with a `Resume` tab in the trace panel.
- Resume can be done without extra input, or with JSON that is converted into a context object and merged before continuing.
- A completed conversation can still start another turn later, but that new turn starts from the **Start** node rather than resuming the suspend node.
