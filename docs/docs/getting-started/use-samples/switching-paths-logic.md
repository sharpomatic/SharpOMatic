---
title: "Basic: Switching paths logic"
---

This sample routes execution through different branches based on a random number.

## Choose this when

Use this sample when you want to learn conditional branching or how to chain switch decisions.

## What it demonstrates

- Setting a random value in context from a **Code** node.
- Using **Switch** outputs for named conditions.
- Chaining one **Switch** node into another.
- Writing a branch-specific value to `output`.

## How it works

The workflow generates a random `number`, then uses the first **Switch** node to choose paths for values below 30, below 60, or 60 and above.
The final range is checked by a second **Switch** node, which routes to either the third or fourth branch.
Each branch delays briefly and writes a message showing which path was taken.

## Setup notes

No connector or model is required.
Run it several times to see different paths execute.
