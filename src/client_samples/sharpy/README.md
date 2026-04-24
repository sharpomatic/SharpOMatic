# Sharpy

`sharpy` is a minimal Next.js client sample that connects CopilotKit directly to the SharpOMatic AG-UI endpoint from the browser. It does not use `CopilotRuntime`; the page registers a CopilotKit `HttpAgent` directly with `agents__unsafe_dev_only`.

## What it demonstrates

- Direct browser-to-AG-UI chat using `@copilotkit/react-core/v2`.
- Passing the selected SharpOMatic `workflowId` through `forwardedProps.sharpomatic.workflowId` on every run.
- Switching message forwarding behavior with `Send All Messages`:
  - enabled: sends the full local CopilotKit message history.
  - disabled: sends only new client-created user/tool messages after the first request.
- Continuing or resetting conversations with the editable thread ID. In SharpOMatic, this maps to the AG-UI conversation ID.
- Rendering SharpOMatic step events as inline activity dividers.
- Showing AG-UI activity snapshots/deltas as compact progress cards when the payload looks like steps.
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
- The sample expects the server to use the current AG-UI reasoning protocol. Emit `REASONING_*` events rather than deprecated `THINKING_*` events if reasoning content should appear in CopilotKit.
