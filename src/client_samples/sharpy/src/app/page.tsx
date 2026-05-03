"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  CopilotChat,
  CopilotKitProvider,
  type Message,
  useAgent,
  useCopilotKit,
  useDefaultRenderTool,
  useHumanInTheLoop,
  useRenderTool,
} from "@copilotkit/react-core/v2";

import { createSharpyAgent, nextThreadId } from "./agui-agent";
import { activityRenderers, SharpyStepDividers } from "./activity-renderers";
import { AGUI_URL, DEFAULT_WORKFLOW_ID, SAMPLE_STATE } from "./config";
import { SharpyDebugLogger } from "./debug-logger";
import { SharpyRunErrorPanel } from "./run-error-panel";
import {
  areYouSureArgsSchema,
  AreYouSureToolCall,
  getWeatherArgsSchema,
  type RestoredFrontendToolCall,
  WeatherToolCall,
} from "./tool-renderers";

type HistoryReloadRequest = {
  id: number;
  maxMessages?: number;
  threadId: string;
  workflowId: string;
};

type PendingFrontendTool = {
  toolCallId: string;
  toolName: string;
  argumentsJson: string;
  assistantMessageId: string;
};

type HistoryEnvelope = {
  messages: Message[];
  state: unknown | null;
  pendingFrontendTools: PendingFrontendTool[];
};

