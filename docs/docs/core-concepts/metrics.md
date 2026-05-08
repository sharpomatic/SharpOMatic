# Metrics

The editor includes a Metrics page for model call and workflow run usage. It is available from the main navigation.

Metrics are based on model call node executions. Each successful or failed model call writes a metric row that stores the workflow, node, connector, model, duration, token usage, cost, and failure details where available.

Workflow run metrics are written when a run reaches a terminal status: success, failure, or suspended. These rows snapshot the workflow name/version, run status, duration, conversation turn details, failure details, and aggregated model call usage for the run. They intentionally do not use foreign keys to runs, workflows, or nodes, so metrics remain available after detailed run records are pruned.

## Views

The page has a top-level switch:

- **Model Calls** shows the existing model-call dashboard.
- **Workflow Runs** shows run-level dashboard data using the same date range and bucket controls.

The Model Calls view has four tabs:

- **All** shows model call usage across every workflow, connector, and model.
- **Workflows** lists workflows and shows metrics for the selected workflow.
- **Connectors** lists connectors and shows metrics for the selected connector.
- **Models** lists models and shows metrics for the selected model.

Scoped tabs use the stored snapshot names from the metric rows, so older calls remain visible even if the original workflow, connector, model, or node has been renamed or deleted.

The Workflow Runs view has three tabs:

- **All** shows workflow run health across every workflow.
- **Workflows** lists workflows and shows run metrics for the selected workflow.
- **Errors** lists failure groups and shows runs matching the selected error, including the failed node snapshot where available.

## Charts And Tables

The Model Calls view shows:

- Total calls, total cost, total tokens, average duration, P95 duration, and failure rate.
- Calls, cost, tokens, and duration over time.
- Top workflows, connectors, models, or nodes depending on the selected tab.
- Failure groups by error type and message.
- Recent model calls and the slowest model calls for the current filter.

The Workflow Runs view shows:

- Total runs, successful runs, failed runs, suspended runs, average duration, P95 duration, and failure rate.
- Runs over time split by success, failure, and suspended.
- Cost, tokens, and duration over time.
- Model usage totals for calls, failures, tokens, and cost.
- Top workflows by run activity.
- Failure groups by error type, message, and failed node.
- Recent runs and slowest runs.

The page opens with the last 24 hours and hourly buckets so the initial dashboard query stays small. Date range presets include the last 24 hours, 7 days, 30 days, 90 days, 1 year, and all time, plus a custom range. Date range and time bucket controls apply to all tabs. Calls without pricing metadata are excluded from cost totals and are shown separately on the page.
