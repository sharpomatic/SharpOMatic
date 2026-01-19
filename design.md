# Node Execution Rewrite Design

## Summary
This design replaces the existing RunContext/ThreadContext split with a hierarchical execution context model. A single ProcessContext represents the full workflow run. A WorkflowContext represents execution of a workflow and is a child of the ProcessContext (or another context when created by gosub). FanOutInContext and BatchContext represent scoped execution boundaries for fan-out/fan-in and batch processing. A ThreadContext still exists to hold per-thread data like the thread's ContextObject and thread id, and to point at the active execution context.

## Goals
- Make execution boundaries explicit and hierarchical.
- Allow nested fan-out and batch processing without global state hacks.
- Keep thread tracking centralized in the ProcessContext.
- Keep thread-to-context mapping explicit for each active thread.
- Keep per-thread data (ContextObject + thread id) in ThreadContext.

## Context Types

### ProcessContext (root)
- Represents a single workflow run from start to finish.
- Created when a new workflow run starts.
- Owns run-wide coordination (thread count, thread id assignment, active context tracking).
- Responsible for creating and removing ThreadContext instances.
- Parent: none.

### WorkflowContext
- Represents execution of a workflow.
- Created once for the root workflow, and again for each gosub call.
- Parent: ProcessContext or any child context that invoked gosub (WorkflowContext, FanOutInContext, or BatchContext).
- Removed when the workflow completes.

### FanOutInContext
- Represents a fan-out scope that exists until its matching fan-in completes.
- Created when a Fan Out node is reached.
- Parent: current active context (typically WorkflowContext, but can be another FanOutInContext or BatchContext).
- Removed when the matching Fan In node is processed.

### BatchContext
- Represents batch processing scope.
- Created when a batch node begins processing.
- Parent: current active context.
- Removed when the continue is processed and batch processing is complete.
- Holds the ContextList of items being processed by the batch.
- Carries batch scheduling state (size, parallelism, cursor, counters, routing ids).

### ThreadContext
- Represents a single execution thread.
- Stores the per-thread ContextObject (node context) and unique thread id.
- Points to the current execution context (WorkflowContext, FanOutInContext, or BatchContext).

## Hierarchy Rules
- ProcessContext is the root for the entire run.
- WorkflowContext is always the next level under the ProcessContext unless created by gosub.
- FanOutInContext and BatchContext are created as children of the current active context.
- Nested fan-outs create nested FanOutInContext children.
- Gosub creates a new WorkflowContext child under the current context, regardless of type.
- Any execution context can resolve ProcessContext by walking Parent to the root.

Example hierarchy:
```
ProcessContext
  WorkflowContext (root workflow)
    FanOutInContext (fan-out A)
      FanOutInContext (nested fan-out B)
      WorkflowContext (gosub inside fan-out)
    BatchContext (batch 1)
      WorkflowContext (gosub inside batch)
```

## Thread Model
- Each thread of execution is represented by a ThreadContext and points to the context it is running inside.
- ProcessContext is responsible for:
  - Tracking active thread count.
  - Assigning new thread identifiers.
  - Creating ThreadContext instances.
  - Removing ThreadContext instances when they complete.
  - Tracking active execution contexts for the run.
  - Any run-wide coordination needed across contexts.
- Execution contexts do not assign thread ids; they only scope execution behavior for the threads that enter them.

## Execution Flow Scenarios

### Root start
1. Create ProcessContext for the run.
2. Create WorkflowContext as a child of ProcessContext.
3. Start execution with a thread pointing to the WorkflowContext.

### Fan-out path
1. Fan Out node encountered in current context.
2. Create FanOutInContext as a child of the current context.
3. Each fan path thread points to the FanOutInContext (or a nested context created beneath it).
4. When the matching Fan In node is processed, remove the FanOutInContext.

### Batch processing
1. Batch node encountered in current context.
2. Create BatchContext as a child of the current context.
3. Threads processing batch items point to the BatchContext (or a nested context created beneath it).
4. When the continue is processed and batching ends, remove the BatchContext.

### Gosub
1. Gosub node encountered in current context.
2. Create WorkflowContext as a child of the current context.
3. Execute the sub-workflow within that WorkflowContext.
4. Remove the WorkflowContext when the sub-workflow completes.

## Mapping From Existing Model
- Current RunContext combines ProcessContext + WorkflowContext responsibilities.
- ProcessContext should own run-wide services/state: ServiceScope, RepositoryService, ProgressServices, ToolMethodRegistry, SchemaTypeRegistry, ScriptOptionsService, JsonConverters, Run, CompletionSource, RunNodeLimit, NodesRun, thread id/count, RunUpdated.
- WorkflowContext should own workflow graph state: Workflow entity, connector maps, ResolveOutput/ResolveSingleOutput.
- RunContext.MergeContexts is a run-wide utility that needs a new home (ProcessContext or a shared helper).
- ThreadContext remains for per-thread data (ContextObject + thread id) and thread-to-context mapping.
- Fan-related properties move from ThreadContext to FanOutInContext.

## Interface Drafts
These are intentionally minimal placeholders to guide implementation; we will flesh them out as the design stabilizes.

```csharp
public interface IExecutionContext
{
    IExecutionContext? Parent { get; }
    IProcessContext ProcessContext { get; }
}

public interface IProcessContext : IExecutionContext
{
    IThreadContext CreateThread(ContextObject nodeContext, IExecutionContext currentContext);
    void RemoveThread(IThreadContext threadContext);
}

public interface IWorkflowContext : IExecutionContext
{
    Guid WorkflowId { get; set; }
    NodeEntity ResolveOutput(ConnectorEntity connector);
    NodeEntity ResolveSingleOutput(NodeEntity node);
}

public interface IFanOutInContext : IExecutionContext
{
    int FanOutCount { get; set; }
    Guid? FanInId { get; set; }
    int FanInArrived { get; set; }
    ContextObject? MergedContext { get; set; }
}

public interface IBatchContext : IExecutionContext
{
    Guid FanOutId { get; set; }
    ContextList BatchItems { get; set; }
    int BatchSize { get; set; }
    int ParallelBatches { get; set; }
    int NextItemIndex { get; set; }
    int InFlightBatches { get; set; }
    int CompletedBatches { get; set; }
    Guid BatchNodeId { get; set; }
    Guid? ContinueNodeId { get; set; }
}

public interface IThreadContext
{
    int ThreadId { get; }
    Guid NodeId { get; set; }
    ContextObject NodeContext { get; set; }
}
```

## Open Questions
- What additional run-wide state should live exclusively on ProcessContext?
- Do any nodes require their own dedicated context type beyond fan-out, batch, and gosub?
