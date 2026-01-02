---
title: Context
sidebar_position: 1
---

Context is the runtime data that flows through a workflow. Every node reads from and writes to context so downstream nodes can use the results of earlier steps.

## Context types

SharpOMatic uses a small set of JSON-serializable context containers:

- `ContextObject`: a JSON object with named properties.
- `ContextList`: a JSON array of values or objects.
- `RunContext`: run-level state shared across all threads in a workflow run.
- `ThreadContext`: per-thread state used by fan-out and parallel execution.

## Why it matters

- Context is persisted with runs, so values must be JSON-serializable.
- Validation and error reporting depend on context structure.
- Editors and nodes rely on context shape for mapping inputs and outputs.

## Example

```json
{
  "input": {
    "prompt": "Summarize the latest run notes",
    "maxTokens": 256
  },
  "results": [
    {
      "nodeId": "model-call",
      "summary": "..."
    }
  ]
}
```

## C# example

```csharp
using SharpOMatic.Engine.Contexts;

var context = new ContextObject
{
    ["input"] = new ContextObject
    {
        ["prompt"] = "Summarize the latest run notes",
        ["maxTokens"] = 256
    },
    ["results"] = new ContextList
    {
        new ContextObject
        {
            ["nodeId"] = "model-call",
            ["summary"] = "..."
        }
    }
};
```

Use this model as the baseline when designing new nodes or integrating external services.
