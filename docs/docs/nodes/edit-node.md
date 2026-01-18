---
title: Edit Node
sidebar_position: 3
---

The edit node adds, updates, or deletes entries in the workflow context.
Use it to shape data between other node operations.
For example, adding constant values or removing intermediate values that are no longer needed.
If you need more advanced logic then update the context using a **Code** node.

## Upserts

Upserts add a new value or replace an existing one at the specified path.
Each upsert entry requires a path and a value.
You can choose from **bool**, **int**, **double**, and **string** as basic scalar types.
Other type options are described below.
Invalid paths or values (for example, bad JSON or an invalid list index) cause the run to fail.

<img src="/img/edit_scalar.png" alt="Mandatory paths" width="900" style={{ maxWidth: '100%', height: 'auto' }} />

## Deletes

Deletes remove the value at the specified path if it exists.
Missing or invalid paths are ignored, but an empty path causes the run to fail.

## Default assets

You can specify a single asset or a list of assets from the asset library.
In both cases, these come only from the asset library and are the same each time you run the workflow.
You cannot add run assets this way; add run assets programmatically by inserting them directly into the context.

<img src="/img/edit_assets.png" alt="Assets values" width="900" style={{ maxWidth: '100%', height: 'auto' }} />

## Default JSON

Initializing from JSON is a fast way to add many values at once.
The JSON can be as simple as a single value or a large and complex hierarchy.
It is easier to apply a JSON block than using many different path initializations for individual values.
Integer numbers are turned into the C# **int** type, fractional numbers into **double**, an object becomes a **ContextObject**, and a list becomes a **ContextList**.

<img src="/img/edit_json.png" alt="JSON value" width="900" style={{ maxWidth: '100%', height: 'auto' }} />

The above block will be parsed and inserted into the context as if the following code was written:

```csharp
    Context.Set("input.json", new ContextObject());
    Context.Set("input.json.text", "Capital of Australia?");
    Context.Set("input.json.number", 42);
    Context.Set("input.json.list", new ContextList());
    Context.Set("input.json.list[0]", 1);
    Context.Set("input.json.list[1]", true);
    Context.Set("input.json.list[2]", "foorbar");
```

## Default expressions

If one of the listed types is not appropriate, you can use a C# expression to return a value.
In the following example, the expression returns the current date and time.
This works because the returned type **DateTimeOffset** is a scalar and all scalars can be persisted to and from JSON without additional work.
Returning a class would require you to add an appropriate **JsonConverter** to the SharpOMatic setup during program setup.

<img src="/img/edit_expression.png" alt="Expression value" width="900" style={{ maxWidth: '100%', height: 'auto' }} />
