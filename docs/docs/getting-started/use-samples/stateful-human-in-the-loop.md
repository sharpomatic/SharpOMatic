---
title: "AG-UI: Stateful human in the loop"
---

This sample is a human-in-the-loop chatbot that asks the user a frontend question before continuing to a model response.

## Choose this when

Use this sample when a workflow needs explicit user input, approval, or a UI choice before the model continues.

## What it demonstrates

- A **FrontendToolCall** node that asks a question in the UI.
- A stateful conversation workflow that can resume after user input.
- Routing successful tool input to a **ModelCall** node.
- Sending a fallback assistant message from a **Code** node when the input does not match the expected tool result path.

## How it works

The workflow starts by calling the frontend `ask_a_question` tool and stores the result at `output.toolResult`.
If the tool result is returned, execution continues into the **ModelCall** node.
Other input follows the alternate branch and emits a simple assistant error message.
The frontend tool call does not write its function call or result into `input.chat`; the returned choice is workflow control-flow data rather than model tool-call history.

## Setup notes

Create a connector and model before running this sample, then select that model in the **ModelCall** node.
It needs the [Sharpy client sample](../../ag-ui/sharpy.md) because it understands the `ask_a_question` tool call and how to present it to the user.
When using Sharpy, make sure you uncheck the **Send All Messages** checkbox and put the workflow ID into the input with the same name.
