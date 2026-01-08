---
title: Start Node
sidebar_position: 1
---

The start node is the entry point for every workflow.
Each workflow must have exactly one start node.

## Input context

The start node receives the initial context that is supplied via the editor or programmatic invocation.
If you do not supply a context, an empty context is created automatically.
If your workflow does not need to enforce inputs, leave the node unchanged.

## Mandatory values

You can choose to initialize the context.
This allows you to enforce mandatory entries.
Simply turn on the **Initialize the context** checkbox and then add a list of paths that must be present.
Note that it does not check the type, only the presence of a value.

<img src="/img/start_mandatory.png" alt="Mandatory paths" width="900" style={{ maxWidth: '100%', height: 'auto' }} />

## Optional values

If you specify a default value, the value becomes optional.
When a value is provided it will always be used; otherwise, the defined default value is applied.

You can choose from **bool**, **int**, **double**, and **string** as basic scalar types.
Other type options are described below.

<img src="/img/start_optional.png" alt="Optional paths" width="900" style={{ maxWidth: '100%', height: 'auto' }} />

## Default assets

You can specify a single asset or a list of assets from the asset library.
In both cases, these come only from the asset library and are the same each time you run the workflow.
You cannot add run assets this way; add run assets programmatically by inserting them directly into the context.

<img src="/img/start_assets.png" alt="Assets values" width="900" style={{ maxWidth: '100%', height: 'auto' }} />

## Default JSON

Initializing from JSON is a fast way to add many values at once.
The JSON can be as simple as a single value or a large and complex hierarchy.
It is easier to apply a JSON block than using many different path initializations for individual values.
Integer numbers are turned into the C# **int** type, fractional numbers into **double**, an object becomes a **ContextObject**, and a list becomes a **ContextList**.

<img src="/img/start_json.png" alt="JSON value" width="900" style={{ maxWidth: '100%', height: 'auto' }} />

The above block will be parsed and inserted into the context as if the following code was written:

```csharp
    Context.Set("input.json", new ContextObject());
    Context.Set("input.json.text", "Capital of England?");
    Context.Set("input.json.number", 42);
    Context.Set("input.json.list", new ContextList());
    Context.Set("input.json.list[0]", 1);
    Context.Set("input.json.list[1]", true);
    Context.Set("input.json.list[2]", "foobar");
```

## Default expressions

If one of the listed types is not appropriate, you can use a C# expression to return a value.
In the following example, the expression returns the current date and time.
This works because the returned type **DateTimeOffset** is a scalar and all scalars can be persisted to and from JSON without additional work.
Returning a class would require you to add an appropriate **JsonConverter** to the SharpOMatic setup during program setup.

<img src="/img/start_expression.png" alt="Expression value" width="900" style={{ maxWidth: '100%', height: 'auto' }} />
