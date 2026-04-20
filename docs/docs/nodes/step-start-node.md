---
title: Step Start Node
sidebar_position: 11
---

The **Step Start** node emits a single AG-UI `STEP_STARTED` event and then immediately continues through its normal output.

## When To Use It

Use **Step Start** when the workflow wants the frontend to render the beginning of a simple step lifecycle without writing chat messages or suspending the run.

## Behavior

The node:

- emits one `STEP_STARTED` event using the configured `Step Name`
- does not suspend, branch, or write to `input.chat`
- continues through its single output

## Settings

- `Step Name`: the step label emitted to AG-UI

## Notes

- `Step Name` must be a non-empty string.
- The value is stored in SharpOMatic stream history as the event `TextDelta`.
- Pair this node with **Step End** when the frontend should render a complete start/end step lifecycle.
