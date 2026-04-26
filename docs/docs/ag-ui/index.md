---
title: AG-UI
sidebar_position: 1
---

SharpOMatic supports AG-UI so browser and app clients can invoke workflows through a standard server-sent events endpoint.
Use this section when you want to test workflows over the protocol, build a chat client, or understand how SharpOMatic maps workflow stream events into AG-UI events.

## Start here

- [Sharpy client sample](./sharpy.md): run the React/Next.js sample client against the SharpOMatic AG-UI endpoint.
- [Endpoint](./endpoint.md): configure the ASP.NET Core endpoint and understand the request, context, conversation, and SSE behavior.

## Recommended AG-UI pages

The current section should grow around the way AG-UI is used in practice:

- **Client samples**: Sharpy now covers the React/CopilotKit path. A matching Angular client page would be useful because the main editor frontend is Angular.
- **Workflow patterns**: a focused guide for choosing stateless versus conversation-enabled workflows, with the exact `ModelCall` prompt, chat input path, and chat output path settings.
- **Frontend tool calls**: a protocol-centered guide for human-in-the-loop flows, frontend tool result resumes, and when to keep tool calls out of model chat history.
- **Activity and state streaming**: examples for `Activity Sync`, `State Sync`, step events, and code-node event helpers.
- **Troubleshooting**: common setup problems such as CORS, wrong endpoint paths, missing `forwardedProps.sharpomatic.workflowId`, duplicate history, and mismatched conversation settings.

Related node reference pages already exist for [Frontend Tool Call](../nodes/frontend-tool-call-node.md), [Backend Tool Call](../nodes/backend-tool-call-node.md), [Activity Sync](../nodes/activity-sync-node.md), [State Sync](../nodes/state-sync-node.md), [Step Start](../nodes/step-start-node.md), and [Step End](../nodes/step-end-node.md).
