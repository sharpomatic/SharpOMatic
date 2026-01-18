---
title: Workflows
sidebar_position: 1
---

A workflow is a directed graph of nodes connected by links that control execution flow.
Create and configure workflows in the browser-based editor, run them there to review results, and iterate quickly.
Once complete, you can invoke workflows programmatically in your deployed environment.

## Nodes

Common node types include:

- **Start** - each workflow must have a single start node as the entry point.
- **End** - there can be multiple end nodes when there are multiple paths to completion.
- **Edit** - add, update, or delete entries from the context.
- **Code** - run a C# block of code as glue logic or to call into your backend.
- **ModelCall** - make an LLM call to an external provider.
- **Switch** - choose between multiple output paths.
- **FanOut** / **FanIn** - run multiple paths in parallel.

## Connections

A connection links two nodes and controls the execution flow.

- An output can connect to the input of only one other node. For example, the **Start** node has a single output that connects to a single next node.
- To run multiple paths in parallel, use **FanOut** / **FanIn** instead of fanning a single output.
- A node can accept multiple inputs. For example, a **Switch** node can route multiple paths that converge into **End**. Only one path executes at runtime, but multiple paths can still connect to the same node.

## Inputs and Outputs

Nodes read from and write to a shared context, which is the mechanism for passing information around the workflow.

- The initial context is provided to the **Start** node. If the workflow needs input parameters, they are included in that initial context by the user or programmatically.
- On workflow completion, the final context is returned as the workflow result.
- The context stores intermediate and temporary values. Modify it using configuration via the **Edit** node, or use the **Code** node for full access.

## Runs and Traces

Each workflow run creates a record in the database that tracks:

- Start time, status, initial context, final context, and more.
- The overall success of the workflow, visible in the editor's run history.

Each run also includes trace records that capture node-level execution details.
These help you identify which node raised an error and view the context at the time of failure.
You can see traces in the editor by selecting a run.

## Workflow Completion

A workflow completes when either:

- A node execution error is encountered.
- All execution paths are finished.

**End** nodes are optional, but they are strongly recommended as best practice.
Without end nodes, the context from the last node executed becomes the workflow result.
If there are multiple paths, particularly if they run in parallel, then the order in which paths are completed can vary.
This means different nodes may finish last, producing different results.
If an **End** node runs, its output becomes the workflow result and later non-end paths do not override it.
When multiple **End** nodes run (for example, after parallel paths), the one that finishes last sets the final result.

The **End** node can select which context entries are output as the workflow result.
This lets you exclude intermediate values that are not relevant to the outcome, producing a smaller and cleaner result.
