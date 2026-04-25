---
title: Stateful simple chatbot
---

This sample is a minimal conversation-enabled chatbot that keeps chat history across turns.

## Choose this when

Use this sample when you want a basic chatbot that can remember previous messages in the same conversation.

## What it demonstrates

- Enabling conversation behavior on a workflow.
- A simple **Start** to **ModelCall** flow.
- Using `{{$agent.latestUserMessage}}` while preserving chat context through the workflow conversation state.

## How it works

The workflow sends the latest user message to the **ModelCall** node and stores the updated chat history through the configured chat input and output paths.
Later turns reuse that history, so the model can respond with prior messages in context.

## Setup notes

Create a connector and model before running this sample, then select that model in the **ModelCall** node.
