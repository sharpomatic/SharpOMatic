---
title: Managing State
sidebar_position: 3
---

Workflow state is the context that flows through your run. It is a JSON-serializable
**ContextObject** that you can initialize at run start, read and update inside nodes,
and retrieve when the run completes.

Common state values include tenant identifiers, user identifiers, correlation IDs,
and any other input required by your backend or tools.

## State And Context Basics

State is stored in a **ContextObject**, which is a dictionary-like container with
path helpers (**Get**, **Set**, **TryGet**, **TrySet**). It must be JSON-serializable so that
SharpOMatic can persist and resume runs.

Use nested paths to keep your state organized.

```csharp
var context = new ContextObject();
context.Set("input.tenantId", "tenant-42");
context.Set("input.userId", "user-9");
context.Set("input.correlationId", Guid.NewGuid().ToString("N"));
context.Set("input.requestedAt", DateTimeOffset.UtcNow);
```

For more detail on path rules and JSON requirements, see the [Context](../core-concepts/context.md) page.

## Passing State Into A Run

All run start methods accept an optional **ContextObject**. That context becomes the
initial state for the workflow.

```csharp
var engine = serviceProvider.GetRequiredService<IEngineService>();
var workflowId = await engine.GetWorkflowId("Tenant Summary");

var context = new ContextObject();
context.Set("input.tenantId", "tenant-42");
context.Set("input.userId", "user-9");
context.Set("input.prompt", "Summarize the latest support tickets.");

var completed = await engine.StartWorkflowRunAndWait(workflowId, context);
```

## Accessing State In Code Nodes

Code nodes run with a global named **Context** (the **ContextObject**) and a global
named **ServiceProvider** (for your own backend services).
Return values are ignored, so write results back to **Context**.

```csharp
// Code node script
var tenantId = Context.Get<string>("input.tenantId");
var userId = Context.Get<string>("input.userId");

var customerService = ServiceProvider.GetRequiredService<ICustomerService>();
var profile = await customerService.GetProfileAsync(tenantId, userId);

Context.Set("customer.profile", profile);
Context.Set("audit.codeNodeCalled", true);
```

If your code node needs access to backend assemblies or namespaces, add them when
configuring the engine:

```csharp
builder.Services.AddSharpOMaticEngine()
  .AddScriptOptions([typeof(CustomerService).Assembly], ["MyCompany.Backend"]);
```

## Tool Calling And Context

Tool methods can accept an **IServiceProvider** parameter. During tool execution,
SharpOMatic makes the current **ContextObject** available via DI, so you can read or
write state the same way as in a code node.

```csharp
public static class TenantTools
{
  [Description("Lookup a tenant plan name.")]
  public static string GetTenantPlan(IServiceProvider services)
  {
    var context = services.GetRequiredService<ContextObject>();
    var tenantId = context.Get<string>("input.tenantId");

    var plan = services.GetRequiredService<ITenantService>().GetPlanName(tenantId);
    context.Set("tenant.plan", plan);
    context.Set("audit.toolCalled", true);

    return plan;
  }
}
```

Register tool methods during setup:

```csharp
builder.Services.AddSharpOMaticEngine()
  .AddToolMethods(TenantTools.GetTenantPlan);
```

## Getting State Back Out

The final context is returned on the `Run` after completion as a JSON string.
Use `ContextObject.Deserialize` to get the structured object back.

```csharp
var completed = await engine.StartWorkflowRunAndWait(workflowId, context);
if (completed.RunStatus == RunStatus.Success)
{
  var jsonConverters = serviceProvider.GetRequiredService<IJsonConverterService>();
  var output = ContextObject.Deserialize(completed.OutputContext, jsonConverters);

  var plan = output.Get<string>("tenant.plan");
  var summary = output.Get<string>("output.text");
}
```

## End-To-End Example

This example shows a tenant/user-aware run. The host passes tenant and user IDs,
the code node enriches state, a tool call reads that state, and the host retrieves
the final output.

```csharp
// Host code: start a run with tenant/user state
var engine = serviceProvider.GetRequiredService<IEngineService>();
var workflowId = await engine.GetWorkflowId("Tenant Summary");

var context = new ContextObject();
context.Set("input.tenantId", "tenant-42");
context.Set("input.userId", "user-9");
context.Set("input.prompt", "Summarize this user's recent activity.");

var completed = await engine.StartWorkflowRunAndWait(workflowId, context);
```

```csharp
// Code node script: enrich state for downstream nodes/tools
var tenantId = Context.Get<string>("input.tenantId");
var userId = Context.Get<string>("input.userId");

var customerService = ServiceProvider.GetRequiredService<ICustomerService>();
var profile = await customerService.GetProfileAsync(tenantId, userId);
Context.Set("customer.profile", profile);
```

```csharp
// Tool method: uses state and writes back to context
public static class TenantTools
{
  [Description("Lookup a tenant plan name.")]
  public static string GetTenantPlan(IServiceProvider services)
  {
    var context = services.GetRequiredService<ContextObject>();
    var tenantId = context.Get<string>("input.tenantId");

    var plan = services.GetRequiredService<ITenantService>().GetPlanName(tenantId);
    context.Set("tenant.plan", plan);
    return plan;
  }
}
```

```csharp
// Host code: read output state
if (completed.RunStatus == RunStatus.Success)
{
  var jsonConverters = serviceProvider.GetRequiredService<IJsonConverterService>();
  var output = ContextObject.Deserialize(completed.OutputContext, jsonConverters);
  var plan = output.Get<string>("tenant.plan");
  var summary = output.Get<string>("output.text");
}
```
