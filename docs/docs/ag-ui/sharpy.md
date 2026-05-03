---
title: Sharpy Client Sample
sidebar_position: 2
---

Sharpy is a small React/Next.js client sample that calls the SharpOMatic AG-UI endpoint directly from the browser.
It is useful when you want to test a workflow through the same protocol a real AG-UI client will use, rather than only running the workflow inside the editor.

The sample is located at:

```text
src/client_samples/sharpy
```

It uses CopilotKit's `HttpAgent` directly and sends the selected workflow through `forwardedProps.sharpomatic.workflowId`.
The sample does not use `CopilotRuntime`; it is intended as a local development and protocol testing client.

## What it tests

Sharpy helps you verify:

- a workflow can be invoked through `/sharpomatic/api/agui`
- AG-UI text streaming renders in a browser client
- stateless workflows receive the full message history when required
- conversation-enabled workflows resume through a stable AG-UI `threadId`
- conversation history reloads from the AG-UI `/history` POST endpoint when you select **Reload**
- frontend tool calls can suspend a workflow and resume after the browser returns a tool result
- step, activity, state, tool-call, and run-error events are translated into AG-UI output

The sample includes custom renderers for the `ask_a_question` human-in-the-loop tool, `get_weather` tool results, activity progress cards, step dividers, and run errors.

## Prerequisites

- Node.js 20 or later.
- A SharpOMatic host exposing the AG-UI endpoint.
- A workflow created in the editor, with its workflow ID copied into Sharpy.

When running from source, start the demo server first:

```powershell
dotnet run --project src/SharpOMatic.DemoServer
```

The demo server exposes the AG-UI endpoint at:

```text
http://localhost:9000/sharpomatic/api/agui
```

Sharpy uses that URL by default in:

```text
src/client_samples/sharpy/src/app/config.ts
```

If your host uses a different port, base path, or AG-UI child path, update `AGUI_URL` in that file.

## Build and run

From the repository root:

```powershell
cd src/client_samples/sharpy
npm install
npm run dev
```

Open:

```text
http://localhost:3000
```

To check the production build:

```powershell
npm run build
npm run start
```

## Configure Sharpy

Sharpy shows the important AG-UI settings in its left rail.

**Endpoint**

This displays the AG-UI URL compiled into `src/app/config.ts`.
For the demo server, the default is `http://localhost:9000/sharpomatic/api/agui`.

**Workflow ID**

Paste the workflow ID for the workflow you want to test.
Sharpy sends this value on each request as `forwardedProps.sharpomatic.workflowId`.

**Thread ID**

This becomes the AG-UI `threadId`.
For conversation-enabled workflows, SharpOMatic uses it as the conversation ID.
Keep the same thread ID to continue a conversation, or select **New thread** to start over.
Select **Reload** to load persisted AG-UI messages for the current thread and workflow.

**Send All Messages**

This controls how much local chat history Sharpy sends to the endpoint.
Use it differently for non-conversation and conversation workflows.

## Test a workflow with Sharpy

Use this path when you want to prove a workflow works through AG-UI:

1. Start the SharpOMatic demo server:

   ```powershell
   dotnet run --project src/SharpOMatic.DemoServer
   ```

2. Open the editor:

   ```text
   https://localhost:9001/sharpomatic/editor
   ```

3. Create a workflow from one of the AG-UI samples in the **Samples** menu.
4. Configure any required connector and model, then save the workflow.
5. Copy the workflow ID from the workflow URL or workflow details.
6. Start Sharpy:

   ```powershell
   cd src/client_samples/sharpy
   npm install
   npm run dev
   ```

7. Open `http://localhost:3000`.
8. Paste the workflow ID into **Workflow ID**.
9. Set **Send All Messages** for the workflow type.
10. Send a test message in the chat.
11. Return to the editor and inspect the run or conversation history for the workflow.

## Quick settings

| Workflow type | Send All Messages | Thread ID meaning | What SharpOMatic stores |
| --- | --- | --- | --- |
| Non-conversation | Checked | AG-UI metadata only | Run history only |
| Conversation | Unchecked | SharpOMatic conversation ID | Conversation state, stream history, and run history |

