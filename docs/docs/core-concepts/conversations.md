---
title: Conversations
sidebar_position: 2
---

Conversations allow a workflow to continue across multiple turns instead of treating every run as a one-shot execution.
They are built on top of persisted workflow context, conversation checkpoints, and the suspend/resume execution model.

## What A Conversation Is

A conversation is identified by:

- `workflowId`
- `conversationId`

That pair represents one persisted multi-turn workflow instance.
Each turn creates a new `Run`, while the conversation keeps the shared state and checkpoint needed for later turns.

## Conversation Workflow Behavior

When a workflow is conversation-enabled:

- A new conversation starts from the **Start** node.
- A completed conversation can start another turn from the **Start** node again, using the saved conversation context from the previous turn.
- A suspended conversation resumes from the waiting continuation point instead of the **Start** node.
- Each turn is still stored as a normal run with run history, traces, and assets.
- SharpOMatic does not lock a conversation while a turn is running, so callers can start or resume the same conversation in parallel.

## Suspend And Resume

The **Suspend** node is the built-in way to pause a conversation and wait for another turn.

At suspension time SharpOMatic stores:

- the current conversation status
- the context at the suspension point
- the workflow checkpoint
- any workflow snapshots needed to continue later

On the next turn, the engine can:

- continue without any additional input
- merge extra context into the resume operation

This is what powers the editor's `Resume` tab for suspended conversations.

## Completed Conversations Can Continue

Conversation turns are not limited to suspended workflows.
If the latest turn completed successfully, you can still run the conversation again.
In that case SharpOMatic starts a new turn from the **Start** node and uses the output context from the previous turn as the base context for the next one.

This makes it possible to model multi-turn chat, review cycles, or iterative workflows where each turn builds on the previous result.

## Editor Experience

When you open a conversation-enabled workflow in the editor:

- the latest conversation is loaded automatically
- the trace panel shows per-turn trace history
- conversation-scoped assets are shown separately from run-scoped assets
- the `Resume` tab appears when the latest conversation turn is resumable

For suspended turns, the `Input` tab is hidden and `Resume` becomes the first tab.
For completed conversation turns, `Resume` is still available, but it appears after `Input` because the next turn restarts from the **Start** node.

## Programmatic APIs

Use the conversation methods on `IEngineService`:

- `StartOrResumeConversationAndWait`
- `StartOrResumeConversationAndNotify`
- `StartOrResumeConversationSynchronously`

These methods accept:

- `workflowId`
- `conversationId`
- optional `resumeInput`
- optional `inputEntries`

See [Running Workflows](../programmatic/run-workflow.md) for examples.

## Constraints

- Only workflows marked as conversation-enabled can use conversation APIs.
- Parallel turns for the same conversation are allowed. Applications that need stricter ordering should serialize calls before invoking SharpOMatic.
- Suspended conversations cannot accept start input entries on resume.
- Evaluations do not support conversation workflows because they cannot answer suspend events during row execution.

If you need conversations inside your application, run them directly through the workflow editor or the conversation methods in `IEngineService`, not through evaluations.
