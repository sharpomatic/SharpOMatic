---
title: Code Node
sidebar_position: 4
---

The code node runs a C# script against the current workflow context.
Use it for glue logic, calling backend services, or complex data transformations.

## Access the Context

The script runs with a single global named **Context**, which is a **ContextObject**.
Read and write values using **Get**, **Set**, or the standard dictionary and list APIs.
Return values from the script are ignored, so update **Context** to persist results.

<img src="/img/code_example.png" alt="Mandatory paths" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

There are also **TrySet** and **TryGet** variations for scenarios where you do not know if they will succeed.

<img src="/img/code_short.png" alt="Mandatory paths" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

## Asynchronous Code

You can perform await operations on asynchronous code in the usual way.
It is recommended to use async operations when possible to free up the thread for slow operations.
For example, file and network calls are typically quite slow and benefit from this approach.

<img src="/img/code_async.png" alt="Mandatory paths" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

## Stream Events

Code nodes also expose an **Events** helper for publishing workflow stream events.
These events are persisted to run or conversation stream history and can be consumed by live clients such as the editor or AG-UI.

```csharp
await Events.AddTextMessageAsync(StreamMessageRole.Assistant, "message-1", "Hello from the workflow");
```

Text stream helpers support `StreamMessageRole.User`, `Assistant`, `Developer`, `System`, and `Tool`.
Visible reasoning uses the dedicated reasoning helpers rather than `AddTextMessageAsync`.

All `Events.Add*` helpers now accept an optional `silent` flag.
Set `silent: true` when the event should still be recorded in SharpOMatic stream history but should be suppressed from AG-UI live SSE output.
This flag is transient and is not stored in the database.

```csharp
await Events.AddTextMessageAsync(
    StreamMessageRole.User,
    "user-1",
    "What is the order status?",
    silent: true
);
```

The same helper surface also supports reasoning and tool-call lifecycles.
For example, this emits a complete visible reasoning message:

```csharp
await Events.AddReasoningMessageAsync("reason-1", "Thinking about the next step");
```

If you need the full reasoning lifecycle yourself, use the lower-level helpers:

```csharp
await Events.AddReasoningStartAsync("reason-1");
await Events.AddReasoningMessageStartAsync("reason-1");
await Events.AddReasoningMessageContentAsync("reason-1", "Thinking about the next step");
await Events.AddReasoningMessageEndAsync("reason-1");
await Events.AddReasoningEndAsync("reason-1");
```

Tool calls are also supported.
For example, this emits a tool call for the frontend to handle and return later:

```csharp
await Events.AddToolCallAsync(
    "call-1",
    "lookup_weather",
    "{\"city\":\"Sydney\"}",
    "assistant-1"
);
```

If your workflow already has the tool result, emit the full lifecycle in one call:

```csharp
await Events.AddToolCallWithResultAsync(
    "call-1",
    "lookup_weather",
    "{\"city\":\"Sydney\"}",
    "tool-result-1",
    "Sunny",
    "assistant-1",
    silent: true
);
```

Activity messages are also supported for AG-UI-compatible frontends.
Use a snapshot to publish the current activity payload:

```csharp
await Events.AddActivitySnapshotAsync(
    "plan-1",
    "PLAN",
    new { steps = new[] { new { title = "Search", status = "in_progress" } } },
    replace: false
);
```

Use a delta to apply RFC 6902 JSON Patch updates to an existing activity:

```csharp
await Events.AddActivityDeltaAsync(
    "plan-1",
    "PLAN",
    new object[] { new { op = "replace", path = "/steps/0/status", value = "done" } }
);
```

For activity state that already lives in workflow context, the higher-level sync helper is usually simpler because SharpOMatic stores the previous snapshot in hidden workflow state, computes the JSON Patch for you, and automatically falls back to a replacement snapshot when the patch would be larger:

```csharp
await Events.AddActivitySyncFromContextAsync(
    "plan-1",
    "PLAN",
    "activity.plan",
    replace: false
);

await Events.AddActivitySyncFromContextAsync(
    "plan-1",
    "PLAN",
    "activity.plan"
);
```

On the first call, this emits an activity snapshot. Later calls emit either an activity delta or, if the delta would be larger, a replacement snapshot.
Pass `snapshotsOnly: true` to force a snapshot on every sync call:

```csharp
await Events.AddActivitySyncFromContextAsync(
    "plan-1",
    "PLAN",
    "activity.plan",
    snapshotsOnly: true
);
```

Use the lower-level activity helpers only when you want full control over the emitted payload or patch shape.

