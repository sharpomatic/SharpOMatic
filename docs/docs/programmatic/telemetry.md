---
title: OpenTelemetry Tracing
sidebar_position: 7
---

The engine emits OpenTelemetry activities (spans) for workflow runs so a host application can view them in any OpenTelemetry backend, such as Azure Application Insights, the Aspire Dashboard, or Jaeger.

## What Is Emitted

All engine spans are created on a single `ActivitySource` named `SharpOMatic.Engine` (exposed as the constant `SharpOMaticDiagnostics.SourceName`).

| Span | When | Key attributes |
| --- | --- | --- |
| `workflow {workflow name}` | One per workflow run, from start until success, failure, or suspension. A run executes a statically authored graph rather than a model-directed loop, so it is deliberately *not* tagged as a GenAI agent invocation — the `gen_ai.*` spans belong to the model calls nested underneath it. | `gen_ai.conversation.id` and `session.id` (conversation id), `sharpomatic.workflow.id`, `sharpomatic.workflow.name`, `sharpomatic.run.id`, `sharpomatic.run.status`, `sharpomatic.usage.input_tokens`, `sharpomatic.usage.output_tokens`, `sharpomatic.model_call.count`, `sharpomatic.model_call.total_cost`, `error.type`, `sharpomatic.failed_node.*` |
| `executor.process {node title}` | One per node execution, parented to the run span. Failed nodes include a standard `exception` event containing the exception type, message, and stack trace. | `sharpomatic.executor.id`, `sharpomatic.executor.type`, `sharpomatic.executor.title`, `sharpomatic.node.status`, `sharpomatic.run.id`, `error.type`, plus node-specific attributes below |
| `invoke_agent {node title}` | One per model call, emitted by the Agent Framework OpenTelemetry middleware that the engine wraps around the agent every model call runs through. A model call with tools is a model-directed loop whose number of provider round trips is not known up front, so this span is what carries the duration and token totals of the whole call. Applied to every model call, tools or not, so the span shape does not change when tools are added to a node. | `gen_ai.operation.name` (`invoke_agent`), `gen_ai.agent.name` (node title), `gen_ai.usage.*` |
| `chat {model}` | One per provider round trip, emitted by the `Microsoft.Extensions.AI` OpenTelemetry middleware that the engine wraps around every model call chat client. A tool-calling model call produces several of these under one `invoke_agent` span. Follows the OpenTelemetry GenAI semantic conventions (`gen_ai.*` attributes including token usage). | `gen_ai.*` |
| `execute_tool {tool name}` | One per tool invocation, emitted by the `Microsoft.Extensions.AI` function invocation middleware. | `gen_ai.tool.*` |

Node executions add type-specific attributes to their `executor.process` span:

| Node type | Attributes |
| --- | --- |
| Model Call | `sharpomatic.model.name`, `sharpomatic.model.config`, `sharpomatic.connector.name`, `sharpomatic.connector.config`, `sharpomatic.model.provider_name`, `sharpomatic.usage.input_tokens`, `sharpomatic.usage.output_tokens`, `sharpomatic.model_call.total_cost`, `sharpomatic.model_call.attempt`, `sharpomatic.model_call.fallback`, `sharpomatic.model_call.try` |
| Switch | `sharpomatic.switch.selected` — the name of the branch that was taken |
| Fan Out | `sharpomatic.fan_out.branch_count` |
| Fan In | `sharpomatic.fan_in.arrival` (this thread's arrival order), `sharpomatic.fan_in.expected`, `sharpomatic.fan_in.completed` (true on the arrival that completed the merge) |
| Batch | `sharpomatic.batch.item_count`, `sharpomatic.batch.batch_size`, `sharpomatic.batch.parallel_batches` |
| Gosub | `sharpomatic.gosub.workflow_id`, `sharpomatic.gosub.workflow_name` |

Custom node implementations deriving from `RunNode<T>` can stamp their own tags through the protected `NodeActivity` property (null when telemetry is disabled or nothing is listening, so always use `NodeActivity?.SetTag(...)`).

## Attribute Naming

Every attribute the engine defines itself is prefixed `sharpomatic.`. The prefix keeps engine attributes out of the namespaces owned by the OpenTelemetry semantic conventions, so an engine attribute can never collide with — or be silently absorbed into — a `gen_ai.*` or other convention attribute that a backend interprets specially.

The only deliberately unprefixed attributes are the conventions the engine participates in on purpose: `gen_ai.conversation.id` and `session.id` (both set from the conversation id, because backends key conversation and session grouping off those exact names) and `error.type`. The `gen_ai.*` attributes on the `invoke_agent`, `chat`, and `execute_tool` spans are set by the Microsoft middleware rather than by the engine, and follow the conventions as published.

:::note Node titles are not unique
`gen_ai.agent.name` on an `invoke_agent` span is the node title, which is user-authored and carries no uniqueness constraint. Two model call nodes sharing a title therefore produce agent spans that a backend will group together — including in the Application Insights Agents (preview) view. Group by the parent node span's `sharpomatic.executor.id` (the node's unique id, alongside the human-readable `sharpomatic.executor.title`) to tell them apart, or give the nodes distinct titles.
:::

The run span is started on the caller's async flow, so it is parented to whatever ambient `Activity` is current when the run starts (for example an ASP.NET Core request or a message-processing span in the host). Node spans execute on engine worker threads and are parented explicitly to the run span. A model call node nests an `invoke_agent` span under its node span, and the `chat` and `execute_tool` spans of the call sit under that, giving three distinct levels: the node span covers what the engine controlled (which attempt, which fallback model), the agent span covers what the model controlled (how many turns it took), and each chat span covers one provider round trip. Retries and fallbacks are handled by the engine outside the agent, so a node that retries produces one `invoke_agent` span per attempt.

Failed runs and nodes set the span status to `Error`. A failed node also records the original exception on its span as an OpenTelemetry `exception` event with `exception.type`, `exception.message`, and `exception.stacktrace`. Exceptions wrapped with a workflow-friendly message preserve the original exception as their inner exception. Exception messages and stack traces can contain sensitive application data or source paths, so hosts should send traces only to a trusted backend. The workflow run and model call metrics recorded in the repository complement the spans.

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

For Azure Application Insights use the `Azure.Monitor.OpenTelemetry.Exporter` package as above; runs then appear in the portal under Transaction search and End-to-end transaction details. The `chat {model}` spans carry the `gen_ai.*` attributes that populate the Application Insights model and token-consumption panels, and each model call appears in the Agents (preview) view under its node title. A workflow run itself is not reported as an agent run, so the run does not appear there — if a host wants the whole run listed as one agent, it should wrap the run in its own `invoke_agent` span describing the agent that owns it.

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
| `Enabled` | `true` | When `false`, no engine spans are created and neither the agent nor the chat client of a model call is wrapped with the OpenTelemetry middleware. |
| `EnableSensitiveData` | `false` | When `true`, prompts, responses, and tool arguments are recorded on model call spans. Leave off unless the trace backend is trusted with message content. When left off, the `Microsoft.Extensions.AI` middleware still honors the standard `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` environment variable. |
