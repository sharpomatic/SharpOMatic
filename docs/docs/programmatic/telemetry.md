---
title: OpenTelemetry Tracing
sidebar_position: 7
---

The engine emits OpenTelemetry activities (spans) for workflow runs so a host application can view them in any OpenTelemetry backend, such as Azure Application Insights, the Aspire Dashboard, or Jaeger.

## What Is Emitted

All engine spans are created on a single `ActivitySource` named `SharpOMatic.Engine` (exposed as the constant `SharpOMaticDiagnostics.SourceName`).

| Span | When | Key attributes |
| --- | --- | --- |
| `invoke_agent {workflow name}` | One per workflow run, from start until success, failure, or suspension. Follows the OpenTelemetry GenAI agent-invocation convention so agent-aware backends (such as the Application Insights Agents view) list each run as an agent run. | `gen_ai.operation.name` (`invoke_agent`), `gen_ai.provider.name` (`sharpomatic`), `gen_ai.agent.id` / `gen_ai.agent.name` (workflow id/name), `gen_ai.conversation.id` and `session.id` (conversation id), `workflow.id`, `workflow.name`, `sharpomatic.run.id`, `sharpomatic.run.status`, `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, `sharpomatic.model_call.count`, `sharpomatic.model_call.total_cost`, `error.type`, `sharpomatic.failed_node.*` |
| `executor.process {node title}` | One per node execution, parented to the run span. | `executor.id`, `executor.type`, `sharpomatic.node.status`, `sharpomatic.run.id`, plus node-specific attributes below |
| `chat {model}` | One per provider round trip, emitted by the `Microsoft.Extensions.AI` OpenTelemetry middleware that the engine wraps around every model call chat client. Follows the OpenTelemetry GenAI semantic conventions (`gen_ai.*` attributes including token usage). | `gen_ai.*` |

Node executions add type-specific attributes to their `executor.process` span:

| Node type | Attributes |
| --- | --- |
| Model Call | `sharpomatic.model.name`, `sharpomatic.model.config`, `sharpomatic.connector.name`, `sharpomatic.connector.config`, `gen_ai.request.model` (provider model name), `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, `sharpomatic.model_call.total_cost` |
| Switch | `sharpomatic.switch.selected` — the name of the branch that was taken |
| Fan Out | `sharpomatic.fan_out.branch_count` |
| Fan In | `sharpomatic.fan_in.arrival` (this thread's arrival order), `sharpomatic.fan_in.expected`, `sharpomatic.fan_in.completed` (true on the arrival that completed the merge) |
| Batch | `sharpomatic.batch.item_count`, `sharpomatic.batch.batch_size`, `sharpomatic.batch.parallel_batches` |
| Gosub | `sharpomatic.gosub.workflow_id`, `sharpomatic.gosub.workflow_name` |

Custom node implementations deriving from `RunNode<T>` can stamp their own tags through the protected `NodeActivity` property (null when telemetry is disabled or nothing is listening, so always use `NodeActivity?.SetTag(...)`).

The run span is started on the caller's async flow, so it is parented to whatever ambient `Activity` is current when the run starts (for example an ASP.NET Core request or a message-processing span in the host). Node spans execute on engine worker threads and are parented explicitly to the run span. Model call spans are children of their node span.

Failed runs and nodes set the span status to `Error`; the workflow run and model call metrics recorded in the repository are unchanged and complement the spans.

## Exporting Spans From A Host

The engine only creates activities — it never references the OpenTelemetry SDK or any exporter. Nothing is collected (and the overhead is near zero) until the host registers the source name with a trace provider. If the source is not registered, spans are silently dropped.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyApplication"))
            .AddSource(SharpOMaticDiagnostics.SourceName)          // engine workflow/node/model spans
            .AddSource("Experimental.Microsoft.Extensions.AI")     // execute_tool spans from function invocation
            .AddAzureMonitorTraceExporter(options => options.ConnectionString = appInsightsConnectionString);
    });
```

For Azure Application Insights use the `Azure.Monitor.OpenTelemetry.Exporter` package as above; runs then appear in the portal under Transaction search and End-to-end transaction details. The `gen_ai.*` attributes also feed the Application Insights Agents (preview) view: each `invoke_agent` span appears under Agent runs, and the `chat {model}` spans populate the model and token-consumption panels.

## Options

Telemetry is enabled by default (harmless without a subscribed trace provider) and configured through the builder:

```csharp
builder.Services.AddSharpOMaticEngine()
    .AddTelemetry(options =>
    {
        options.Enabled = true;
        options.EnableSensitiveData = false;
    })
    .AddSqliteRepository(connectionString: "...");
```

| Option | Default | Effect |
| --- | --- | --- |
| `Enabled` | `true` | When `false`, no engine spans are created and model call chat clients are not wrapped with the OpenTelemetry middleware. |
| `EnableSensitiveData` | `false` | When `true`, prompts, responses, and tool arguments are recorded on model call spans. Leave off unless the trace backend is trusted with message content. When left off, the `Microsoft.Extensions.AI` middleware still honors the standard `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` environment variable. |
