---
title: Gosub Node
sidebar_position: 9
---

The **Gosub** node calls another workflow as a child workflow, then optionally returns to the next node in the parent workflow.
Use it to reuse workflow logic without duplicating node graphs.

## Child workflow selection

- **Workflow** must be set.
- The target workflow must contain exactly one **Start** node.

If no return output is connected on the parent **Gosub** node, parent execution stops after the child completes.

## Input mapping modes

### Full-context copy

If **Map parent context into child** is disabled, the child receives a cloned copy of the entire parent context.

### Explicit mapping

If **Map parent context into child** is enabled, each mapping entry can:

- map from parent `Input Path` to child `Output Path`
- be mandatory or optional
- provide a default value when optional and not found in parent context

Mandatory entries fail the run when their source path is missing.

## Output mapping modes

### Full merge back to parent

If **Map child context back to parent** is disabled, the child context is merged back into parent context recursively.
When keys overlap, child values overwrite parent values.

### Explicit mapping back

If **Map child context back to parent** is enabled, only mapped entries are written back:

- child `Input Path` -> parent `Output Path`

Missing child paths are skipped.

## Return behavior

When child execution completes, control returns to the parent output connected from the **Gosub** node (if present).
Child traces are linked to parent execution for debugging continuity.

