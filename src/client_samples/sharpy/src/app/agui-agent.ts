import { HttpAgent } from "@copilotkit/react-core/v2";

import { AGUI_URL, SAMPLE_STATE } from "./config";

type ClientMessage = {
  id: string;
  role: string;
  toolCallId?: string;
};

type AgentRefs = {
  workflowIdRef: React.RefObject<string>;
  sendAllMessagesRef: React.RefObject<boolean>;
  sentMessageIdsByThreadRef: React.RefObject<Map<string, Set<string>>>;
  browserToolCallIdsByThreadRef: React.RefObject<Map<string, Set<string>>>;
};

export function nextThreadId() {
  return crypto.randomUUID();
}

function getPendingClientMessages<T extends ClientMessage>(
  messages: ReadonlyArray<T>,
  browserToolCallIds: ReadonlySet<string>,
) {
  const pending: T[] = [];

  for (let index = messages.length - 1; index >= 0; index -= 1) {
    const message = messages[index];

    if (isForwardableClientMessage(message, browserToolCallIds)) {
      pending.unshift(message);
      continue;
    }

    break;
  }

  return pending;
}

function isBrowserToolResult(
  message: ClientMessage,
  browserToolCallIds: ReadonlySet<string>,
) {
  return (
    message.role === "tool" &&
    typeof message.toolCallId === "string" &&
    browserToolCallIds.has(message.toolCallId)
  );
}

function isForwardableClientMessage(
  message: ClientMessage,
  browserToolCallIds: ReadonlySet<string>,
) {
  return message.role === "user" || isBrowserToolResult(message, browserToolCallIds);
}

export function createSharpyAgent({
  workflowIdRef,
  sendAllMessagesRef,
  sentMessageIdsByThreadRef,
  browserToolCallIdsByThreadRef,
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
    const browserToolCallIds =
      browserToolCallIdsByThreadRef.current.get(input.threadId) ?? new Set();
    const pendingClientMessages = getPendingClientMessages(
      input.messages,
      browserToolCallIds,
    );
    const filteredMessages = sendAllMessagesRef.current
      ? input.messages
      : existingSentMessageIds.size === 0
        ? pendingClientMessages
        : input.messages.filter(
            (message) =>
              isForwardableClientMessage(message, browserToolCallIds) &&
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
