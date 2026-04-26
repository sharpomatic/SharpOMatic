---
title: AG-UI
sidebar_position: 1
---

SharpOMatic supports AG-UI so browser and app clients can invoke workflows through a standard server-sent events endpoint.
Use this section when you want to test workflows over the protocol, build a chat client, or understand how SharpOMatic maps workflow stream events into AG-UI events.

## What is AG-UI?

AG-UI is the Agent-User Interaction Protocol.
It is an open, event-based protocol for connecting user-facing applications to agentic backends.
An AG-UI client sends a run request, such as `RunAgentInput`, and receives a stream of structured events such as lifecycle, text message, tool call, state, activity, and custom events.

The benefit is that a frontend can talk to different agent or workflow backends through a common contract instead of one-off request and streaming formats.
For SharpOMatic, that means a client such as Sharpy can call a workflow, render streamed model output, handle frontend tool calls, and keep conversation state aligned over the AG-UI protocol.

Protocol references:

- [AG-UI overview](https://docs.ag-ui.com/introduction)
- [AG-UI core architecture](https://docs.ag-ui.com/concepts/architecture)
- [AG-UI event reference](https://docs.ag-ui.com/sdk/js/core/events)
- [AG-UI GitHub repository](https://github.com/ag-ui-protocol/ag-ui)

## Start here

- [Sharpy client sample](./sharpy.md): run the React/Next.js sample client against the SharpOMatic AG-UI endpoint.
- [Endpoint](./endpoint.md): configure the ASP.NET Core endpoint and understand the request, context, conversation, and SSE behavior.
