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
| `invoke_agent {node title}` | One per model call, emitted by the Agent Framework OpenTelemetry middleware that the engine wraps around the agent every model call runs through. A model call with tools is a model-directed loop whose number of provider round trips is not known up front, so this span is what carries the duration of the whole call. Applied to every model call, tools or not, so the span shape does not change when tools are added to a node. Its token usage is deliberately removed — see [Token Usage Is Recorded Once](#token-usage-is-recorded-once). | `gen_ai.operation.name` (`invoke_agent`), `gen_ai.agent.name` (node title), `gen_ai.agent.id` (node id) |
| `chat {model}` | One per provider round trip, emitted by the `Microsoft.Extensions.AI` OpenTelemetry middleware that the engine wraps around every model call chat client. A tool-calling model call produces several of these under one `invoke_agent` span. Follows the OpenTelemetry GenAI semantic conventions (`gen_ai.*` attributes including token usage). | `gen_ai.*` |
| `execute_tool {tool name}` | One per tool invocation, emitted by the `Microsoft.Extensions.AI` function invocation middleware on the engine's own source, so registering that source is all a host needs to see tool spans. | `gen_ai.tool.*` |

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

## Token Usage Is Recorded Once

Model call spans nest: a node span contains an `invoke_agent` span, which contains one `chat` span per provider round trip. Each layer observes the same underlying model calls, so if every layer reported its own token usage, a backend summing `gen_ai.usage.*` would count the same tokens several times over — and because the wrapper spans carry no model name, the duplicates would land in an unattributed bucket rather than showing up as an obvious error.

The engine therefore makes the **`chat` spans the single source of GenAI token usage**:

| Span | Token usage | Why |
| --- | --- | --- |
| `chat {model}` | `gen_ai.usage.*` | One per provider round trip, each carrying its own model. This is the billing truth: it includes the round trips that a retry or fallback later discarded. |
| `invoke_agent {node title}` | none — stripped by the engine | Spans exactly the `chat` spans beneath it, so its usage is always a duplicate of their sum. |
| `executor.process {node title}` | `sharpomatic.usage.*` | The engine's own accounting for a model call node, under the `sharpomatic` prefix so it never joins a `gen_ai` sum. Counts only the attempt that succeeded, so it is lower than the `chat` total whenever calls were retried. |
| `workflow {workflow name}` | `sharpomatic.usage.*` | Whole-run totals, likewise outside the `gen_ai` namespace. |

A backend can therefore chart `sum(gen_ai.usage.input_tokens) by gen_ai.request.model` with no span filtering and get a correct, fully attributed answer.

:::note Do not register the function invocation source
`execute_tool` spans arrive on the engine's own source, so registering it is all a host needs for tool-level tracing. The `Microsoft.Extensions.AI` function invocation middleware *also* declares an `Experimental.Microsoft.Extensions.AI` source, which carries an `orchestrate_tools` span wrapping a whole tool-calling loop and reporting that loop's token usage without a model name. Adding `AddSource("Experimental.Microsoft.Extensions.AI")` therefore reintroduces duplicate, unattributed usage while adding nothing to tool visibility. Earlier versions of this page recommended registering it; remove that line if your host still has it.
:::

## Attribute Naming

Every attribute the engine defines itself is prefixed `sharpomatic.`. The prefix keeps engine attributes out of the namespaces owned by the OpenTelemetry semantic conventions, so an engine attribute can never collide with — or be silently absorbed into — a `gen_ai.*` or other convention attribute that a backend interprets specially.

The only deliberately unprefixed attributes are the conventions the engine participates in on purpose: `gen_ai.conversation.id` and `session.id` (both set from the conversation id, because backends key conversation and session grouping off those exact names) and `error.type`. The `gen_ai.*` attributes on the `invoke_agent`, `chat`, and `execute_tool` spans are set by the Microsoft middleware rather than by the engine, and follow the conventions as published.

:::note Node titles are not unique
`gen_ai.agent.name` on an `invoke_agent` span is the node title, which is user-authored and carries no uniqueness constraint. The engine therefore supplies the node's id as the agent id, so the middleware also records `gen_ai.agent.id` and appends it to the span name (`invoke_agent Ask model(3f2a…)`). Two model call nodes sharing a title stay distinct in a backend — group by `gen_ai.agent.id`, or by the parent node span's `sharpomatic.executor.id`, rather than by the agent name.
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
