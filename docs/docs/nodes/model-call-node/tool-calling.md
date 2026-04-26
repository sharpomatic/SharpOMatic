---
title: Tool Calling
sidebar_position: 6
---

If the model supports tool calling, this tab is available.

<img src="/img/modelcall-tools.png" alt="Asset Substitution" width="600" style={{ maxWidth: '100%', height: 'auto' }} />

## Available tools

During program setup, you can use the **AddToolMethods** extension to specify a list of C# static methods available for calling.
Use a comma-separated list if you need to specify more than one method.
You do not have to provide all the tools for every call. Use the checkboxes to select only those you want to make available during this model call.

```csharp
  builder.Services.AddSharpOMaticEngine()
     .AddToolMethods(ToolCalling.GetGreeting, ToolCalling.GetTime);
```

Example tool call implementations.

```csharp
    namespace SharpOMatic.DemoServer;

    public static class ToolCalling
    {
      [Description("Get a friendly greeting.")]
      public static string GetGreeting(IServiceProvider services)
      {
        var context = services.GetRequiredService<ContextObject>();
        context.Set("GetGreetingCalled", true);
        return "Howdy doody!";
      }

      [Description("Get current time")]
      public static string GetTime(IServiceProvider services)
      {
        var context = services.GetRequiredService<ContextObject>();
        context.Set("GetTimeCalled", true);
        return DateTimeOffset.Now.ToString();
      }
    }
```

### IServiceProvider

Notice that your methods can take **IServiceProvider** as a parameter.
This allows you to get required data from the service provider.
This example demonstrates how the workflow context for the model node can be accessed and modified.
You could also request other interfaces relating to your own backend services.

## Parallel tool calls

Some model providers can make multiple tool call requests in a single model reply.
This is called parallel tool calling and is more efficient than round-tripping for each individual tool call.
Models offering this capability allow you to use this checkbox to turn it on or off.

## Runtime stream events

When a model call performs tool calling, SharpOMatic now emits tool-call stream events as part of the live run output.
These appear in the stream tab and are also translated to AG-UI protocol events when using the AG-UI endpoint.
If the node has **Disable Tool Events** enabled on its [**Stream** tab](./stream.md), these protocol-style tool stream events are suppressed for that model call, but the tool-call trace information is still recorded.

The tool-call stream lifecycle is:

- `ToolCallStart`
- `ToolCallArgs`
- `ToolCallEnd`
- `ToolCallResult`

The trace viewer still records tool calls as information entries as well, so you keep both the protocol-friendly stream events and the trace summary.

## Tool choice

There are only two options for this dropdown.

- **None**: the list of tools is provided, but the model is not allowed to invoke any.
This is useful if you want the model to create a plan of what to do but not actually execute that plan.
In that case, it needs to know the availability and signature of the tools to plan.

- **Auto**: the model can decide to invoke zero or more tools.
