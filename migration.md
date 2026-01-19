# Node Execution Rewrite - Migration Plan

## Purpose
Provide a detailed, step-by-step plan for migrating the engine from the current `RunContext` + `ThreadContext` model to the new hierarchical context model defined in `design.md`.

This plan assumes incremental delivery with compile checkpoints and a focus on preserving runtime behavior while the new contexts are introduced.

## Current Baseline (What Exists Today)
- `RunContext` combines:
  - Run-wide services and state (`ServiceScope`, repositories, progress services, tool/schema registries, script options, converters, `Run`, run limits, node counters).
  - Workflow graph state and traversal (`Workflow`, connector maps, `ResolveOutput`, `ResolveSingleOutput`).
  - Run-level helpers like `MergeContexts`.
- `ThreadContext` includes per-thread data and fan-out state (`NodeContext`, `ThreadId`, `FanOutCount`, `FanInId`, `FanInArrived`, `FanInMergedContext`, and `Parent`).
- Fan-in/out logic relies on `ThreadContext.Parent` and shared fan state.
- Execution pipeline (`NodeExecutionService`, `NodeQueueService`, `RunNode`) relies heavily on `RunContext`.

## Target Architecture (Design Summary)
See `design.md`. High-level goals:
- Split `RunContext` into `ProcessContext` (run-wide) and `WorkflowContext` (graph traversal).
- Introduce `FanOutInContext` and `BatchContext` for scoped execution.
- Keep `ThreadContext` as per-thread data holder and pointer to current execution context.
- All contexts can resolve `ProcessContext` by walking `Parent`.

## Migration Strategy
Incremental migration with short-lived compatibility shims to keep the build green between phases. Avoid a single "big bang" change.

## Phase 0 - Inventory and Guardrails
1. Add this document and keep it updated as the work progresses.
2. Capture the current set of runtime behaviors that must be preserved:
   - Fan-out produces N threads.
   - Fan-in merges outputs and re-joins correctly.
   - Batch node validates inputs and continues properly.
   - Run completion only triggers once and writes output context.
3. Identify all `RunContext` and `ThreadContext` usage:
   - `src/SharpOMatic.Engine/Contexts/RunContext.cs`
   - `src/SharpOMatic.Engine/Contexts/ThreadContext.cs`
   - `src/SharpOMatic.Engine/Nodes/*.cs`
   - `src/SharpOMatic.Engine/Services/*.cs`

## Phase 1 - Introduce New Context Types (No Behavior Change Yet)
Create new context classes and interfaces in `src/SharpOMatic.Engine/Contexts/`:
- `ProcessContext`
- `WorkflowContext`
- `FanOutInContext`
- `BatchContext`
- Update `ThreadContext` to include `CurrentContext` (and remove fan state later).

Minimal implementation goals:
- `ProcessContext` holds everything currently in `RunContext` (for now), so we can gradually move usage.
- `WorkflowContext` wraps graph traversal (`Workflow`, connector maps, `ResolveOutput`, `ResolveSingleOutput`).
- `FanOutInContext` holds fan state currently on `ThreadContext`.
- `BatchContext` holds batch scheduling fields from `design.md`.
- `IExecutionContext.ProcessContext` should be resolvable by walking `Parent` to root.

Deliverable: new types exist, no runtime changes yet.

## Phase 2 - Create a Compatibility Layer (Short Lived)
Add a lightweight adapter so that existing code can still use `RunContext` while new contexts are wired:
- Option A (preferred): Keep `RunContext` but implement it as a thin wrapper around `ProcessContext` + `WorkflowContext`.
- Option B: Add `RunContext` as an alias or facade to the new contexts until nodes are migrated.

Deliverable: project compiles with new contexts present, minimal changes to callers.

## Phase 3 - ThreadContext and Execution Flow Update
1. Update `ThreadContext` to:
   - Store `ThreadId`, `NodeContext`, `CurrentContext`.
   - Remove fan-out fields (`FanOutCount`, `FanInId`, `FanInArrived`, `FanInMergedContext`).
2. Update thread creation:
   - Replace `new ThreadContext(runContext, nodeContext, parent)` with `processContext.CreateThread(...)`.
3. Update `NodeExecutionService` to use `ProcessContext` for:
   - Thread counting and completion logic.
   - Run completion and notifications.
4. Update `EngineService` to:
   - Create `ProcessContext`, `WorkflowContext`, and the initial `ThreadContext`.

Files to update:
- `src/SharpOMatic.Engine/Contexts/ThreadContext.cs`
- `src/SharpOMatic.Engine/Services/EngineService.cs`
- `src/SharpOMatic.Engine/Services/NodeExecutionService.cs`
- `src/SharpOMatic.Engine/Services/NodeQueueService.cs` (if any signature changes)
- `src/SharpOMatic.Engine/Nodes/RunNode.cs` (expose `ProcessContext` and `WorkflowContext` accessors)

