---
title: Batch processing of items
---

This sample processes a generated list in parallel batches and merges the batch outputs.

## Choose this when

Use this sample when you need to split a list into chunks and process more than one chunk at a time.

## What it demonstrates

- Creating a `ContextList` from a **Code** node.
- Configuring a **Batch** node with `batchSize: 2` and `parallelBatches: 2`.
- Processing each batch slice through the **process** output.
- Merging batch results into an output list.

## How it works

The workflow starts with an optional `count` input defaulting to `10`, then creates an `inputs` list containing numbers from `0` to `count - 1`.
The **Batch** node splits `inputs` into chunks of two and runs up to two chunks in parallel.
The batch Code node doubles each number and writes the slice to `outputs`, which the Batch node merges before continuing to **End**.

## Setup notes

No connector or model is required.
Change the `count`, batch size, or parallel batch settings to see how the batching behavior changes.
