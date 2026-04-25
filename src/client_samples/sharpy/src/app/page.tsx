"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import {
  CopilotChat,
  CopilotKitProvider,
  useCopilotKit,
  useDefaultRenderTool,
  useHumanInTheLoop,
  useRenderTool,
} from "@copilotkit/react-core/v2";

import { createSharpyAgent, nextThreadId } from "./agui-agent";
import { activityRenderers, SharpyStepDividers } from "./activity-renderers";
import { AGUI_URL, DEFAULT_WORKFLOW_ID } from "./config";
import { SharpyDebugLogger } from "./debug-logger";
import { SharpyRunErrorPanel } from "./run-error-panel";
import {
  areYouSureArgsSchema,
  AreYouSureToolCall,
  getWeatherArgsSchema,
  WeatherToolCall,
} from "./tool-renderers";

function SharpyChat({
  browserToolCallIdsByThreadRef,
  threadId,
}: {
  browserToolCallIdsByThreadRef: React.RefObject<Map<string, Set<string>>>;
  threadId: string;
}) {
  const { copilotkit } = useCopilotKit();

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
      render: (props) => <AreYouSureToolCall {...props} />,
    },
    [],
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

export default function SharpyPage() {
  const [threadId, setThreadId] = useState("sharpy-demo-thread");
  const [workflowId, setWorkflowId] = useState(DEFAULT_WORKFLOW_ID);
  const [sendAllMessages, setSendAllMessages] = useState(true);
  const workflowIdRef = useRef(workflowId);
  const sendAllMessagesRef = useRef(sendAllMessages);
  const sentMessageIdsByThreadRef = useRef(new Map<string, Set<string>>());
  const browserToolCallIdsByThreadRef = useRef(new Map<string, Set<string>>());

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
                  onChange={(event) => setThreadId(event.target.value)}
                  value={threadId}
                />
                <div className="buttonRow">
                  <button
                    className="button buttonPrimary"
                    onClick={() => setThreadId(nextThreadId())}
                    type="button"
                  >
                    New thread
                  </button>
                </div>
              </div>
            </section>

            <section className="metaBlock">
              <p className="label">Workflow ID</p>
              <input
                className="threadInput mono"
                onChange={(event) => setWorkflowId(event.target.value)}
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
              threadId={threadId}
            />
          </div>
        </section>
      </main>
    </CopilotKitProvider>
  );
}
