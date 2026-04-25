---
title: Looping around path
---

This sample loops through the same workflow path a fixed number of times.

## Choose this when

Use this sample when you need to repeat workflow logic until a counter or other condition says it is finished.

## What it demonstrates

- Initializing a configurable `loops` value.
- Using an **Edit** node to initialize `count`.
- Using a **Switch** node to choose between loop and finished paths.
- Updating context from a **Code** node before returning to the switch.

## How it works

The workflow starts with an optional `loops` input defaulting to `5`, sets `count` to `0`, and checks whether `count` is less than `loops`.
Each loop waits briefly, increments `count`, appends a timestamp to `results`, and routes back to the **Switch** node.
When the counter reaches the configured limit, the workflow exits through **End**.

## Setup notes

No connector or model is required.
Change the `loops` start input to test different run lengths.
