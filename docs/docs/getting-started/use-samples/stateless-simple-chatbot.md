---
title: "AG-UI: Stateless simple chatbot"
---

This sample is the smallest and simplest AG-UI chatbot workflow.
The client is expected to send all the conversation history to the workflow.
It then uses the latest user message from the history and uses it as the model prompt.

## Choose this when

Use this sample when you want to understand the minimum setup for a model-backed workflow or when each request can be handled independently.

## What it demonstrates

- A **Start** node connected directly to a **ModelCall** node.
- Using `{{$agent.latestUserMessage.content}}` as the model prompt.
- Producing text output from a non-conversation workflow.

## How it works

The workflow starts, passes the latest user message into the **ModelCall** node, and lets the configured model generate a text response.
Because conversation mode is disabled, the workflow does not keep prior turns as state.
Therefore, the client must send the entire message history on each call so the model call has that history.

## Setup notes

Create a connector and model before running this sample, then select that model in the **ModelCall** node.
When using the [Sharpy client sample](../../ag-ui/sharpy.md), make sure you check the **Send All Messages** checkbox and put the workflow ID into the input with the same name.
