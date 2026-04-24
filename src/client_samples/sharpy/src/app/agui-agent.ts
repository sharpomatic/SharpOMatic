import { HttpAgent } from "@copilotkit/react-core/v2";

import { AGUI_URL, SAMPLE_STATE } from "./config";

type ClientMessage = {
  id: string;
  role: string;
};

type AgentRefs = {
  workflowIdRef: React.RefObject<string>;
  sendAllMessagesRef: React.RefObject<boolean>;
  sentMessageIdsByThreadRef: React.RefObject<Map<string, Set<string>>>;
};

export function nextThreadId() {
  return crypto.randomUUID();
}

function getPendingClientMessages<T extends ClientMessage>(
  messages: ReadonlyArray<T>,
) {
  const pending: T[] = [];

  for (let index = messages.length - 1; index >= 0; index -= 1) {
    const message = messages[index];

    if (message.role === "user" || message.role === "tool") {
      pending.unshift(message);
      continue;
    }

    break;
  }

  return pending;
}

export function createSharpyAgent({
  workflowIdRef,
  sendAllMessagesRef,
  sentMessageIdsByThreadRef,
}: AgentRefs) {
  const httpAgent = new HttpAgent({
    url: AGUI_URL,
    initialState: SAMPLE_STATE,
  });

  httpAgent.use((input, next) => {
    const existingSharpomatic =
      (
        input.forwardedProps as
          | { sharpomatic?: Record<string, unknown> }
          | undefined
      )?.sharpomatic ?? {};
    const existingSentMessageIds =
      sentMessageIdsByThreadRef.current.get(input.threadId) ?? new Set();
    const pendingClientMessages = getPendingClientMessages(input.messages);
    const filteredMessages = sendAllMessagesRef.current
      ? input.messages
      : existingSentMessageIds.size === 0
        ? pendingClientMessages
        : input.messages.filter(
            (message) =>
              (message.role === "user" || message.role === "tool") &&
              !existingSentMessageIds.has(message.id),
          );

    sentMessageIdsByThreadRef.current.set(
      input.threadId,
      new Set(input.messages.map((message) => message.id)),
    );

    return next.run({
      ...input,
      state: SAMPLE_STATE,
      messages: filteredMessages,
      forwardedProps: {
        ...(input.forwardedProps ?? {}),
        sharpomatic: {
          ...existingSharpomatic,
          workflowId: workflowIdRef.current,
        },
      },
    });
  });

  return httpAgent;
}
