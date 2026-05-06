# Model Call Logging

SharpOMatic records one `ModelCallMetric` row for each model call node execution. Rows are appended for successful calls and for failures, including failures that happen before the provider call starts.

The table stores snapshot values instead of foreign-key relationships so historical metrics still make sense after workflows, nodes, connectors, or models are renamed or deleted.

## Stored Fields

| Field | Type | Purpose |
| --- | --- | --- |
| `Id` | `Guid` | Unique metric row identifier. |
| `Created` | `DateTime` | UTC time the model call metric was created. Used for time-series charts and date filtering. |
| `Duration` | `long?` | Elapsed call duration in milliseconds. Used for average and P95 latency metrics. |
| `Succeeded` | `bool` | Separates successful calls from failures. |
| `ErrorMessage` | `string?` | Failure message shown in metrics failure tables. |
| `ErrorType` | `string?` | Exception type used to group failures. |
| `WorkflowId` | `Guid` | Workflow snapshot key for grouping. Not a foreign key. |
| `WorkflowName` | `string` | Workflow display name at execution time. |
| `RunId` | `Guid` | Run snapshot key for tracing a call back to a run. Not a foreign key. |
| `ConversationId` | `string?` | Conversation id when the call happened in a conversation workflow. |
| `NodeEntityId` | `Guid` | Node snapshot key for grouping calls by workflow node. Not a foreign key. |
| `NodeTitle` | `string` | Node display title at execution time. |
| `ConnectorId` | `Guid?` | Connector snapshot key when resolved. Not a foreign key. |
| `ConnectorName` | `string?` | Connector display name at execution time. |
| `ConnectorConfigId` | `string?` | Connector provider/config id. |
| `ConnectorConfigName` | `string?` | Connector provider/config display name. |
| `ModelId` | `Guid?` | Model snapshot key when resolved. Not a foreign key. |
| `ModelName` | `string?` | Model display name at execution time. |
| `ModelConfigId` | `string?` | Model config id. |
| `ModelConfigName` | `string?` | Model config display name. |
| `ProviderModelName` | `string?` | Provider model name returned by the model caller. |
| `InputTokens` | `long?` | Provider-reported input token count. |
| `OutputTokens` | `long?` | Provider-reported output token count. |
| `TotalTokens` | `long?` | Provider-reported total token count. |
| `InputCost` | `decimal?` | Calculated input cost when model pricing metadata is available. |
| `OutputCost` | `decimal?` | Calculated output cost when model pricing metadata is available. |
| `TotalCost` | `decimal?` | Calculated total cost when pricing metadata is available. |

## Metrics Page

The editor exposes a Metrics page with All, Workflows, Connectors, and Models tabs. Each tab shows total calls, cost, tokens, latency, failures, time-series charts, breakdown tables, grouped failures, recent calls, and slowest calls.

Scoped tabs use a master-detail list. The list is grouped by the snapshot id when one exists and uses the stored snapshot name for display, so deleted or renamed objects remain visible in historical metrics.
