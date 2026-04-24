# Sharpy

`sharpy` is a minimal browser-side CopilotKit sample that talks directly to an AG-UI endpoint without `CopilotRuntime`.

## Target endpoint

The sample is hard-wired to:

`https://localhost:9001/sharpomatic/api/agui`

The demo server also exposes HTTP on:

`http://localhost:9000/sharpomatic/api/agui`

The page pre-fills the AG-UI `workflowId` in a sidebar input and sends the current value in `forwardedProps.sharpomatic` on every run. 
It also includes a `Send All Messages` checkbox: when enabled, the full local message history is sent; when disabled, only new client-created user/tool messages are forwarded. 

## Run

npm install
npm run dev

Then open:

`http://localhost:3000`

## Notes

- This sample uses `agents__unsafe_dev_only`, which CopilotKit documents as a development/testing path rather than the normal production setup.
- Because the AG-UI endpoint is HTTPS on `localhost`, the browser must trust that certificate. If the certificate is self-signed and untrusted, the browser will block the request before CopilotKit sees any response.
- To continue a conversation, keep the same thread ID in the page. In SharpOMatic that same value is the `conversationId`.
- This sample is now aligned with the current AG-UI reasoning protocol. The server should emit `REASONING_*` events rather than the deprecated `THINKING_*` events if you want reasoning content to appear in the chat UI.
- The sample enables CopilotKit's default wildcard tool renderer for generic AG-UI tools, and also registers a dedicated `are_you_sure(title, message)` human-in-the-loop tool that renders an inline Yes/No confirmation card and returns a boolean result to the backend.
