---
title: Evaluations
sidebar_position: 8
---

Evaluations let you run a workflow repeatedly against a defined dataset and score the results with one or more grader workflows.
This is useful for prompt tuning, regression checks, and comparing workflow changes over time.

## Core entities

- **Eval Config**: top-level definition of the evaluation, including name, description, default workflow, and max parallelism.
- **Eval Columns**: schema for row fields (name, type, required/optional, and optional input path).
- **Eval Rows**: ordered test records.
- **Eval Data**: cell values for each row/column pair.
- **Eval Graders**: grader workflow references, labels, order, and pass thresholds.
- **Eval Runs**: execution instances with status, counters, and per-row/per-grader outputs.

## Run lifecycle

Eval runs use these statuses:

- `Running`
- `Completed`
- `Failed`
- `Canceled`

Each run tracks:

- `TotalRows`
- `CompletedRows`
- `FailedRows`
- `Started` and `Finished`
- `Message` and optional `Error`

## Starting runs

You can start a run from the editor or by API.

Start options:

- `name`: optional display name. If omitted, the engine generates one from the start timestamp.
- `sampleCount`: optional random sample size from the configured rows.

`sampleCount` rules:

- Must be between `1` and the total number of rows.
- Cannot be provided when there are no rows.
- If omitted, all rows are included.

## Cancel and delete rules

- **Cancel**: only valid while run status is `Running`.
- **Delete**: not allowed while a run is `Running`.

Cancel is cooperative: already-started row work may complete, but new work is not scheduled once cancellation is observed.

## Grader summaries

After row execution, grader summary metrics are calculated per grader:

- `TotalCount`, `CompletedCount`, `FailedCount`
- `MinScore`, `MaxScore`, `AverageScore`, `MedianScore`
- `StandardDeviation`
- `PassRate` (scores greater than or equal to threshold)

## HTTP API (`/api/eval/*`)

The editor host exposes evaluation endpoints from `EvalController`.

### Config endpoints

- `GET /api/eval/configs`
- `GET /api/eval/configs/count`
- `GET /api/eval/configs/{id}`
- `GET /api/eval/configs/{id}/detail`
- `POST /api/eval/configs`
- `DELETE /api/eval/configs/{id}`

Config list/count query parameters:

- `search`
- `sortBy`: `Name`, `Description`
- `sortDirection`: `Ascending`, `Descending`
- `skip`, `take`

### Config child data endpoints

- `POST /api/eval/configs/graders`
- `DELETE /api/eval/graders/{id}`
- `POST /api/eval/configs/columns`
- `DELETE /api/eval/columns/{id}`
- `POST /api/eval/configs/rows`
- `DELETE /api/eval/rows/{id}`
- `POST /api/eval/configs/data`
- `DELETE /api/eval/data/{id}`

### Run endpoints

- `POST /api/eval/configs/{id}/runs`
- `GET /api/eval/configs/{id}/runs`
- `GET /api/eval/configs/{id}/runs/count`
- `GET /api/eval/runs/{id}/detail`
- `GET /api/eval/runs/{id}/rows`
- `GET /api/eval/runs/{id}/rows/count`
- `POST /api/eval/runs/{id}/cancel`
- `DELETE /api/eval/runs/{id}`

Run list query parameters:

- `search`
- `sortBy`: `Started`, `Status`, `CompletedRows`, `FailedRows`, `Name`
- `sortDirection`
- `skip`, `take`

Run row list query parameters:

- `search`
- `sortBy`: `Name`, `Status`, `Started`, `Order`
- `sortDirection`
- `skip`, `take`

### Start run request body

```json
{
  "name": "Nightly prompt regression",
  "sampleCount": 25
}
```

Both fields are optional.