function SharpyChat({
  browserToolCallIdsByThreadRef,
  historyReloadRequest,
  sentMessageIdsByThreadRef,
  threadId,
}: {
  browserToolCallIdsByThreadRef: React.RefObject<Map<string, Set<string>>>;
  historyReloadRequest: HistoryReloadRequest | null;
  sentMessageIdsByThreadRef: React.RefObject<Map<string, Set<string>>>;
  threadId: string;
}) {
  const { copilotkit } = useCopilotKit();
  const { agent } = useAgent({ agentId: "sharpy", threadId });

  useEffect(() => {
    if (!historyReloadRequest || historyReloadRequest.threadId !== threadId) {
      return;
    }

    let cancelled = false;

    async function loadHistory() {
      const trimmedWorkflowId = historyReloadRequest!.workflowId.trim();
      const trimmedThreadId = historyReloadRequest!.threadId.trim();
      if (!trimmedWorkflowId || !trimmedThreadId) {
        agent.setMessages([]);
        agent.setState(SAMPLE_STATE);
        sentMessageIdsByThreadRef.current.set(historyReloadRequest!.threadId, new Set());
        browserToolCallIdsByThreadRef.current.set(historyReloadRequest!.threadId, new Set());
        return;
      }

      const historyUrl = new URL(
        `${AGUI_URL.replace(/\/$/, "")}/history`,
        window.location.href,
      );

      const response = await fetch(historyUrl, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          ...(historyReloadRequest!.maxMessages
            ? { maxMessages: historyReloadRequest!.maxMessages }
            : {}),
          threadId: trimmedThreadId,
          workflowId: trimmedWorkflowId,
        }),
      });
      if (cancelled) {
        return;
      }

      if (response.status === 404) {
        agent.setMessages([]);
        agent.setState(SAMPLE_STATE);
        sentMessageIdsByThreadRef.current.set(historyReloadRequest!.threadId, new Set());
        browserToolCallIdsByThreadRef.current.set(historyReloadRequest!.threadId, new Set());
        return;
      }

      if (!response.ok) {
        throw new Error(`History request failed with HTTP ${response.status}`);
      }

      const historyEnvelope = (await response.json()) as HistoryEnvelope;
      if (cancelled) {
        return;
      }

      const pendingTools = historyEnvelope.pendingFrontendTools ?? [];
      const historyMessages = annotateRestoredPendingToolCalls(
        historyEnvelope.messages,
        pendingTools,
      );

      agent.setMessages(historyMessages);
      agent.setState(historyEnvelope.state ?? SAMPLE_STATE);
      sentMessageIdsByThreadRef.current.set(
        historyReloadRequest!.threadId,
        new Set(historyMessages.map((message) => message.id)),
      );
      browserToolCallIdsByThreadRef.current.set(
        historyReloadRequest!.threadId,
        collectPendingFrontendToolCallIds(pendingTools),
      );
    }

    loadHistory().catch((error) => {
      if (!cancelled) {
        console.error("Failed to load AG-UI history", error);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [
    agent,
    browserToolCallIdsByThreadRef,
    historyReloadRequest,
    sentMessageIdsByThreadRef,
    threadId,
  ]);

  const submitRestoredToolResult = useCallback(
    async (tool: PendingFrontendTool, result: boolean) => {
      const browserToolCallIds =
        browserToolCallIdsByThreadRef.current.get(threadId) ?? new Set<string>();
      browserToolCallIds.add(tool.toolCallId);
      browserToolCallIdsByThreadRef.current.set(threadId, browserToolCallIds);

      agent.addMessage({
        id: crypto.randomUUID(),
        role: "tool",
        toolCallId: tool.toolCallId,
        content: JSON.stringify(result),
      });

      await copilotkit.runAgent({ agent });
    },
    [agent, browserToolCallIdsByThreadRef, copilotkit, threadId],
  );

  useEffect(() => {
    return copilotkit.subscribe({
      onToolExecutionStart: ({ agentId, toolCallId }) => {
        if (agentId !== "sharpy") {
          return;
        }

        const browserToolCallIds =
          browserToolCallIdsByThreadRef.current.get(threadId) ?? new Set();

        browserToolCallIds.add(toolCallId);
        browserToolCallIdsByThreadRef.current.set(threadId, browserToolCallIds);
      },
    }).unsubscribe;
  }, [browserToolCallIdsByThreadRef, copilotkit, threadId]);

  useHumanInTheLoop(
    {
      agentId: "sharpy",
      name: "ask_a_question",
      description: "Ask the user to confirm whether the backend should continue.",
      parameters: areYouSureArgsSchema,
      render: (props) => (
        <AreYouSureToolCall
          {...props}
          onRespondRestored={submitRestoredToolResult}
        />
      ),
    },
    [submitRestoredToolResult],
  );

  useRenderTool(
    {
      agentId: "sharpy",
      name: "get_weather",
      parameters: getWeatherArgsSchema,
      render: (props) => <WeatherToolCall {...props} />,
    },
    [],
  );

  useDefaultRenderTool();

  return (
    <>
      <SharpyDebugLogger threadId={threadId} />
      <SharpyRunErrorPanel threadId={threadId} />
      <SharpyStepDividers threadId={threadId} />
      <CopilotChat
        agentId="sharpy"
        className="chat"
        welcomeScreen={false}
        labels={{
          chatInputPlaceholder: "Send a message",
        }}
        threadId={threadId}
      />
    </>
  );
}

function collectPendingFrontendToolCallIds(
  pendingTools: ReadonlyArray<PendingFrontendTool>,
) {
  return new Set(pendingTools.map((tool) => tool.toolCallId));
}

function annotateRestoredPendingToolCalls(
  messages: ReadonlyArray<Message>,
  pendingTools: ReadonlyArray<PendingFrontendTool>,
) {
  if (pendingTools.length === 0) {
    return [...messages];
  }

  const pendingToolsById = new Map(
    pendingTools.map((tool) => [tool.toolCallId, tool]),
  );

  return messages.map((message) => {
    if (message.role !== "assistant" || !message.toolCalls?.length) {
      return message;
    }

    const toolCalls = message.toolCalls.map((toolCall) => {
      const pendingTool = pendingToolsById.get(toolCall.id);
      if (!pendingTool || pendingTool.assistantMessageId !== message.id) {
        return toolCall;
      }

      return {
        ...toolCall,
        function: {
          ...toolCall.function,
          arguments: annotateRestoredPendingToolArguments(
            toolCall.function.arguments,
            pendingTool,
          ),
        },
      };
    });

    return { ...message, toolCalls };
  });
}

function annotateRestoredPendingToolArguments(
  argumentsJson: string,
  pendingTool: PendingFrontendTool,
) {
  let args: Record<string, unknown> = {};
  try {
    const parsed = JSON.parse(argumentsJson) as unknown;
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      args = parsed as Record<string, unknown>;
    }
  } catch {
    args = {};
  }

  return JSON.stringify({
    ...args,
    __sharpomaticRestoredFrontendTool: {
      toolCallId: pendingTool.toolCallId,
      toolName: pendingTool.toolName,
      argumentsJson: pendingTool.argumentsJson,
      assistantMessageId: pendingTool.assistantMessageId,
    } satisfies RestoredFrontendToolCall,
  });
}

