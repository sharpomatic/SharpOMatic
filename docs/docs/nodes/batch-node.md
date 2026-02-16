---
title: Batch Node
sidebar_position: 8
---

The **Batch** node processes a list in chunks and can run multiple chunks in parallel.
It has two outputs:

- **continue**: runs once after all batch processing has finished.
- **process**: runs for each batch slice.

## Required settings

- **Input Array Path**: context path that must resolve to a `ContextList`.
- **Output Array Path**: context path used to collect per-batch outputs.
- **Batch Size**: number of items per batch.
- **Parallel Batches**: maximum number of active batch slices at once.

## Runtime behavior

1. The node resolves the input list from **Input Array Path**.
2. It schedules up to **Parallel Batches** batch slices.
3. Each slice is written back into context at the same input path before invoking the **process** path.
4. When all slices complete, outputs are merged and execution continues through **continue** (if connected).

If **process** is not connected, the node skips batch execution and immediately attempts the **continue** path.
If **continue** is not connected, execution ends after batch handling completes.

## Validation and failure rules

The node fails if:

- `BatchSize` is less than `1`
- `ParallelBatches` is less than `1`
- **Input Array Path** is empty
- **Input Array Path** does not resolve
- resolved value is not a `ContextList`

## Output merge notes

Per-batch values found at **Output Array Path** are merged back into a single final context after all slices complete.
If **Output Array Path** is empty, no batch output merge is performed.

The merged context then flows into the **continue** branch.

