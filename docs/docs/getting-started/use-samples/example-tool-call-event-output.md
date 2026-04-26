---
title: "AG-UI: Example tool call event output"
---

This sample shows backend AG-UI tool call events that can be rendered by the frontend.

## Choose this when

Use this sample when you want a small workflow focused on custom tool-call display behavior.

## What it demonstrates

- Calling a backend tool from a **BackendToolCall** node.
- Passing fixed JSON arguments to a tool.
- Storing the tool result in context.
- Following the tool call with a **Code** node that updates the same result value.

## How it works

The workflow calls the `get_weather` backend tool with `Sydney` as the location and stores the result at `temperature`.
It then runs a Code node that sets `temperature` to a random value, which gives the frontend a tool-call event and a changed context value to display.

## Setup notes

No connector or model is required.
This sample is not conversation-enabled, so it is suitable for learning activity rendering without chat state.
It needs the [Sharpy client sample](../../ag-ui/sharpy.md) because it understands the expected `get_weather` backend tool for rendering.