State messages are also supported for AG-UI-compatible frontends.
Use a snapshot to publish the full current `agent.state` payload:

```csharp
await Events.AddStateSnapshotAsync(
    new { mode = "assistant", count = 1 }
);
```

Use a delta to apply RFC 6902 JSON Patch updates to the current state:

```csharp
await Events.AddStateDeltaAsync(
    new object[] { new { op = "replace", path = "/mode", value = "review" } }
);
```

If your workflow already keeps state in `agent.state`, prefer the higher-level sync helper:

```csharp
Context.Set("agent.state.mode", "assistant");
await Events.AddStateSyncAsync();

Context.Set("agent.state.mode", "review");
await Events.AddStateSyncAsync();
```

This compares the current `agent.state` value to hidden baseline state at `agent._hidden.state` and emits either `StateDelta` or `StateSnapshot`.
Pass `snapshotsOnly: true` to force a snapshot on every sync call:

```csharp
await Events.AddStateSyncAsync(snapshotsOnly: true);
```

If the frontend only needs a simple progress phase marker, emit AG-UI-compatible step lifecycle events:

```csharp
await Events.AddStepStartAsync("Search");
await Events.AddStepEndAsync("Search");
```

For application-specific AG-UI protocol extensions, emit a custom event:

```csharp
await Events.AddCustomEventAsync(
    "weather_progress",
    "{\"stage\":\"fetch\"}"
);
```

This stores the custom event name in the stream event `TextDelta` field and the custom value string in `Metadata`.

## Implicit Assemblies

The following using statements are implicitly already applied:

```csharp
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text.Json;
  using System.Net.Http;
  using System.IO;
  using System.Threading.Tasks;
  using Microsoft.Extensions.AI;
  using SharpOMatic.Engine.Contexts;
```

You can access the types defined in these namespaces.

## Additional Assembly References

If you need to access assemblies or types not already implicitly added, you can do so easily.
When adding the SharpOMatic engine in program setup, add the **AddScriptOptions** extension.
The first parameter is a list of additional assemblies that should be made available.
Note that the referenced assembly must be part of the owning project, so you might need to add it to your project references.

In the following example, we add the demo server assembly by specifying one of the types within it so the entire assembly can be found.
The second parameter is the list of extra namespaces to add.
Here we have the namespace of the demo server.

```csharp
  builder.Services.AddSharpOMaticEngine()
    .AddScriptOptions([typeof(ClassExample).Assembly], ["SharpOMatic.DemoServer"]);
```

## Code compilation

Your code will automatically be checked to ensure it is valid.
The familiar red error line will appear in the place the code has a problem.
Do not ignore these because it indicates the code will fail at runtime.
As noted above, you can add access to additional assemblies and types if you want to access backend-specific code.

## Custom types and JsonConverter

The context must be serializable to and from JSON so that it can be saved to the database. 
This restriction allows a workflow to be suspended and restarted and allows intermediate states to be recorded to help with debugging.

Scalar values persist automatically with standard JSON serialization.
Classes require a converter. 
You can register additional types from your project so they can be added to the context and persisted.
Here is a trivial class definition.

```csharp
  public class ClassExample
  {
      public required bool Success { get; set; }
      public required string ErrorMessage { get; set; }
      public required int[] Scores { get; set; }
  }
```

Now you need to implement a **JsonConverter** for it.

```csharp
  public sealed class ClassExampleConverter : JsonConverter<ClassExample>
  {
      public override ClassExample? Read(ref Utf8JsonReader reader, 
                                         Type typeToConvert, 
                                         JsonSerializerOptions options)
          => JsonSerializer.Deserialize<ClassExample>(ref reader, Clean(options));

      public override void Write(Utf8JsonWriter writer, 
                                 ClassExample value, 
                                 JsonSerializerOptions options)
          => JsonSerializer.Serialize(writer, value, Clean(options));

      private JsonSerializerOptions Clean(JsonSerializerOptions options)
      {
          var inner = new JsonSerializerOptions(options);
          inner.Converters.Remove(this);
          return inner;
      }
  }
```

Provide the implementation type in the SharpOMatic setup and ensure the assembly and namespace are available to code execution.

```csharp
  builder.Services.AddSharpOMaticEngine()
    .AddJsonConverters(typeof(ClassExampleConverter))
    .AddScriptOptions([typeof(ClassExample).Assembly], ["SharpOMatic.DemoServer"]);
```

Now we can access the class and add it to a context inside the **Code** node.

<img src="/img/code_types.png" alt="Mandatory paths" width="900" style={{ maxWidth: '100%', height: 'auto' }} />
