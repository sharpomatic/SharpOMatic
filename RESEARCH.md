# Research

## Current engine behavior (baseline)
- `EngineService.StartRunInternal` creates a `RunContext`, wraps it in a `ThreadContext`, and enqueues the Start node. See `src/SharpOMatic.Engine/Services/EngineService.cs`.
- `NodeExecutionService` dequeues `(ThreadContext, NodeEntity)` and runs node runners. It caps concurrent node execution with `SemaphoreSlim(5)`. See `src/SharpOMatic.Engine/Services/NodeExecutionService.cs`.
- Thread completion is tracked by `RunContext.RunningThreadCount` and `UpdateThreadCount(nextNodes.Count - 1)`; when it hits 0, the run is completed. See `src/SharpOMatic.Engine/Contexts/RunContext.cs`.
- `RunContext` builds connector maps for output traversal and includes `MergeContexts` for merging output data. See `src/SharpOMatic.Engine/Contexts/RunContext.cs`.

## FanOut/FanIn today
- FanOut clones the parent `NodeContext`, creates a new `ThreadContext` per output, and sets fan-out counters on the parent thread. See `src/SharpOMatic.Engine/Nodes/FanOutNode.cs`.
- FanIn uses the parent thread counters, merges only the `output` key, and releases the parent when all branches arrive. See `src/SharpOMatic.Engine/Nodes/FanInNode.cs`.
- FanOut ignores unconnected outputs; if no outputs are connected, the fan-out completes immediately and preserves the parent context.
- FanIn requires all arrivals to originate from the same fan-out; otherwise the run fails.
- FanOut without a downstream FanIn uses the last completed branch context (last writer wins).
- Output merge rules: multiple branch outputs become a list, a single branch output stays scalar, list + scalar merges into a list, and nested `output.*` keys merge by object path.

## Batch node today
- `BatchNodeEntity` defines `InputArrayPath`, `BatchSize`, and `ParallelBatches`; the frontend defines outputs `continue` and `process`. See `src/SharpOMatic.Engine/Entities/Definitions/BatchNodeEntity.cs` and `src/SharpOMatic.FrontEnd/src/app/entities/definitions/batch-node.entity.ts`.
- Engine `BatchNode` currently only validates input and returns the first output. See `src/SharpOMatic.Engine/Nodes/BatchNode.cs`.

## Other node behaviors (unit-test baselines)
- Start: workflow must have exactly one Start node. Without init entries, Start preserves the incoming context. With init entries enabled, Start builds a new context from the configured entries (null counts as provided) and drops other input values; if no entries are configured, the context is cleared. Entry paths must be non-empty, list index paths are not allowed, and parsing/validation errors fail the run.
- End: when `ApplyMappings` is false, End is a no-op. When `ApplyMappings` is true, output context includes only mapped values; missing inputs are skipped and empty mappings clear context. Empty input/output paths fail; invalid output paths are skipped. In concurrent branches without fan-in (or multiple End nodes), the last completed End wins.
- Switch: choices evaluate in order; the last choice is the default (empty expression) and must have a connected output. If a matched choice has no connected output, it is skipped and evaluation continues. Non-bool or null results, compile errors, and runtime errors fail the run.
- Edit: delete and upsert entries apply in order; delete on missing paths is a no-op; empty delete paths fail. Upsert requires non-empty, valid paths (no list indexes) and uses the same parsing/error semantics as Start inputs, including expression and asset validation.
- Code: empty code is a no-op, async is allowed, and project APIs can be invoked. Compilation or runtime errors fail the run.

## External wait patterns in other engines
- Durable Functions waits for external events and can unload while waiting; events are at-least-once, so de-duplication is recommended. See `https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-external-events`.
- Durable Functions human interaction pattern combines external events with durable timers for escalation. See `https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-overview#application-patterns`.
- Other engines model durable wait states (Temporal signals, Step Functions task tokens, BPMN user tasks) as persisted tokens with correlation ids.

## Requirements from discussion
### FanOut/FanIn
- Keep fan-out/fan-in semantics but move state out of `ThreadContext` to support nesting and future nodes.
- Merge strategy remains "merge `output` only" unless explicitly extended.

### Batch
- Create batches from `InputArrayPath` (a `ContextList`).
- `BatchSize == 0` means one batch containing the full list.
- `ParallelBatches` caps concurrent batch threads.
- Each batch thread receives a cloned context with `InputArrayPath` set to its slice and optional metadata (batch index/count/start/end/total).
- Continue output runs only after all batch work completes.

### Gosub (workflow call)
- A gosub node starts a child workflow run and maps child output into the parent context.
- It must not block a worker slot while waiting (avoid deadlock with the global semaphore).
- Parent continuation must be triggered by a completion callback from the child run.

### UserInputNode and suspension
- UserInputNode is run-scoped for suspension decisions (not just a group).
- Only one user input can be active at a time. Additional UserInputNodes are queued and must not notify the user until they reach the front of the queue.
- Scenario A: One branch waits for input while another branch continues; if input arrives before the other branch completes, resume without persisting.
- Scenario B: All branches are waiting; the run is persisted and unloaded. When input arrives, rehydrate and resume the active wait while keeping queued waits suspended.
- External events must use correlation ids and be idempotent (at-least-once delivery).

### Persistence/resume
- Persist enough state to resume exactly where execution paused:
  - Active threads, suspended threads, and their `ThreadContext`.
  - Execution context tree (workflow, group, subworkflow, suspension).
  - Group queues (batch slices), user input queue, and awaited events.
  - Subworkflow links (parent thread + child run id).
- A run is suspendable only when no runnable threads remain (all are waiting/queued).

## Selected execution model: hierarchical contexts
- Use a hierarchical execution model to isolate concerns and support nesting:
  - `WorkflowExecutionContext`: root for a run; owns connector maps, limits, and run-level queues (user input queue).
  - `ThreadGroupContext`: fan-out/fan-in and batch coordination with expected/arrived counts and merged context.
  - `SubworkflowContext`: tracks child run id, input/output mapping, and parent continuation.
  - `SuspensionContext`: tracks an awaited external event and the thread to resume.
- This model avoids storing fan-out state directly on threads, supports nested groups, and enables durable waits and gosub without blocking worker slots.