export default function SharpyPage() {
  const [threadId, setThreadId] = useState("sharpy-demo-thread");
  const [workflowId, setWorkflowId] = useState(DEFAULT_WORKFLOW_ID);
  const [historyMaxMessages, setHistoryMaxMessages] = useState("");
  const [sendAllMessages, setSendAllMessages] = useState(true);
  const [historyReloadRequest, setHistoryReloadRequest] =
    useState<HistoryReloadRequest | null>(null);
  const workflowIdRef = useRef(workflowId);
  const sendAllMessagesRef = useRef(sendAllMessages);
  const nextHistoryReloadRequestIdRef = useRef(0);
  const sentMessageIdsByThreadRef = useRef(new Map<string, Set<string>>());
  const browserToolCallIdsByThreadRef = useRef(new Map<string, Set<string>>());

  function clearHistoryReloadRequest() {
    setHistoryReloadRequest(null);
  }

  function requestHistoryReload() {
    nextHistoryReloadRequestIdRef.current += 1;
    const parsedMaxMessages = Number(historyMaxMessages);
    setHistoryReloadRequest({
      id: nextHistoryReloadRequestIdRef.current,
      maxMessages:
        Number.isInteger(parsedMaxMessages) && parsedMaxMessages > 0
          ? parsedMaxMessages
          : undefined,
      threadId,
      workflowId,
    });
  }

  useEffect(() => {
    workflowIdRef.current = workflowId;
  }, [workflowId]);

  useEffect(() => {
    sendAllMessagesRef.current = sendAllMessages;
  }, [sendAllMessages]);

  const agent = useMemo(
    () =>
      createSharpyAgent({
        workflowIdRef,
        sendAllMessagesRef,
        sentMessageIdsByThreadRef,
        browserToolCallIdsByThreadRef,
      }),
    [],
  );

  const agents = useMemo(() => ({ sharpy: agent }), [agent]);

  return (
    <CopilotKitProvider
      agents__unsafe_dev_only={agents}
      renderActivityMessages={activityRenderers}
    >
      <main className="shell">
        <aside className="rail">
          <div className="brand">
            <div className="brandCopy">
              <h1>Sharpy</h1>
              <p>Direct browser chat over AG-UI.</p>
            </div>
          </div>

          <div className="metaGroup">
            <section className="metaBlock">
              <p className="label">Endpoint</p>
              <p className="value endpointValue mono">{AGUI_URL}</p>
            </section>

            <section className="metaBlock">
              <p className="label">Thread ID</p>
              <div className="threadControls">
                <input
                  className="threadInput mono"
                  onChange={(event) => {
                    clearHistoryReloadRequest();
                    setThreadId(event.target.value);
                  }}
                  value={threadId}
                />
                <div className="buttonRow">
                  <button
                    className="button buttonPrimary"
                    onClick={() => {
                      clearHistoryReloadRequest();
                      setThreadId(nextThreadId());
                    }}
                    type="button"
                  >
                    New thread
                  </button>
                  <button
                    className="button"
                    onClick={requestHistoryReload}
                    type="button"
                  >
                    Reload
                  </button>
                </div>
              </div>
            </section>

            <section className="metaBlock">
              <p className="label">Messages to load</p>
              <input
                className="threadInput mono"
                inputMode="numeric"
                min="1"
                onChange={(event) => {
                  clearHistoryReloadRequest();
                  setHistoryMaxMessages(event.target.value);
                }}
                placeholder="All"
                type="number"
                value={historyMaxMessages}
              />
            </section>

            <section className="metaBlock">
              <p className="label">Workflow ID</p>
              <input
                className="threadInput mono"
                onChange={(event) => {
                  clearHistoryReloadRequest();
                  setWorkflowId(event.target.value);
                }}
                value={workflowId}
              />
            </section>

            <section className="metaBlock">
              <label className="checkboxRow">
                <input
                  checked={sendAllMessages}
                  className="checkboxInput"
                  onChange={(event) => setSendAllMessages(event.target.checked)}
                  type="checkbox"
                />
                <span>Send All Messages</span>
              </label>
            </section>
          </div>
        </aside>

        <section className="workspace">
          <div className="chatWrap">
            <SharpyChat
              browserToolCallIdsByThreadRef={browserToolCallIdsByThreadRef}
              historyReloadRequest={historyReloadRequest}
              sentMessageIdsByThreadRef={sentMessageIdsByThreadRef}
              threadId={threadId}
            />
          </div>
        </section>
      </main>
    </CopilotKitProvider>
  );
}
