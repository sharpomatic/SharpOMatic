---
title: "AG-UI: Code only event stream"
---

This sample emits a multi-turn AG-UI conversation stream directly from Code nodes without calling a model.

## Choose this when

Use this sample when you want to understand how custom backend code can produce streamed assistant, user, and system messages.

## What it demonstrates

- Emitting text start, text delta, and text end events from a **Code** node.
- Streaming different message roles.
- Using **Suspend** nodes in a conversation workflow.
- Producing a final event-driven message without a **ModelCall** node.

## How it works

The workflow streams an assistant message in several chunks, suspends, emits messages with different roles, suspends again, then emits a final user message before ending.
The delays in the Code nodes make the stream behavior visible in the editor.

## Setup notes

No connector or model is required.
Run this as an interactive conversation workflow so you can see the streamed events and suspend points.
