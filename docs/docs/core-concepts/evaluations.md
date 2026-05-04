---
title: Evaluations
sidebar_position: 9
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
You can define multiple graders, set a pass threshold for each one, and choose whether each grader contributes to the overall run score.
After the run completes, grader summaries provide statistics such as minimum score, maximum score, average score, median score, standard deviation, and pass rate.

Evaluation workflows and grader workflows must be standard one-shot workflows.
Conversation-enabled workflows are intentionally excluded from the evaluation workflow selectors because evaluations do not provide a way to answer suspend events during row execution.

### Grader Output Contract

A grader workflow is expected to write its score to the context path `score`.
This value is used for score statistics and pass-rate calculations.
The score can be numeric, or a string value that can be parsed as a number.

If a grader completes without providing a numeric `score`, the grader run can still complete, but score-based aggregates will not include that row.
For consistent evaluation metrics, ensure every grader writes a valid numeric value to `score`.

### Score Calculation

Each completed grader result has its own raw score.
The grader's **Pass Threshold** is used only for pass-rate calculations: a scored grader result passes when `score >= passThreshold`.
Failed grader runs and completed grader runs without a numeric score are excluded from score statistics and pass-rate denominators.

The evaluation's **Row Score** setting controls how each row score is calculated from that row's grader scores:

- **First grader**: use the first grader's score, based on grader order.
- **Average**: average all available grader scores for the row.
- **Minimum**: use the lowest available grader score for the row.
- **Maximum**: use the highest available grader score for the row.

The evaluation's **Run Score** setting controls which per-grader summary metric contributes to the run score:

- **Average score**: use each selected grader's average score.
- **Average min score**: use each selected grader's minimum score.
- **Average max score**: use each selected grader's maximum score.
- **Average pass rate**: use each selected grader's pass rate.

For each grader, the **Run Score** checkbox controls whether that grader is included in the run score calculation.
The final run score is the average of the selected metric across included graders that have a value.
If no graders are included, or none of the included graders produced a usable metric, the run score is empty.
Pass thresholds still appear in grader summaries even when the run score is not based on pass rate.

## Running an Evaluation

From the evaluation page, select **Start Run**.
The start dialog allows:

- **Run name (optional)**: give the run a readable label. If omitted, the system uses a timestamp-based default name.
- **Run random sample**: enable this to execute only a subset of rows.
- **Sample count**: choose how many rows to run in the random sample.

Sampling is useful for quick checks when you want faster feedback before running the full dataset.
If sampling is enabled, sample count must be between `1` and the total number of rows in the evaluation.
Rows are selected randomly each run, so two sampled runs can execute different subsets.
If sampling is disabled, all rows are executed.

## Runs

Each run is stored with status and progress information.
You can review run summaries, open detailed results, and inspect grader outcomes.
Runs can be canceled while in progress, and completed/failed/canceled runs remain in history for later comparison.

Evaluation execution creates underlying workflow runs for the main workflow and any grader workflows.
Those child workflow runs are stored like normal runs, but they are treated as background execution by the editor.
This means the evaluation pages continue to show evaluation progress, while the workflow page trace panel does not live-follow those evaluation-driven runs or show workflow completion toasts for them.

## Transfer Import Behavior

When evaluations are imported from a transfer package, SharpOMatic creates new evaluation entries with new identifiers.
The import does not replace an existing evaluation by ID.

This clone-on-import behavior is intentional so existing evaluations keep their original run history.
In other words, importing an evaluation configuration does not delete previous runs from existing evaluations.

Evaluation transfer includes the full configuration:

- EvalConfig
- EvalGraders
- EvalColumns
- EvalRows
- EvalData

Evaluation transfer also includes terminal run result data:

- EvalRun
- EvalRunRow
- EvalRunRowGrader
- EvalRunGraderSummary

Runs that are still running at export time are skipped because they cannot be resumed in the target instance.

## Troubleshooting

Common causes of evaluation run failures:

- **Missing workflow reference**: the evaluation workflow or grader workflow is not set or no longer exists.
- **Conversation workflow selected previously**: if an older configuration references a conversation-enabled workflow, SharpOMatic clears that selection because evaluations cannot execute conversation turns.
- **Missing mandatory row data**: a required column has no value for one or more rows.
- **Invalid sample count**: the sample count is outside the valid range for the current row total.
- **Missing grader score**: the grader workflow does not output a numeric value at `score`, so score summaries may look incomplete.

When troubleshooting, open the run details and inspect row-level errors and grader results to identify the exact failure point.
