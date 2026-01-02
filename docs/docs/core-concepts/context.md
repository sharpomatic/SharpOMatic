---
title: Context
sidebar_position: 1
---

Context is the runtime data that flows through a workflow. 
Nodes can read from and write to context so that downstream nodes can use the results of earlier steps.
A context can be passed into a workflow providing the parameters it needs as input.
When a workflow run completes it passes back a context so you can access the results.

## Context Types

SharpOMatic uses a small set of JSON-serializable context containers:

- **`ContextObject`**: a dictionary of property names to values.
- **`ContextList`**: a dynamic list of values.

The context is always an instance of **`ContextObject`**. 
You can add **`ContextObject`** and **`ContextList`** instances as values allowing the creation of arbitrary hierarchies.
Values can also be any of the standard scalar types in C# such as **`string`**, **`bool`**, **`int`**, **`DateTimeOffset`** and so forth.

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

A **`ContextObject`** is just a **`Dictionary`** with some extra checks added.
Likewise, the **`ContextList`** is just a **`List`** with additional checks.
So you can use all the usual **`Dictionary`** and **`List`** methods and properties.

```csharp
  var list = new ContextList();
  list.AddRange([3.14, true, DateTime.Now]);

  var context = new ContextObject();
  context.Add("myList", list);
  context.Add("summary", "No additiona notes.");
  context.Remove("notes");
  var count = context.Count;
```

## Path Accessors

There are some additional helper methods appropriate for handling nested values.

Use **`Set`** with a path to traverse down objects and lists to set values.
It has the useful logic that if an intermediate object is missing it will create it for you.
For example, in the below code, the path _second.third_ refers to a _second_ property that does not yet exist.
It will automatically be created as a **`ContextObject`** and then the _third_ property added inside it with the _FooBar_ value.

**`ContextList`** values can be traversed using the standard index notation, e.g. _[4]_.
Note that for the list it does not automatically create instances, so the referenced index must exist.

Use **`Get`** to recover values using a path. 
Both methods take a type so that the provided or returned value are strongly types.

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

There are also **`TrySet`** and **`TryGet`** variations for scenarios where you do not know if they will succeed.

```csharp
  var context = new ContextObject();

  var setResult = context.TrySet<bool>("second.third", false);
  var getResult = context.TryGet<bool>("second.third", out var first);
```

## Property Names

One of the additional checks enforced by the **`ContextObject`** is that property names (the dictionary keys) must be valid C# identifier names.
You cannot have a property name of an empty string or a name starting with numbers. It must have the same format as regular C# variable names.

## JSON Serialization

The context must be serializable to and from JSON so that it can be saved to the database.
This restriction allows a workflow to be suspended and restarted and allows intermediate states to be recorded to help with debugging.

You can always add scalar values as they persist automatically with JSON-serialization but classes do not.
There is a mechanism provided that allows you specify additional classes from your own project so that they are allowed to be added into the context and be appropriately persisted.

Here is a trivial class definition.

```csharp
  public class ClassExample
  {
      public required bool Success { get; set; }
      public required string ErrorMessage { get; set; }
      public required int[] Scores { get; set; }
  }
```

Now you need to implment a **`JsonConverter`** for it.

```csharp
  public sealed class ClassExampleConverter : JsonConverter<ClassExample>
  {
      public override ClassExample? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
          => JsonSerializer.Deserialize<ClassExample>(ref reader, Clean(options));

      public override void Write(Utf8JsonWriter writer, ClassExample value, JsonSerializerOptions options)
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

