---
title: Context
sidebar_position: 4
---

Context is the runtime data that flows through a workflow. It provides inputs, carries intermediate state, and returns outputs.

- Nodes read from and write to context so downstream steps can use earlier results.
- An initial context can be passed into a workflow to provide input parameters.
- When a workflow run completes, the final context is returned as the result.

## Context Types

SharpOMatic uses a small set of JSON-serializable context containers:

- **ContextObject**: a dictionary of property names to values.
- **ContextList**: a dynamic list of values.

The context is always an instance of **ContextObject**.
You can add **ContextObject** and **ContextList** instances as values, allowing the creation of arbitrary hierarchies.
Values can also be any of the standard scalar types in C# such as **string**, **bool**, **int**, **DateTimeOffset**, and so forth.

```csharp
  var context = new ContextObject
  {
      ["input"] = new ContextObject
      {
          ["prompt"] = "Summarize the latest run notes",
          ["maxTokens"] = 256
      },
      ["output"] = new ContextList
      {
          new ContextObject
          {
              ["pi"] = 3.14,
              ["mixed"] = new ContextList { 1, true, "string" }
          }
      }
  };
```

## Dictionary and List

**ContextObject** is a **Dictionary** with additional checks, and **ContextList** is a **List** with additional checks.
You can use the usual **Dictionary** and **List** methods and properties.

```csharp
  var list = new ContextList();
  list.AddRange([3.14, true, DateTime.Now]);

  var context = new ContextObject();
  context.Add("myList", list);
  context.Add("summary", "No additional notes.");
  context.Remove("notes");
  var count = context.Count;
```

## Path Accessors

There are some additional helper methods for handling nested values.

```csharp
  var context = new ContextObject();

  context.Set<bool>("first", false);
  context.Set<string>("second.third", "FooBar");
  context.Set<ContextList>("second.fourth", new ContextList() { 0 });
  context.Set<double>("second.fourth[0]", 3.14);

  var boolean = context.Get<bool>("first");
  var text = context.Get<string>("second.third");
  var pi = context.Get<double>("second.fourth[0]");
```

- Use **Set** with a path to traverse down objects and lists to set values. If an intermediate object is missing, it is created for you. For example, the path _second.third_ refers to a _second_ property that does not yet exist, so **Set** creates it as a **ContextObject** and adds the _third_ property with the _FooBar_ value.
- **ContextList** values can be traversed using standard index notation, for example _[4]_. The list does not automatically create instances, so the referenced index must exist.
- Use **Get** to recover values using a path. 
- Both methods take a type so that the provided or returned value is strongly typed.

There are also **TrySet** and **TryGet** variations for scenarios where you do not know if they will succeed.

```csharp
  var context = new ContextObject();

  var setResult = context.TrySet<bool>("second.third", false);
  var getResult = context.TryGet<bool>("second.third", out var first);
```

## Property Names

One of the additional checks enforced by **ContextObject** is that property names (the dictionary keys) must be valid C# identifiers.
For example, they cannot be an empty string, cannot start with a number or contain a fullstop, plus the other rules for regular C# variable names.

## JSON Serialization

The context must be serializable to and from JSON so that it can be saved to the database.
This restriction allows a workflow to be suspended and restarted and allows intermediate states to be recorded to help with debugging.
Standard scalar types have been setup to work automatically.
You can register additional types from your project so they can be added to the context and persisted.

Here is an example class definition.

```csharp
  public class ClassExample
  {
      public required bool Success { get; set; }
      public required string ErrorMessage { get; set; }
      public required int[] Scores { get; set; }
  }
```

Now you need to implement a **IJsonConverter** for it.

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

Provide the implementation type in the SharpOMatic setup.

```csharp
  builder.Services.AddSharpOMaticEngine()
    .AddJsonConverters(typeof(ClassExampleConverter))
```

For additional information, see the [JSON Serialization](../programmatic/JSON-serialization.md) section.
