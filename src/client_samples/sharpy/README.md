# Sharpy

`sharpy` is a minimal Next.js client sample that connects CopilotKit directly to the SharpOMatic AG-UI endpoint from the browser. It does not use `CopilotRuntime`; the page registers a CopilotKit `HttpAgent` directly with `agents__unsafe_dev_only`.

## What it demonstrates

- Direct browser-to-AG-UI chat using `@copilotkit/react-core/v2`.
- Passing the selected SharpOMatic `workflowId` through `forwardedProps.sharpomatic.workflowId` on every run.
- Loading persisted AG-UI conversation history with the **Reload** button, which calls `POST /sharpomatic/api/agui/history` with `threadId`, `workflowId`, and optional `maxMessages` in the JSON body.
- Switching message forwarding behavior with `Send All Messages`:
  - enabled: sends the full local CopilotKit message history. This is used when calling a non-conversational workflow.
  - disabled: sends only new user messages and browser-executed tool results after the first request. Tool result messages that originated from backend AG-UI protocol events are not resent. This is used to call a conversation workflow which already records previous state.
- Continuing or resetting conversations with the editable thread ID. In SharpOMatic, this maps to the AG-UI conversation ID.
- Rendering SharpOMatic step events as inline activity dividers.
- Showing AG-UI activity snapshots/deltas as compact progress cards when the payload looks like steps, with finished/100% states highlighted in green and all other states in orange.
- Handling the `ask_a_question(title, message)` human-in-the-loop tool with an inline Yes/No card that returns a boolean result to the backend.
- Rendering `get_weather` tool results with a custom weather card, while leaving other tools to CopilotKit's default wildcard renderer.
- Displaying AG-UI run errors above the chat and logging debug events to the browser console.

## Target endpoint

The sample is configured in `src/app/config.ts` to call:

```text
http://localhost:9000/sharpomatic/api/agui
```

Run the SharpOMatic demo server or another compatible host that exposes that AG-UI route before using the chat page.

## Build and run

Install dependencies:

```bash
npm install
```

Start the development server:

```bash
npm run dev
```

Open:

```text
http://localhost:3000
```

Create a production build:

```bash
npm run build
```

Run the production build:

```bash
npm run start
```

## Notes

- `agents__unsafe_dev_only` is intentionally used here because this is a direct browser sample for development and testing.
- The default workflow ID is pre-filled in the sidebar and can be edited without rebuilding the app.
- Keep the same thread ID to continue a conversation; use `New thread` to generate a new browser-side UUID.
- For conversation workflows, use **Reload** to hydrate the chat from the AG-UI history endpoint before sending the next turn. Enter **Messages to load** before reloading to test capped history responses, or leave it blank to load all messages. The response envelope restores messages, state, and the final pending frontend confirmation tool when one exists. That restored confirmation is handled on the inline chat tool card. A `404` history response for a new thread is treated as an empty chat.
- The sample expects the server to use the current AG-UI reasoning protocol. Emit `REASONING_*` events rather than deprecated `THINKING_*` events if reasoning content should appear in CopilotKit.