## Non-conversation workflows

Use these settings for a stateless workflow:

- Workflow has conversation behavior disabled.
- **Send All Messages** is checked.
- **Workflow ID** points to the stateless workflow.
- **Thread ID** can stay at the default value or be changed for client-side organization.

In this mode, Sharpy sends the full local message history on every request.
SharpOMatic rebuilds `input.chat` from those incoming AG-UI messages for the current run.
The workflow should normally use `{{$agent.latestUserMessage.content}}` as the current prompt.

This is the right setting for samples such as **AG-UI: Stateless simple chatbot**.

## Conversation workflows

Use these settings for a stateful workflow:

- Workflow has conversation behavior enabled.
- **Send All Messages** is unchecked.
- **Workflow ID** points to the conversation-enabled workflow.
- **Thread ID** is stable for the conversation you want to continue.

In this mode, Sharpy sends only the new user message or browser tool result needed for the current turn.
SharpOMatic loads the saved conversation context for the `(workflowId, threadId)` pair and exposes the latest incoming message under `agent`.
The workflow owns durable chat history, usually by having the `ModelCall` node write back to `input.chat`.
When you select **Reload**, Sharpy calls:

```text
POST /sharpomatic/api/agui/history
```

with:

```json
{
  "threadId": "<thread-id>",
  "workflowId": "<workflow-id>"
}
```

The returned envelope applies `messages` to the chat, restores `state` on the CopilotKit agent, and seeds only the server-reported `pendingFrontendTools`.
If the last restored message is an unresolved SharpOMatic frontend confirmation call, Sharpy renders that inline chat tool card as an actionable Yes/No card.
Submitting that card adds a matching AG-UI `tool` message and runs the agent so the suspended workflow can resume.
If the server returns `404` because the thread has not been created yet, Sharpy starts with an empty chat for that thread.
Sharpy does not call this endpoint automatically when the page starts, refreshes, or the thread/workflow selection changes.

Use **New thread** when you want to test a fresh conversation without reusing the saved state from an earlier run.

This is the right setting for samples such as **AG-UI: Stateful simple chatbot**, **AG-UI: Stateful human in the loop**, and **AG-UI: Stateful agent with customization**.

## What to inspect

When testing a workflow over AG-UI, check both Sharpy and the SharpOMatic editor:

- Sharpy should show streamed assistant text as the workflow emits output.
- The editor should show a new run for non-conversation workflows.
- The editor should show conversation history for conversation-enabled workflows.
- The workflow **Stream** tab should contain the events that were translated into AG-UI output.
- Frontend tool-call samples should pause in the workflow and resume after the browser returns the tool result.
- Changing **Thread ID** should start a separate conversation for conversation-enabled workflows.

## Troubleshooting

**Sharpy cannot connect**

Check that the demo server or host is running and that `AGUI_URL` in `src/app/config.ts` matches the host URL.
The default Sharpy URL is `http://localhost:9000/sharpomatic/api/agui`.

**Browser CORS errors**

Enable CORS in the ASP.NET Core host when calling AG-UI directly from the browser during local development.
The getting-started host example uses `app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader())`.

**Workflow is not found**

Confirm the workflow ID is copied correctly and pasted into **Workflow ID**.
Sharpy sends it as `forwardedProps.sharpomatic.workflowId`.

**Conversation repeats or loses context**

Check **Send All Messages**.
Use checked for non-conversation workflows and unchecked for conversation-enabled workflows.

**Unexpected old conversation state appears**

Use **New thread** to generate a fresh thread ID.
For conversation-enabled workflows, the thread ID is the SharpOMatic conversation ID, so reusing it continues the same saved conversation.

**History does not reload after refresh**

Confirm the workflow is conversation-enabled, that the same **Thread ID** and **Workflow ID** are selected, and then select **Reload**.
The history endpoint returns `404` for missing workflows, missing threads, and workflow/thread mismatches.
