# Metrics

The editor includes a Metrics page for model call usage. It is available from the main navigation.

Metrics are based on model call node executions. Each successful or failed model call writes a metric row that stores the workflow, node, connector, model, duration, token usage, cost, and failure details where available.

## Views

The page has four tabs:

- **All** shows model call usage across every workflow, connector, and model.
- **Workflows** lists workflows and shows metrics for the selected workflow.
- **Connectors** lists connectors and shows metrics for the selected connector.
- **Models** lists models and shows metrics for the selected model.

Scoped tabs use the stored snapshot names from the metric rows, so older calls remain visible even if the original workflow, connector, model, or node has been renamed or deleted.

## Charts And Tables

Each view shows:

- Total calls, total cost, total tokens, average duration, P95 duration, and failure rate.
- Calls, cost, tokens, and duration over time.
- Top workflows, connectors, models, or nodes depending on the selected tab.
- Failure groups by error type and message.
- Recent model calls and the slowest model calls for the current filter.

Date range and time bucket controls apply to all tabs. Calls without pricing metadata are excluded from cost totals and are shown separately on the page.
