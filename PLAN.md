# Plan

## Objectives
- Implement a hierarchical execution model that preserves existing behavior and supports every scenario in `VARIATIONS.md`.
- Add durable waits and resume capability without blocking the node execution worker pool.
- Keep fan-out/fan-in semantics, batch scheduling, gosub, and input handling consistent and composable.
- Ensure new features can be extended without reworking core scheduling and persistence.

## Scope and constraints
- Preserve current engine semantics (Start/End, tracing, node limits, merge behavior, failure handling).
- Preserve node behaviors validated by unit tests (Start init handling and validation, Edit ordering/validation, Switch default/skip semantics, End mappings and last-writer-wins, FanOut merge and unconnected output behavior).
- Keep global execution concurrency limits (current semaphore) for runnable nodes only.
- Treat input as a run-level barrier and serialize input prompts.
- Persist enough state to resume exactly where execution paused.

## Architecture: hierarchical execution contexts
### Core types
- `ExecutionContext` (base)
  - Fields: `ContextId`, `ParentId`, `PendingCount`, `Status`, `MergeStrategy`.
  - Methods: `OnNodeComplete(...)`, `Schedule(...)`, `IncrementPending(...)`, `DecrementPending(...)`.
- `WorkflowExecutionContext`
  - Root context for a run.
  - Owns connector maps, node/run limits, and run-level queues (input queue).
- `ThreadGroupContext`
  - Manages FanOut/FanIn and Batch coordination.
  - Tracks `ExpectedCount`, `ArrivedCount`, `MergedContext`, and any pending batch slices.
- `SubworkflowContext`
  - Tracks child run id, parent thread reference, input/output mappings, and completion callback.
- `SuspensionContext`
  - Tracks awaited external event, correlation id, and the suspended thread reference.

### Thread model changes
- Extend `ThreadContext` to reference `ExecutionContext` (id or direct reference).
- Add a helper to clone `ThreadContext.NodeContext` for child threads.
- Remove fan-out state from `ThreadContext` (no `FanOutCount`/`FanInArrived`/`FanInId`/`FanInMergedContext`).

### Scheduling model
- `NodeExecutionService` remains the runner for executable nodes.
- After each node finishes, delegate to the current `ExecutionContext` to decide:
  - Whether to enqueue follow-on nodes.
  - Whether the thread is complete, suspended, or waiting for external completion.
- External waits (gosub, input) never block the worker pool.
- Pending/suspended work counts must be included in completion checks.

## Node behavior updates
### New node types (schema + UI)
- Add `NodeType.Gosub` and `NodeType.Input`.
- Add `GosubNodeEntity` with `WorkflowId` (required) and `InputNodeEntity` with no fields.
- Update JSON converters and frontend node factories/snapshots/dialogs to support both nodes.

### FanOut/FanIn
- FanOut creates a `ThreadGroupContext` with `ExpectedCount = outputs.Length`.
- Each child thread references the group context; parent thread becomes the group owner.
- FanIn uses the group context to merge `output` values and release the parent only after all branches arrive.
- Merge strategy stays "merge output only" to match existing behavior.
- Ignore unconnected fan-out outputs; if none are connected, the fan-out completes immediately.
- If a fan-out has no downstream fan-in, the last completed branch context wins.
- Preserve current output merge rules (single scalar stays scalar, multiple outputs aggregate into a list, list + scalar merges into a list, nested `output.*` keys merge by object path).

### Batch
- Batch creates a `ThreadGroupContext` with `ProcessNode` and `ContinueNode`.
- Build batch slices from `InputArrayPath`:
  - `BatchSize == 0` => one slice with the full list.
  - Empty list => skip process, continue once.
- Spawn up to `ParallelBatches` child threads with:
  - Cloned context and `InputArrayPath` replaced by slice.
  - Optional batch metadata (`index`, `count`, `start`, `end`, `totalItems`).
- On each child completion, the group schedules the next slice or the continue node.

### GosubNode (WorkflowCallNode)
- `WorkflowId` identifies the child workflow to run.
- Start a child workflow run with mapped inputs (reuse `ContextEntryListEntity` patterns).
- Register a `SubworkflowContext` that:
  - Stores parent thread reference and continuation node.
  - Maps child output back into the parent context on completion.
- Do not await the child run inside the node; resume is triggered by completion callback.

### InputNode
- No fields initially; behavior is driven by the run-level input queue.
- On hit, enqueue a `SuspensionContext` into the run-level input queue.
- Only the head of the queue is "active" and can notify the user.
- If an active wait completes, resume its thread and activate the next queued wait.
- The run is only suspendable when no runnable threads remain (all are waiting/queued).

## Persistence and resume
### Run status
- Extend `RunStatus` with `Waiting` (active wait exists) and `Suspended` (no runnable threads).
- Keep `Running` for runs with at least one runnable thread.

### Stored state
- Persist a run snapshot that includes:
  - All `ThreadContext` instances (active and waiting).
  - Execution context tree (workflow, group, subworkflow, suspension).
  - Pending batch slices and group counters.
  - Input queue (active + pending) and correlation ids.
  - Subworkflow links (parent thread + child run id).
- Use JSON serialization with existing converters to keep storage minimal.

### Resume flow
- External event arrives with `runId` + `correlationId`.
- Load snapshot, locate the `SuspensionContext`, apply payload to the waiting thread context.
- Mark the wait complete, update the input queue, and enqueue resumed work.
- If more waits exist, activate the next and notify the user.

## API and notification surface
- Add APIs to post input replies (correlation id + payload).
- Add APIs to query active waits (for UI prompts).
- Add notifications for:
  - Input requested (active wait created).
  - Input resumed (reply applied).
  - Run suspended/resumed.

## Backward compatibility
- Old runs (Created/Running/Success/Failed) remain valid.
- New statuses only applied to runs that use InputNode or other waits.
- Existing workflows without new nodes behave as today.

## Validation against `VARIATIONS.md`
- Scenarios 1-4: FanOut/FanIn behavior preserved; merge logic unchanged.
- Scenarios 5-9: Batch scheduling, parallel caps, empty list, and nested groups are supported by `ThreadGroupContext`.
- Scenarios 10-11: Gosub nodes use `SubworkflowContext` and completion callbacks.
- Scenarios 12-16: Input queue, run-level suspension, and resume are handled by `WorkflowExecutionContext` + `SuspensionContext`.

## Implementation phases
1) Context infrastructure
   - Add context types, registry, and persistence model.
   - Update `ThreadContext` and `NodeExecutionService` to use contexts.
2) Add new node types
   - Add `GosubNodeEntity` (with `WorkflowId`) and `InputNodeEntity`.
   - Update `NodeType`, converters, and frontend nodes/dialogs.
3) FanOut/FanIn refactor
   - Move state into `ThreadGroupContext` and remove thread counters.
4) Batch implementation
   - Build slicing, parallel scheduling, and continue path orchestration.
5) Gosub implementation
   - Add `WorkflowCallNode`, mapping utilities, and completion callbacks.
6) Input + suspension
   - Add `InputNode`, queue handling, and run status transitions.
   - Implement persistence/resume endpoints and notifications.
7) Hardening
   - Add idempotency checks for external events.
   - Add guardrails for invalid connections and missing outputs.
8) Tests
   - Implement unit tests per `VARIATIONS.md`.
   - Add resume/suspend integration tests for input and gosub.