## Phase 4 - Migrate Node Implementations
### Fan Out / Fan In
- `src/SharpOMatic.Engine/Nodes/FanOutNode.cs`
  - Create `FanOutInContext` child context when encountering Fan Out.
  - Spawn threads via `ProcessContext.CreateThread` tied to the new child context.
  - Store fan state on the `FanOutInContext`.
- `src/SharpOMatic.Engine/Nodes/FanInNode.cs`
  - Replace `ThreadContext.Parent` fan-state access with `FanOutInContext` state.
  - Lock on the `FanOutInContext` instance (or use interlocked counters) to keep synchronization correct.
  - On completion, merge contexts via `ProcessContext.MergeContexts` and collapse to parent context.

### Batch
- `src/SharpOMatic.Engine/Nodes/BatchNode.cs`
  - Create a `BatchContext` child context with:
    - `BatchItems`, `BatchSize`, `ParallelBatches`, `NextItemIndex`, `InFlightBatches`, `CompletedBatches`.
    - Routing ids (`BatchNodeId`, `ContinueNodeId`).
  - Update batch scheduling to enqueue threads through `ProcessContext.CreateThread`.

### Gosub
- `src/SharpOMatic.Engine/Nodes/GosubNode.cs`
  - Create a `WorkflowContext` child under the current context.
  - Start execution within the sub-workflow context.

### Other Nodes
Most nodes access:
- `RunContext` services, run status, and helper methods.
- `Workflow` and graph resolution.

Update `RunNode` base to expose:
- `ProcessContext` (run-wide services and helpers).
- `WorkflowContext` (graph traversal helpers).

Files to update:
- `src/SharpOMatic.Engine/Nodes/*.cs`

## Phase 5 - Retire RunContext
1. Remove or reduce `RunContext`:
   - Fold any remaining methods into `ProcessContext` or shared helpers.
   - Ensure all nodes and services use new contexts.
2. Update references across the engine to point to:
   - `ProcessContext` for run-level state.
   - `WorkflowContext` for workflow traversal.
   - `ThreadContext.CurrentContext` for execution scope.

Deliverable: no runtime usage of `RunContext`.

## Phase 6 - Tests and Validation
Add or update tests to cover:
- Fan-out/fan-in correctness with nested fan-outs.
- Batch processing with various sizes and parallelism.
- Gosub nesting (gosub inside fan-out or batch).
- Run completion on success and on failure.
- Thread id allocation and active-thread counting.

Recommended manual validation:
- Run a workflow containing fan-out and fan-in with 2+ paths.
- Run a batch workflow with multiple batches and parallelism > 1.
- Run a workflow containing gosub inside fan-out and inside batch.

## Risks and Mitigations
- **Concurrency bugs in fan-in merging**
  - Mitigation: lock on `FanOutInContext` or use atomic operations.
- **Thread-count mismatch causing premature completion**
  - Mitigation: centralize thread counting in `ProcessContext` and add debug asserts.
- **Context leaks (not removed on completion)**
  - Mitigation: track active contexts in `ProcessContext` and validate on run completion.

## Proposed File Checklist
- New:
  - `src/SharpOMatic.Engine/Contexts/ProcessContext.cs`
  - `src/SharpOMatic.Engine/Contexts/WorkflowContext.cs`
  - `src/SharpOMatic.Engine/Contexts/FanOutInContext.cs`
  - `src/SharpOMatic.Engine/Contexts/BatchContext.cs`
- Modified:
  - `src/SharpOMatic.Engine/Contexts/ThreadContext.cs`
  - `src/SharpOMatic.Engine/Contexts/RunContext.cs` (transition, then remove)
  - `src/SharpOMatic.Engine/Services/EngineService.cs`
  - `src/SharpOMatic.Engine/Services/NodeExecutionService.cs`
  - `src/SharpOMatic.Engine/Services/RunNodeFactory.cs`
  - `src/SharpOMatic.Engine/Services/NodeQueueService.cs`
  - `src/SharpOMatic.Engine/Nodes/RunNode.cs`
  - `src/SharpOMatic.Engine/Nodes/FanOutNode.cs`
  - `src/SharpOMatic.Engine/Nodes/FanInNode.cs`
  - `src/SharpOMatic.Engine/Nodes/BatchNode.cs`
  - `src/SharpOMatic.Engine/Nodes/GosubNode.cs`

## Completion Criteria
- All nodes use `ThreadContext.CurrentContext` and resolve `ProcessContext`/`WorkflowContext` via the new hierarchy.
- `RunContext` is removed or reduced to a compatibility shim with no direct usage.
- Engine executes fan-out, fan-in, batch, and gosub flows with the same observable behavior as before.

