# Streams

## Purpose

This document defines the generic workflow output streaming feature.

The goal is to let workflows emit ordered output events while they are running, instead of only returning a final output context at the end of execution.

This feature is intentionally generic. It is not limited to chatbot text and is not AG-UI-specific.

Possible uses include:

- chatbot responses
- progress updates
- LLM thinking/tool activity
- data intended for files or external sinks
- artifacts or generated content
- future AG-UI message emission

## Core Concept

The workflow engine should support an append-only stream of emitted output events.

These events are distinct from workflow context.

- context is mutable execution state
- stream events are observable output history

The stream should be:

- ordered
- append-only
- persisted
- available live while execution is running
- queryable later for replay/history

## Main Requirements

- A workflow must be able to emit output incrementally during execution.
- Output must be available for live subscribers as it happens.
- The exact emitted output stream must be persisted.
- The persisted stream must be queryable later as historical output.
- The same persisted stream should support replay, debugging, and client reconstruction.
- This output mechanism should be reusable outside chatbot scenarios.
- The output stream feature should work independently of the conversation feature, although the two will often be used together.

## Separation Of Concerns

The engine should keep these concepts separate:

- workflow context
- workflow output stream
- workflow traces
- workflow informations

`Trace` and `Information` are execution/debugging telemetry.

The new output stream is a client-consumable product of workflow execution and should not be implemented by overloading traces or information records.

## Persistence Direction

The output stream should be persisted as raw emitted events and treated as the source of truth.

This is the same model chosen for future AG-UI event persistence:

- store the raw emitted stream
- use it for replay/debugging/history
- avoid introducing a second derived/materialized history model initially

Recommended persistence shape:

- one row per emitted event
- raw payload preserved
- enough indexed metadata to support filtering and ordering

Likely metadata fields:

- conversation id, nullable for non-conversation streams
- run id
- workflow id
- sequence number
- timestamp
- node id or trace id
- event type
- raw payload

## Live Delivery Model

- Stream events should be deliverable live as they are emitted.
- The engine should have a single write path:
  - persist the output event
  - notify live subscribers
- Historical queries should read from the persisted event log rather than from a separate in-memory channel.
- Later API/protocol layers such as SSE or AG-UI should adapt these core output events for transport.

## Event Model

The core output stream should not be modeled as chatbot messages or AG-UI events directly.

Instead, the core should expose a generic event model such as `WorkflowOutputEvent`.

This model should support:

- ordered emission
- type/category
- payload data
- source node metadata
- persistence
- live delivery

The event model should not be so loose that it becomes only arbitrary JSON with no useful contract.

Recommended direction:

- define a small set of built-in event categories
- allow extension for custom event types

Possible categories:

- `Text`
- `Data`
- `Progress`
- `Prompt`
- `Tool`
- `Artifact`
- `Custom`

These categories are provisional and should be validated during design.

## Relationship To AG-UI

- Streams are the generic engine-level output mechanism.
- AG-UI can later map workflow output events to AG-UI protocol messages.
- AG-UI-specific event emission should sit above this layer or adapt this layer, rather than replacing it.
- Some future nodes may emit AG-UI-specific messages, but the underlying engine capability should remain generic.

## Relationship To Conversations

- A workflow may emit streamed output even if it is not resumable.
- A workflow may be resumable even if it emits little or no streamed output.
- In multi-turn scenarios, persisted stream events become the historical record of what the client saw across turns.
- In conversation scenarios, the stream history is distinct from the checkpoint/state needed to resume execution.

## Node-Level Direction

- Nodes should have a standard engine mechanism for emitting output events.
- Model-call nodes are a likely early user of this feature because they already generate user-visible intermediate data such as reasoning or tool-call activity.
- Future node types may emit prompts, structured outputs, or custom events.
- Node authors should not need to know transport details such as SSE or AG-UI framing.

## Open Questions

- What exact built-in event categories should exist in the core stream model?
- How much typed metadata should each event store in columns versus only in raw payload JSON?
- Should output events be associated primarily with runs, conversations, or both?
- What APIs should expose live stream events versus historical event replay?
- Should there be a stream-specific service abstraction for emit/persist/subscribe?
