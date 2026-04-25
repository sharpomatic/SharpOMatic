---
title: Stateless simple chatbot
---

This sample is the smallest chatbot workflow: it sends the latest user message to a model and returns the model response without preserving conversation state.

## Choose this when

Use this sample when you want to understand the minimum setup for a model-backed workflow or when each request can be handled independently.

## What it demonstrates

- A **Start** node connected directly to a **ModelCall** node.
- Using `{{$agent.latestUserMessage.content}}` as the model prompt.
- Producing text output from a non-conversation workflow.

## How it works

The workflow starts, passes the latest user message into the **ModelCall** node, and lets the configured model generate a text response.
Because conversation mode is disabled, the workflow does not keep prior turns as state.

## Setup notes

Create a connector and model before running this sample, then select that model in the **ModelCall** node.
