---
title: Parallel paths logic
---

This sample runs multiple paths in parallel and merges their output values.

## Choose this when

Use this sample when independent pieces of work can run at the same time and the workflow should continue only after all branches finish.

## What it demonstrates

- Splitting execution with **FanOut**.
- Waiting for branches with **FanIn**.
- Nesting a second **FanOut** and **FanIn** inside one branch.
- Merging values written under `output`.

## How it works

The workflow initializes shared context, fans out into parallel Code nodes, and each branch waits for a random short delay before writing its own output message.
One branch contains another fan-out and fan-in pair, showing that parallel orchestration can be nested.
The final **FanIn** waits for all branches and merges their `output` entries before the workflow ends.

## Setup notes

No connector or model is required.
The random delays make branch completion order visible in the editor.
