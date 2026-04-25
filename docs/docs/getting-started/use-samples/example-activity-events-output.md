---
title: Example activity events output
---

This sample shows how to publish activity progress to the frontend while a workflow is running.

## Choose this when

Use this sample when you want to display progress, checklist-style activity state, or long-running task updates in the editor UI.

## What it demonstrates

- Creating activity data in context from a **Code** node.
- Publishing activity state with repeated **ActivitySync** nodes.
- Updating progress in a loop.
- Using a **Switch** node to stop when progress reaches 100 percent.

## How it works

The workflow creates an `activity` object with steps, syncs it to the frontend, marks the first step finished, then repeatedly increments a `progress` value.
Each loop updates the second activity step and syncs the latest state until the **Switch** node routes execution to **End**.

## Setup notes

No connector or model is required.
This sample is not conversation-enabled, so it is suitable for learning activity rendering without chat state.
