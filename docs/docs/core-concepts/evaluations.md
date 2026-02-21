---
title: Evaluations
sidebar_position: 8
---

Evaluations help you measure workflow quality in a repeatable way.
Instead of checking outputs manually one-by-one, you define a reusable evaluation configuration and run it whenever your workflow changes.
This makes regression checks, model swaps, prompt updates, and release decisions more reliable.

## Why Evaluations Matter

AI workflow behavior can shift over time due to model updates, prompt changes, tool changes, and workflow logic edits.
Without evaluations, these shifts are hard to detect consistently.

Evaluations provide a structured test harness so you can:

- Compare quality across workflow versions.
- Catch regressions before promoting changes.
- Measure pass/fail and score trends over time.
- Debug weak cases using row-level run details.

## Evaluation Structure

An evaluation is defined by three core concepts: columns, rows, and graders.

### Columns

Columns define the schema for each test case row.
Each column has a type, can be mandatory or optional, and can map to an input path used when the workflow run is created.

The first column is a required **Name** column.
It is used to identify each row in run views and result tables.

### Rows

Rows are the individual test cases.
Each row stores values for the configured columns.
When you run the evaluation, each row becomes one workflow execution input.

### Graders

Graders are workflows that score or assess the output from the main evaluation workflow.
You can define multiple graders and set a pass threshold for each one.
After the run completes, grader summaries provide statistics such as average score and pass rate.

## Running an Evaluation

From the evaluation page, select **Start Run**.
The start dialog allows:

- **Run name (optional)**: give the run a readable label. If omitted, the system uses a timestamp-based default name.
- **Run random sample**: enable this to execute only a subset of rows.
- **Sample count**: choose how many rows to run in the random sample.

Sampling is useful for quick checks when you want faster feedback before running the full dataset.

## Runs

Each run is stored with status and progress information.
You can review run summaries, open detailed results, and inspect grader outcomes.
Runs can be canceled while in progress, and completed/failed/canceled runs remain in history for later comparison.

## Transfer Import Behavior

When evaluations are imported from a transfer package, SharpOMatic creates new evaluation entries with new identifiers.
The import does not replace an existing evaluation by ID.

This clone-on-import behavior is intentional so existing evaluations keep their original run history.
In other words, importing an evaluation configuration does not delete previous runs from existing evaluations.

