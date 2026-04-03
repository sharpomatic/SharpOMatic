# Conversations

## Purpose

This document defines the generic suspend/resume workflow feature.

The feature is intentionally protocol-agnostic. It is not specific to AG-UI or chatbots, even though chatbot conversations are the first expected use case.

The core idea is that a workflow can:

- execute normally
- suspend at a safe checkpoint
- persist enough state to continue later
- resume from a single continuation point
- repeat this across multiple turns

AG-UI can then be built later as one API/protocol layer on top of this capability.

## Core Concept

The term `conversation` will be used for this feature.

A conversation represents a persisted, resumable workflow instance. It is identified by:

- `workflowId`
- `conversationId`

The pair `workflowId + conversationId` maps 1:1 to one persisted conversation.

## Main Requirements

- A workflow may be started as a conversation rather than as a one-shot run.
- If no matching conversation exists, execution starts from the workflow start node.
- If a matching conversation exists, execution resumes from the persisted checkpoint.
- A conversation may span an unlimited number of turns.
- Each turn creates a new `Run` record.
- There is one persisted conversation record for the full multi-turn interaction.
- Only one execution may run for a conversation at a time.
- This single-execution rule must hold across multiple backend instances.
- The implementation must be compatible with Entity Framework Core and the backing database.

## Suspension Model

- Suspension is a core engine feature, not an AG-UI-specific feature.
- Suspension must only occur after all runnable work has completed.
- The engine must never suspend while runnable nodes still exist elsewhere in the workflow.
- At the point of suspension there must be exactly one valid continuation point.
- No more than one node may be waiting to resume at the same time.
- If a second node attempts to become the resume point while one already exists, that is an error and the turn fails.

## Resume Model

- The start node behaves differently from all other nodes.
- On a new turn after a previously completed conversation, execution restarts from the start node.
- The incoming user text is injected into workflow context at `conversation.text`.
- Start node initialization runs normally on that turn, even if it overwrites parts of resumed context.
- For non-start nodes, resume is node-specific.
- A resumable node executes normally the first time.
- If the node is configured to wait for a later response, it completes normal execution and marks the workflow for suspension.
- When the next turn arrives, the same node is invoked again through a new resume-specific override rather than through the normal execute path.
- That resume override receives the follow-up response and can decide how to transform it into context or other state.
- Once the resume override completes, execution continues to downstream nodes.

## Node-Level Decisions

- The wait-for-response behavior should be controlled by node configuration rather than by a dedicated suspend node.
- Any non-start node may potentially support resume behavior.
- Nodes that do not support resume should keep default non-resumable behavior.
- Validation should prevent invalid combinations such as a node marked as resumable without implementing the required resume behavior.
- A node may need a small suspended-state payload in addition to workflow context.
- This payload is intended for node-local checkpoint data such as prompt metadata, choice metadata, or correlation values required by resume behavior.

## Checkpoint Design

The checkpoint should be compact. It should not attempt to serialize the full in-memory execution graph.

This is possible because of the chosen suspension rule: the workflow only suspends once all runnable work has drained and only one continuation point remains.

The checkpoint likely needs:

- conversation identity
- workflow identity
- conversation status
- current workflow context
- resume node identifier, nullable
- resume mode, such as `StartNode` or `ResumeNode`
- optional node suspension payload/state
- last run metadata
- concurrency metadata

The checkpoint should not need:

- full queue contents
- multiple waiting nodes
- active thread snapshots
- in-flight branch snapshots
- executor stack frames

## Branching Rules

- Branching workflows may continue executing after one branch has identified a future resume point.
- The workflow only suspends after all other runnable branches have completed.
- The model assumes that by suspension time there is only one legal continuation point left.
- If future requirements introduce multiple pending continuation points, the current compact checkpoint design will no longer be sufficient.

## Persistence Model

Current agreed direction:

- one conversation record for the overall resumable workflow instance
- one run record per turn
- a compact checkpoint associated with the conversation

Open design choice:

- store checkpoint fields directly on the conversation record
- or split them into `Conversation` and `ConversationCheckpoint`

The latter is likely cleaner if checkpoint state becomes more complex.

## Concurrency Model

- A conversation must not be executed concurrently by two requests.
- This must work even when multiple backend servers are running.
- The expected direction is a database-backed lease/lock model implemented through EF Core and database updates.
- The exact schema and lock strategy remain design work.

## API Direction

- New API endpoints should be added for starting/resuming conversation workflows.
- The exact number and shape of those APIs will be defined during the design phase.
- The conversation capability itself should remain independent of any particular client protocol.

## Relationship To AG-UI

- Conversations are the generic engine capability.
- AG-UI is a future protocol/API layer that can drive conversations.
- Chatbot behavior should not be hardcoded into the core conversation engine beyond the agreed `conversation.text` input convention for that specific integration path.

## Open Questions

- Should checkpoint state live on one table or be split across conversation and checkpoint tables?
- What exact statuses should a conversation support?
- What exact node contract should be introduced for normal execution vs resume execution?
- What validation rules are needed in the designer for resumable nodes?
- How should node-specific suspended payloads be serialized and versioned?
