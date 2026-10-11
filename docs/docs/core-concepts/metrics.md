# Metrics

The editor includes a Metrics page for model call and workflow run usage. It is available from the main navigation.

Metrics are based on model call node executions. Each successful or failed model call writes a metric row that stores the workflow, node, connector, model, duration, token usage, cost, and failure details where available.

Each primary or fallback provider attempt writes its own model-call metric. Attempts from one node execution share a logical call ID and use increasing attempt numbers, starting at 1. The highest attempt number is the final attempt; attempt numbers greater than 1 are fallbacks. A recovered call therefore contributes one failed primary metric and one successful fallback metric. Model-call and workflow-run model-call counts describe provider attempts, while the workflow itself can still succeed after a recovered failure.

Failed attempts also store a normalized failure category and the raw HTTP provider status code when one was returned, such as 403, 429, or 503. Network failures and other errors that occur without an HTTP response have no provider status code.

The dashboard derives logical-call outcomes by grouping attempts with the same logical call ID. A logical call is **recovered** when an earlier attempt failed and a later fallback succeeded. A call is **unrecovered** when its highest-numbered attempt failed. Historical metrics without a logical call ID are treated as independent logical calls using their metric row ID.

Workflow run metrics are written when a run reaches a terminal status: success, failure, or suspended. These rows snapshot the workflow name/version, run status, duration, conversation turn details, failure details, and aggregated model call usage for the run. They intentionally do not use foreign keys to runs, workflows, or nodes, so metrics remain available after detailed run records are pruned.

The workflow editor's Runs tab shows the model-call count and known USD model cost for each completed run. For conversation-enabled workflows, the Conversations tab shows cumulative values across all turns. These values are persisted when each run finishes, including model calls made by Gosub workflows. Calls without pricing metadata contribute to the call count but not to the displayed cost.

## Tenant And User Attribution

Model call metrics and workflow run metrics both store an optional tenant ID and user ID, so usage and cost can be attributed to whoever triggered the work. The values are read from the context using two paths set on the workflow's **Details** tab:

- **Tenant Id Path** defaults to `auth.tenantId`.
- **User Id Path** defaults to `auth.userId`.

An empty path also uses the default, so existing workflows record attribution without any changes. Set a different path when your host places the identifiers elsewhere in the context.

When a metric is written, the value found at the path is stored as text:

- A `Guid` is stored in its standard lowercase form.
- A string that parses as a GUID is normalized to the same lowercase form, so values from different sources match.
- Any other string is stored trimmed.
- A missing path, an invalid path, an empty string, a value longer than 256 characters, or a value of any other type is stored as null. Attribution never causes a model call or run to fail.

The Metrics page has a filter bar below the header with **Tenant Id** and **User Id** inputs. Entering either value limits every view, tab, chart, and table to metrics with a matching value, and entering both requires both to match. Values are matched exactly after the same normalization used when storing them, so a GUID matches regardless of letter case or surrounding whitespace. The filter is applied in the database query alongside the date range, and each metrics table has a tenant and a user index for it. Clear the inputs, or use the clear button, to show all metrics again.

A model call metric uses the paths of the workflow that contains the model call node, resolved against the context at the time of the call. For a model call inside a **Gosub** workflow, that is the child workflow's paths and the child's context, so when the Gosub applies input mappings the identifiers must be mapped into the child for them to be recorded. A workflow run metric uses the paths of the workflow that was started, resolved against the context when the run finishes.

## Views

The page has a top-level switch:

- **Model Calls** shows the existing model-call dashboard.
- **Workflow Runs** shows run-level dashboard data using the same date range and bucket controls.

The Model Calls view has four tabs:

- **All** shows model call usage across every workflow, connector, and model.
- **Workflows** lists workflows and shows metrics for the selected workflow.
- **Connectors** lists connectors and shows metrics for the selected connector.
- **Models** lists models and shows metrics for the selected model.

Scoped tabs use the stored snapshot names from the metric rows, so older calls remain visible even if the original workflow, connector, model, or node has been renamed or deleted. Workflow, connector, and model selections match by snapshot name rather than internal ID, so selecting an old name after a rename shows only metrics captured under that old name.

The Workflow Runs view has three tabs:

- **All** shows workflow run health across every workflow.
- **Workflows** lists workflows and shows run metrics for the selected workflow.
- **Errors** lists failure groups and shows runs matching the selected error, including the failed node snapshot where available.

## Charts And Tables

The Model Calls view shows:

- Provider attempts, logical calls, calls requiring fallback, recovered calls, unrecovered calls, and fallback recovery rate.
- Attempt failure rate separately from final logical-call failure rate.
- Attempts and recovery, cost, tokens, and duration over time. Attempt charts distinguish primary success/failure from fallback recovery/failure.
- Top workflows, connectors, models, or nodes depending on the selected tab, including primary-attempt, fallback-attempt, and successful-fallback counts.
- Failure groups by normalized category and HTTP status as well as detailed exception type and message.
- Recent and slowest logical calls. Each row can be expanded to inspect its ordered provider attempts, actual models/connectors, normalized failure category, HTTP status, duration, tokens, cost, and error.

The Workflow Runs view shows:

- Total runs, successful runs, failed runs, suspended runs, average duration, P95 duration, and failure rate.
- Runs over time split by success, failure, and suspended.
- Cost, tokens, and duration over time.
- Model usage totals for provider attempts, failed model attempts, tokens, and cost.
- Top workflows by run activity.
- Failure groups by error type, message, and failed node.
- Recent runs and slowest runs.

The page opens with the last 24 hours and hourly buckets so the initial dashboard query stays small. Date range presets include the last 24 hours, 7 days, 30 days, 90 days, 1 year, and all time, plus a custom range. Date range and time bucket controls apply to all tabs. Calls without pricing metadata are excluded from cost totals and are shown separately on the page.
