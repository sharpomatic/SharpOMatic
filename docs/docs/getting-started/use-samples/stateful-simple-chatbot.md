---
title: "AG-UI: Stateful simple chatbot"
---

This sample is a minimal AG-UI chatbot with a stateful workflow that keeps chat history across turns.

## Choose this when

Use this sample when you want a basic chatbot that can remember previous messages in the same conversation.

## What it demonstrates

- Enabling conversation behavior on a workflow.
- A simple **Start** to **ModelCall** flow.
- Using `{{$agent.latestUserMessage.content}}` while preserving chat context through the workflow conversation state.

## How it works

The workflow sends the latest user message to the **ModelCall** node and stores the updated chat history through the configured chat input and output paths.
Later turns reuse that history, so the model can respond with prior messages in context.

## Setup notes

Create a connector and model before running this sample, then select that model in the **ModelCall** node.
When using the 'Sharpy' client sample, make sure you uncheck the 'Send All Messages' checkbox and put the workflow ID into the input with the same name.
