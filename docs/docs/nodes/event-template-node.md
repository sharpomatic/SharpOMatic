---
title: Event Template Node
sidebar_position: 18
---

The **Event Template** node emits a text or reasoning stream message from a template.
Use it when a workflow needs to write a simple AG-UI-compatible stream event without adding a code node.

## Template

The node expands the **Template** field with the same syntax used by Model Call text fields and the code node `Templates` helper:

- `{{path}}` or `{{$path}}` inserts a value from workflow context.
- `<<asset-name>>` inserts the contents of a text-like asset.
- A comma-separated list, such as `{{$preferred, $fallback}}` or `<<preferred.txt, fallback.txt>>`, tries each context path or asset name from left to right.
- String context values and text asset contents are expanded recursively.

If none of the listed context paths or assets can be resolved, template expansion fails and the workflow run records the error.
If the expanded template is empty or only whitespace, the node does nothing and continues to the next node.

## Output Type

Set **Output Type** to choose the stream lifecycle:

- **Text** emits `TextStart`, `TextContent`, and `TextEnd`.
- **Reasoning** emits `ReasoningStart`, `ReasoningMessageStart`, `ReasoningMessageContent`, `ReasoningMessageEnd`, and `ReasoningEnd`.

The node generates a new message id each time it runs.

## Text Role

When **Output Type** is **Text**, choose the **Text Role**:

- `Assistant`
- `User`
- `System`
- `Developer`
- `Tool`

Reasoning output always uses the protocol reasoning role, so the text role selector is hidden when **Reasoning** is selected.

## Silent

Enable **Silent** when the event should be stored in SharpOMatic stream history but suppressed from live AG-UI SSE output for the current run.
Silent is useful for recording messages that clients already know about, such as an echoed user message.

The silent flag is transient.
It affects live stream delivery only and is not stored in the persisted stream event rows.
