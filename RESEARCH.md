# Workflow Lifecycle Research

## Lifecycle

- A workflow run starts from the editor UI: the workflow page calls `WorkflowService.run()`, which POSTs the current start-node input snapshot to `/api/workflow/run/{id}` and resets the live run state in the client. See [workflow.component.ts](C:/code/SharpOMatic/src/SharpOMatic.FrontEnd/src/app/pages/workflow/components/workflow.component.ts#L125), [workflow.service.ts](C:/code/SharpOMatic/src/SharpOMatic.FrontEnd/src/app/pages/workflow/services/workflow.service.ts#L179), and [server.repository.service.ts](C:/code/SharpOMatic/src/SharpOMatic.FrontEnd/src/app/services/server.repository.service.ts#L136).
- The API controller creates a persisted `Run` with `RunStatus.Created` and `NeedsEditorEvents = true`, then starts it asynchronously. See [WorkflowController.cs](C:/code/SharpOMatic/src/SharpOMatic.Editor/Controllers/WorkflowController.cs#L64), [EngineService.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Services/EngineService.cs#L38), and [Run.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Repository/Run.cs#L5).
- `EngineService.StartRunInternal()` applies input entries into the initial context, loads the workflow, creates a scoped `ProcessContext`, wraps the workflow in a `WorkflowContext`, creates the first `ThreadContext`, persists the run update, and enqueues the single start node. See [EngineService.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Services/EngineService.cs#L113).

## Execution

- The engine registers a singleton queue/executor plus a hosted background service that continuously drains the queue. See [ServiceCollectionExtensions.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Services/ServiceCollectionExtensions.cs#L9) and [HostedNodeExecutionService.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Services/HostedNodeExecutionService.cs#L5).
- `NodeExecutionService.RunQueueAsync()` dequeues `(thread,node)` work and processes up to 5 in parallel. See [NodeExecutionService.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Services/NodeExecutionService.cs#L10) and [NodeQueueService.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Services/NodeQueueService.cs#L20).
- Every node runs through the `RunNode<T>` base: it creates a `Trace`, emits a running trace update, executes node logic, then emits final trace plus any `Information` records. See [RunNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/RunNode.cs#L38).
- `StartNode` is where the run first flips from `Created` to `Running`; it also optionally rebuilds the node context from configured initialization entries. See [StartNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/StartNode.cs#L6).
- Branching and nested execution are managed by contexts and thread counts, not by a single linear cursor. `FanOut`, `FanIn`, `Batch`, and `Gosub` all manipulate `ThreadContext.CurrentContext` and active thread counts. See [ProcessContext.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Contexts/ProcessContext.cs#L48), [FanOutNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/FanOutNode.cs#L3), [FanInNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/FanInNode.cs#L3), [BatchNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/BatchNode.cs#L3), and [GosubNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/GosubNode.cs#L3).

## Feedback And Finish

- Live feedback goes through `IProgressService`, not `IEngineNotification`. `ProcessContext.RunUpdated()` persists the `Run` and pushes `RunProgress`; `RunNode` pushes `TraceProgress` and `InformationsProgress`. See [ProcessContext.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Contexts/ProcessContext.cs#L98), [IProgressService.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Interfaces/IProgressService.cs#L3), and [RunNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/RunNode.cs#L57).
- The editor wires that progress path to SignalR. It registers `NotificationHub`, broadcasts all progress events with `Clients.All`, and the Angular client connects to `/notifications`, subscribes, then filters events locally by workflow/run id. See [SharpOMaticEditorExtensions.cs](C:/code/SharpOMatic/src/SharpOMatic.Editor/SharpOMaticEditorExtensions.cs#L24), [ProgressService.cs](C:/code/SharpOMatic/src/SharpOMatic.Editor/Services/ProgressService.cs#L5), [signalr.service.ts](C:/code/SharpOMatic/src/SharpOMatic.FrontEnd/src/app/services/signalr.service.ts#L27), and [workflow.service.ts](C:/code/SharpOMatic/src/SharpOMatic.FrontEnd/src/app/pages/workflow/services/workflow.service.ts#L203).
- The frontend turns terminal `RunProgress` into user-visible success/error toasts and keeps loading persisted traces/assets over HTTP for detail views. See [workflow.service.ts](C:/code/SharpOMatic/src/SharpOMatic.FrontEnd/src/app/pages/workflow/services/workflow.service.ts#L227), [toast.service.ts](C:/code/SharpOMatic/src/SharpOMatic.FrontEnd/src/app/services/toast.service.ts#L24), [RunController.cs](C:/code/SharpOMatic/src/SharpOMatic.Editor/Controllers/RunController.cs#L10), [TraceController.cs](C:/code/SharpOMatic/src/SharpOMatic.Editor/Controllers/TraceController.cs#L5), and [InformationController.cs](C:/code/SharpOMatic/src/SharpOMatic.Editor/Controllers/InformationController.cs#L5).
- `InformationsProgress` is currently populated mainly by `ModelCallNode`, which converts assistant reasoning and tool calls into `Information` rows. See [ModelCallNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/ModelCallNode.cs#L68).
- A run finishes when `NodeExecutionService` sees the active thread count reach zero. `EndNode` only sets output/returns from gosub; final success/failure is stamped in `RunCompleted()`, which sets status/message/error/stopped time, defaults output if no end node wrote it, persists, prunes run history, invokes `IEngineNotification.RunCompleted`, and disposes the scope. See [EndNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/EndNode.cs#L39) and [NodeExecutionService.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Services/NodeExecutionService.cs#L131).
- `IEngineNotification` is a separate host hook for terminal completion and model connection overrides, not the editor’s live progress channel. See [IEngineNotification.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Interfaces/IEngineNotification.cs#L3) and [OpenAIModelCaller.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Services/OpenAIModelCaller.cs#L80).

## Planning Seams

- Start behavior: [WorkflowController.cs](C:/code/SharpOMatic/src/SharpOMatic.Editor/Controllers/WorkflowController.cs#L64), [EngineService.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Services/EngineService.cs#L113), [StartNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/StartNode.cs#L6)
- Live feedback: [ProcessContext.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Contexts/ProcessContext.cs#L98), [RunNode.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Nodes/RunNode.cs#L57), [ProgressService.cs](C:/code/SharpOMatic/src/SharpOMatic.Editor/Services/ProgressService.cs#L5), [workflow.service.ts](C:/code/SharpOMatic/src/SharpOMatic.FrontEnd/src/app/pages/workflow/services/workflow.service.ts#L227)
- Completion behavior: [NodeExecutionService.cs](C:/code/SharpOMatic/src/SharpOMatic.Engine/Services/NodeExecutionService.cs#L131)

The main architectural constraint for feature planning is that live editor notifications are currently global broadcasts and client-filtered. If the expected feature needs scoped, private, or explicitly targeted live updates, that part will require architectural change rather than a small patch.

## Interactive AG-UI Requirements

### Overview

- SharpOMatic must support a new interactive conversation workflow mode in addition to the current non-interactive run model.
- The target client is a custom React or Angular chatbot-style frontend.
- The frontend communicates with SharpOMatic using the AG-UI protocol.
- The implementation should target the stable AG-UI event model rather than the draft interrupt lifecycle.
- This interactive capability should use new conversation-oriented APIs rather than modifying the existing one-shot workflow run API directly.

### Conversation Identity And Persistence

- Interactive conversations are keyed by `workflowId + conversationId`.
- `conversationId` is a GUID and is globally unique.
- A given `workflowId + conversationId` pair maps 1:1 to a persisted conversation instance.
- If no conversation exists for the incoming pair, the backend creates a new conversation and starts the workflow from the start node.
- If a conversation already exists, the backend restores its suspended state and resumes execution.
- There should be one persisted conversation record for the overall conversation.
- Each user turn should create a new `Run` record.
- A conversation may span an indefinite number of turns.
- Conversation expiry/cleanup is out of scope for this phase.

### API And Transport

- The interactive execution API should be a new REST endpoint shape dedicated to conversation workflows.
- The exact number and final shape of the APIs will be decided during design.
- The primary execution call should be a single REST endpoint returning an SSE stream.
- The SSE stream should remain open while the workflow is actively executing.
- The SSE stream should close when the workflow either suspends, completes, or fails according to the stable AG-UI event model.

### User Input Handling

- Each incoming user message must always be written into standard workflow context at `conversation.text`.
- `conversation.text` should be available from the start node onward on a fresh turn.
- If execution resumes at a non-start node, that resumed node and any later nodes should also be able to read `conversation.text`.
- When a previously completed conversation is resumed for a new turn, execution should start again at the start node.
- In that case, start node initialization should run normally again, even if it overwrites parts of resumed context.

### AG-UI Events

- While the workflow executes, the backend must emit AG-UI-compatible events over SSE for live chatbot feedback.
- The exact raw emitted AG-UI event stream must be persisted as the source of truth.
- The persisted raw event stream is required for:
  - exact replay/debugging
  - rebuilding chatbot history after browser refresh
  - reconstructing conversation history for future UI queries
- The initial design should persist the raw AG-UI events only rather than also maintaining a derived/materialized chat history view.
- Some nodes are expected to emit AG-UI-specific low-level events directly.
- The `Model Call` node may also gain the ability to emit appropriate low-level AG-UI events.

### Execution And Suspension Model

- Interactive execution must support repeated suspend/resume cycles across many turns.
- A workflow may suspend in two broad ways:
  - normal workflow completion
  - explicit workflow suspension while waiting for user interaction
- Normal completion should preserve the finishing workflow state/context.
- On a later user message after normal completion, the workflow should start again from the start node using the resumed context.
- Explicit suspension may be implemented either as a dedicated suspend/wait node or as a flag/capability on existing nodes. That exact workflow design remains open.
- If one branch reaches a wait-for-user condition, other runnable branches should continue executing.
- The engine should only persist the suspended checkpoint once no runnable nodes remain anywhere in the workflow.
- No more than one node may be waiting for user input at the same time.
- If a second node attempts to enter a client-interaction wait state while another node is already waiting, that is an error and the workflow turn should fail.

### Checkpointing Model

- The suspended execution checkpoint should be a compact continuation record rather than a full execution graph snapshot.
- The compact checkpoint is feasible because suspension only happens after all runnable work has drained.
- At suspension time there must be exactly one legal continuation point.
- The checkpoint likely needs at least:
  - conversation identity
  - workflow identity
  - conversation status
  - current workflow context
  - a nullable resume node identifier
  - a way to distinguish resume-at-start versus resume-at-node
  - small wait-state metadata if needed
  - last run metadata
  - concurrency metadata
- The checkpoint should not need:
  - full queue contents
  - multiple waiting nodes
  - active thread snapshots
  - in-flight branch snapshots
  - full executor stack state
- Execution-state persistence and AG-UI event-history persistence are separate concerns.

### Concurrency And Multi-Server Safety

- Only one execution may run for a given conversation at a time.
- Concurrent execution of the same conversation must not be allowed.
- This restriction must hold even when multiple backend server instances are running.
- The concurrency control mechanism should be implementable using Entity Framework Core and the backing database.

### Designer And Authoring

- The existing SharpOMatic designer should be reused.
- Workflows marked as interactive should expose additional AG-UI-related node types and/or configuration.
- Existing non-interactive workflow behavior should continue to work separately from the new conversation workflow path.
