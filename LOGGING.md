# Logging Design

## Model Call Metrics

SharpOMatic should log every model call attempt to an append-only Engine database table. The initial goal is to support analysis of model usage by workflow, node, connector, model, conversation, success/failure, duration, tokens, and cost.

The metrics table must not use foreign keys to workflows, runs, nodes, connectors, or models. Metrics need to remain available even if the original workflow, run, node, connector, or model is deleted later. For that reason, the table stores both identifiers and snapshot names where useful.

Prompt text, instructions, resolved prompt content, and prompt hashes are not stored.

## Table

The table should be represented by a `ModelCallMetric` entity.

| Field | Type | Reason |
| --- | --- | --- |
| `Id` | `Guid` | Primary key for the metric row. |
| `Created` | `DateTime` UTC | When the model call attempt was logged; used for time-series analytics. |
| `Duration` | `long?` | Call duration in milliseconds; nullable if unavailable. |
| `Succeeded` | `bool` | Fast success/failure filtering. |
| `ErrorMessage` | `string?` | User-facing failure analysis. Nullable for success. |
| `ErrorType` | `string?` | Exception or provider category for grouping failures. |
| `WorkflowId` | `Guid` | Stable workflow grouping key, with no foreign key. |
| `WorkflowName` | `string` | Snapshot label retained after workflow deletion or rename. |
| `RunId` | `Guid` | Correlates to a run while it exists, with no foreign key. |
| `ConversationId` | `string?` | Groups model usage by conversation when applicable. |
| `NodeEntityId` | `Guid` | Stable node key inside the workflow snapshot, with no foreign key. |
| `NodeTitle` | `string` | Snapshot label retained after node deletion or rename. |
| `ConnectorId` | `Guid?` | User connector instance key, nullable if loading fails early. |
| `ConnectorName` | `string?` | Snapshot connector label. |
| `ConnectorConfigId` | `string?` | Provider/config type such as `openai`, `azure_openai`, or `google`. |
| `ConnectorConfigName` | `string?` | Display label for provider/config grouping. |
| `ModelId` | `Guid?` | User model instance key, nullable if no model was selected or found. |
| `ModelName` | `string?` | Snapshot user model label. |
| `ModelConfigId` | `string?` | Metadata model config key. |
| `ModelConfigName` | `string?` | Display name such as `gpt-5.2`. |
| `ProviderModelName` | `string?` | Actual provider model or deployment name used, important for custom models. |
| `InputTokens` | `long?` | Prompt/input usage; nullable when unavailable. |
| `OutputTokens` | `long?` | Generated-token usage. |
| `TotalTokens` | `long?` | Convenience total for dashboards and sorting. |
| `InputCost` | `decimal?` | Calculated input cost at run time. |
| `OutputCost` | `decimal?` | Calculated output cost at run time. |
| `TotalCost` | `decimal?` | Calculated total cost for reporting without repricing old calls. |

## Implementation Notes

- Add `ModelCallMetric` to the Engine repository model and expose it through `SharpOMaticDbContext`.
- Do not configure `HasOne(...)` relationships for this entity.
- Add indexes for common analytics:
  - `(Created)`
  - `(WorkflowId, Created)`
  - `(ConnectorId, Created)`
  - `(ModelId, Created)`
  - `(ConversationId, Created)`
  - `(Succeeded, Created)`
- Calculate costs at call time from the current model metadata pricing, but store only the calculated cost values.
- Store rows for both successful and failed model calls.
- If a failure occurs before connector or model metadata is loaded, persist whatever identifiers and names are available.
- Use explicit decimal precision in migrations for cost fields.

## Test Scenarios

- A successful model call creates one metric row with workflow, node, connector, and model snapshots.
- A successful model call records duration, token usage, and calculated costs when provider usage is available.
- A failed model call creates one metric row with `Succeeded = false` and error fields populated.
- Deleting workflow, run, connector, or model records does not delete model call metrics.
- Provider calls without token usage still create rows with null token and cost fields.
- Cost fields remain the run-time calculated values even if model metadata pricing changes later.
