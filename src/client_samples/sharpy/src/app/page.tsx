"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { z } from "zod";
import type { Message } from "@ag-ui/core";
import type { ReactActivityMessageRenderer } from "@copilotkit/react-core/v2";
import {
  CopilotChat,
  CopilotKitProvider,
  HttpAgent,
  useAgent,
  useDefaultRenderTool,
  useHumanInTheLoop,
  useRenderTool,
} from "@copilotkit/react-core/v2";

const AGUI_URL = "http://localhost:9000/sharpomatic/api/agui";
const DEFAULT_WORKFLOW_ID = "415e68f7-b7f4-4b5b-a8f7-e832b3df0a02";
const STEP_DIVIDER_ACTIVITY_TYPE = "sharpy-step-divider";
const SAMPLE_STATE = {
  profile: {
    name: "Sharpy Demo",
    tier: "test",
  },
  tags: ["one", "two", "three"],
};

const toolConfirmPrimaryButtonStyle = {
  appearance: "none",
  backgroundColor: "#0d8a55",
  border: "1px solid #0d8a55",
  color: "#ffffff",
} as const;

const toolConfirmSecondaryButtonStyle = {
  appearance: "none",
  backgroundColor: "#ffffff",
  border: "1px solid #c8d3cb",
  color: "#14211a",
} as const;

function nextThreadId() {
  return crypto.randomUUID();
}

function getPendingClientMessages<T extends { id: string; role: string }>(
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

const areYouSureArgsSchema = z.object({
  title: z.string(),
  message: z.string(),
});
const getWeatherArgsSchema = z.any();
const stepDividerContentSchema = z.object({
  stepName: z.string(),
});
const activityContentSchema = z.unknown();
type StepDividerContent = z.infer<typeof stepDividerContentSchema>;

const stepDividerActivityRenderer: ReactActivityMessageRenderer<StepDividerContent> = {
  agentId: "sharpy",
  activityType: STEP_DIVIDER_ACTIVITY_TYPE,
  content: stepDividerContentSchema,
  render: ({ content, message }) => (
    <StepDivider stepName={content.stepName} messageId={message.id} />
  ),
};

const defaultActivityRenderer: ReactActivityMessageRenderer<unknown> = {
  agentId: "sharpy",
  activityType: "*",
  content: activityContentSchema,
  render: ({ activityType, content, message }) => (
    <ActivityMessageCard
      activityType={activityType}
      content={content}
      messageId={message.id}
    />
  ),
};

const activityRenderers: ReactActivityMessageRenderer<any>[] = [
  stepDividerActivityRenderer,
  defaultActivityRenderer,
];

type AreYouSureToolCallProps =
  | {
      status: "inProgress";
      args: Partial<z.infer<typeof areYouSureArgsSchema>>;
      respond: undefined;
      result: undefined;
    }
  | {
      status: "executing";
      args: z.infer<typeof areYouSureArgsSchema>;
      respond: (result: boolean) => Promise<void>;
      result: undefined;
    }
  | {
      status: "complete";
      args: z.infer<typeof areYouSureArgsSchema>;
      respond: undefined;
      result: unknown;
    };

function getBooleanResultLabel(result: unknown) {
  if (result === true || result === "true") {
    return "Yes";
  }

  if (result === false || result === "false") {
    return "No";
  }

  return String(result);
}

function AreYouSureToolCall(props: AreYouSureToolCallProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleResponse(value: boolean) {
    if (props.status !== "executing" || isSubmitting) {
      return;
    }

    setIsSubmitting(true);

    try {
      await props.respond(value);
    } catch (error) {
      console.error("Failed to submit ask_a_question result.", error);
      setIsSubmitting(false);
    }
  }

  if (props.status === "inProgress") {
    return (
      <div className="toolConfirmCard">
        <p className="toolConfirmPending">
          Waiting for the confirmation prompt from the backend.
        </p>
      </div>
    );
  }

  const outcomeLabel =
    props.status === "complete" ? getBooleanResultLabel(props.result) : null;

  return (
    <div className="toolConfirmCard">
      <h3 className="toolConfirmTitle">{props.args.title}</h3>
      <p className="toolConfirmMessage">{props.args.message}</p>

      {props.status === "executing" ? (
        <div className="toolConfirmActions">
          <button
            className="toolConfirmButton toolConfirmButtonPrimary"
            disabled={isSubmitting}
            onClick={() => handleResponse(true)}
            style={toolConfirmPrimaryButtonStyle}
            type="button"
          >
            Yes
          </button>
          <button
            className="toolConfirmButton toolConfirmButtonSecondary"
            disabled={isSubmitting}
            onClick={() => handleResponse(false)}
            style={toolConfirmSecondaryButtonStyle}
            type="button"
          >
            No
          </button>
        </div>
      ) : (
        <div className="toolConfirmResult">
          <span className="toolConfirmResultLabel">Answer</span>
          <span className="toolConfirmResultValue">{outcomeLabel}</span>
        </div>
      )}
    </div>
  );
}

type WeatherToolCallProps =
  | {
      status: "inProgress";
      result: undefined;
    }
  | {
      status: "executing";
      result: undefined;
    }
  | {
      status: "complete";
      result: string;
    };

function parseTemperatureResult(result: string) {
  try {
    const parsedResult = JSON.parse(result) as unknown;

    if (typeof parsedResult === "number" && Number.isFinite(parsedResult)) {
      return parsedResult;
    }

    if (typeof parsedResult === "string") {
      const numericValue = Number(parsedResult);
      return Number.isFinite(numericValue) ? numericValue : null;
    }
  } catch {
    const numericValue = Number(result);
    return Number.isFinite(numericValue) ? numericValue : null;
  }

  return null;
}

function getWeatherVisual(temperature: number | null) {
  if (temperature === null) {
    return {
      icon: "?",
      label: "Weather unavailable",
      toneClassName: "weatherToolUnknown",
    };
  }

  if (temperature < 1) {
    return {
      icon: "\u2744\uFE0F",
      label: "Light snow with a chance of a storm",
      toneClassName: "weatherToolCold",
    };
  }

  if (temperature < 30) {
    return {
      icon: "\u2601\uFE0F",
      label: "Cloudy and overcast",
      toneClassName: "weatherToolMild",
    };
  }

  return {
    icon: "\u2600\uFE0F",
    label: "Bright and sunny day",
    toneClassName: "weatherToolHot",
  };
}

function WeatherToolCall(props: WeatherToolCallProps) {
  if (props.status !== "complete") {
    return (
      <div className="weatherToolCard">
        <h3 className="weatherToolHeading">WEATHER REPORT</h3>
        <p className="weatherToolPending">
          Waiting for the latest weather result.
        </p>
      </div>
    );
  }

  const temperature = parseTemperatureResult(props.result);
  const visual = getWeatherVisual(temperature);

  return (
    <div className={`weatherToolCard ${visual.toneClassName}`}>
      <h3 className="weatherToolHeading">WEATHER REPORT</h3>
      <div className="weatherToolBody">
        <div aria-hidden="true" className="weatherToolIcon">
          {visual.icon}
        </div>
        {temperature === null ? (
          <p className="weatherToolTemperatureUnknown">Unknown temperature</p>
        ) : (
          <p className="weatherToolTemperature">
            {temperature}°C
          </p>
        )}
        <p className="weatherToolTitle">{visual.label}</p>
      </div>
    </div>
  );
}

type ActivityStep = {
  id: string;
  label: string;
  state: string;
  description: string | null;
};

type NormalizedActivity = {
  title: string | null;
  steps: ActivityStep[];
};

type RunErrorState = {
  code: string | null;
  message: string;
};

const activityCollectionKeys = [
  "steps",
  "items",
  "entries",
  "activities",
  "tasks",
  "stages",
] as const;

const activityNestedKeys = [
  "snapshot",
  "activity",
  "content",
  "data",
  "value",
  "payload",
  "details",
  "state",
] as const;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function asTrimmedString(value: unknown) {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

function humanizeLabel(value: string) {
  const normalized = value
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .trim();

  if (!normalized) {
    return value;
  }

  return normalized.charAt(0).toUpperCase() + normalized.slice(1);
}

function formatActivityContent(content: unknown) {
  if (typeof content === "string") {
    return content;
  }

  try {
    return JSON.stringify(content, null, 2);
  } catch {
    return String(content);
  }
}

function getActivityTitle(content: Record<string, unknown>) {
  return (
    asTrimmedString(content.title) ??
    asTrimmedString(content.label) ??
    asTrimmedString(content.name) ??
    null
  );
}

function getActivityState(step: Record<string, unknown>) {
  const explicitState =
    asTrimmedString(step.state) ??
    asTrimmedString(step.status) ??
    asTrimmedString(step.phase);

  if (explicitState) {
    return explicitState;
  }

  if (
    step.completed === true ||
    step.complete === true ||
    step.succeeded === true ||
    step.done === true
  ) {
    return "completed";
  }

  if (
    step.failed === true ||
    step.error === true ||
    step.cancelled === true ||
    step.canceled === true
  ) {
    return "failed";
  }

  if (
    step.running === true ||
    step.executing === true ||
    step.active === true ||
    step.inProgress === true
  ) {
    return "running";
  }

  return null;
}

function normalizeStep(
  value: unknown,
  index: number,
  fallbackKey?: string,
): ActivityStep | null {
  if (isRecord(value)) {
    const label =
      asTrimmedString(value.label) ??
      asTrimmedString(value.title) ??
      asTrimmedString(value.name) ??
      asTrimmedString(value.step) ??
      asTrimmedString(value.description) ??
      (fallbackKey ? humanizeLabel(fallbackKey) : null) ??
      `Step ${index + 1}`;

    const state = getActivityState(value) ?? "pending";
    const description =
      asTrimmedString(value.description) ??
      asTrimmedString(value.summary) ??
      asTrimmedString(value.message) ??
      null;

    return {
      id:
        asTrimmedString(value.id) ??
        asTrimmedString(value.key) ??
        fallbackKey ??
        `step-${index}`,
      label,
      state,
      description: description === label ? null : description,
    };
  }

  if (typeof value === "string") {
    return {
      id: fallbackKey ?? `step-${index}`,
      label: fallbackKey ? humanizeLabel(fallbackKey) : `Step ${index + 1}`,
      state: value,
      description: null,
    };
  }

  if (
    fallbackKey &&
    (typeof value === "number" || typeof value === "boolean")
  ) {
    return {
      id: fallbackKey,
      label: humanizeLabel(fallbackKey),
      state: String(value),
      description: null,
    };
  }

  return null;
}

function looksLikeStepRecord(record: Record<string, unknown>) {
  const entries = Object.entries(record).filter(([key]) => {
    return (
      !activityCollectionKeys.includes(
        key as (typeof activityCollectionKeys)[number],
      ) &&
      !activityNestedKeys.includes(key as (typeof activityNestedKeys)[number]) &&
      key !== "title" &&
      key !== "label" &&
      key !== "name" &&
      key !== "summary" &&
      key !== "message"
    );
  });

  if (!entries.length) {
    return false;
  }

  return entries.every(([, value]) => {
    if (typeof value === "string") {
      return true;
    }

    if (!isRecord(value)) {
      return false;
    }

    return (
      getActivityState(value) !== null ||
      asTrimmedString(value.description) !== null ||
      asTrimmedString(value.label) !== null ||
      asTrimmedString(value.title) !== null ||
      asTrimmedString(value.name) !== null
    );
  });
}

function normalizeActivitySteps(
  content: unknown,
  inheritedTitle: string | null = null,
  depth = 0,
): NormalizedActivity | null {
  if (depth > 4) {
    return null;
  }

  if (Array.isArray(content)) {
    const steps = content
      .map((item, index) => normalizeStep(item, index))
      .filter((step): step is ActivityStep => step !== null);

    return steps.length ? { title: inheritedTitle, steps } : null;
  }

  if (!isRecord(content)) {
    return null;
  }

  const title = getActivityTitle(content) ?? inheritedTitle;

  for (const key of activityCollectionKeys) {
    if (content[key] === undefined) {
      continue;
    }

    const steps = normalizeActivitySteps(content[key], title, depth + 1);
    if (steps) {
      return steps;
    }
  }

  if (looksLikeStepRecord(content)) {
    const steps = Object.entries(content)
      .map(([key, value], index) => normalizeStep(value, index, key))
      .filter((step): step is ActivityStep => step !== null);

    if (steps.length) {
      return { title, steps };
    }
  }

  for (const key of activityNestedKeys) {
    if (content[key] === undefined) {
      continue;
    }

    const steps = normalizeActivitySteps(content[key], title, depth + 1);
    if (steps) {
      return steps;
    }
  }

  return null;
}

function getActivityStateClassName(state: string) {
  const normalized = state.toLowerCase();

  if (
    normalized.includes("complete") ||
    normalized.includes("success") ||
    normalized.includes("done")
  ) {
    return "activityStepStateDone";
  }

  if (
    normalized.includes("run") ||
    normalized.includes("progress") ||
    normalized.includes("execut") ||
    normalized.includes("active")
  ) {
    return "activityStepStateRunning";
  }

  if (
    normalized.includes("fail") ||
    normalized.includes("error") ||
    normalized.includes("cancel")
  ) {
    return "activityStepStateFailed";
  }

  return "activityStepStatePending";
}

function ActivityMessageCard({
  activityType,
  content,
  messageId,
}: {
  activityType: string;
  content: unknown;
  messageId: string;
}) {
  const activity = useMemo(
    () => normalizeActivitySteps(content),
    [content],
  );

  return (
    <div className="activityCard">
      {activity?.steps.length ? (
        <div className="activityBody" data-activity-message-id={messageId}>
          <div className="activityMeta">
            <span className="activitySummary">
              {activity.title ?? "Workflow progress"}
            </span>
            <span className="activityCount">{activity.steps.length} steps</span>
          </div>

          <ol className="activitySteps">
            {activity.steps.map((step) => (
              <li className="activityStep" key={step.id}>
                <div className="activityStepBody">
                  <span className="activityStepDescription">{step.label}</span>
                </div>
                <span
                  className={`activityStepState ${getActivityStateClassName(step.state)}`}
                >
                  {humanizeLabel(step.state)}
                </span>
              </li>
            ))}
          </ol>
        </div>
      ) : (
        <pre className="activityContent">{formatActivityContent(content)}</pre>
      )}
    </div>
  );
}

function StepDivider({
  stepName,
  messageId,
}: {
  stepName: string;
  messageId: string;
}) {
  return (
    <div className="stepDivider" data-step-message-id={messageId}>
      <span className="stepDividerLabel">{humanizeLabel(stepName)}</span>
      <span className="stepDividerLine" aria-hidden="true" />
    </div>
  );
}

function logSharpyMessage(message: unknown) {
  console.log("[Sharpy message]", message);
}

function logSharpyActivityEvent(label: string, payload: unknown) {
  console.log(`[Sharpy ${label}]`, payload);
}

function logSharpyStepEvent(label: string, payload: unknown) {
  console.log(`[Sharpy ${label}]`, payload);
}

function logSharpyRunError(payload: unknown) {
  console.log("[Sharpy run error]", payload);
}

function getRunErrorState(event: {
  code?: string;
  message?: string;
  rawEvent?: { error?: unknown };
}) {
  const message =
    asTrimmedString(event.message) ??
    (typeof event.rawEvent?.error === "string"
      ? asTrimmedString(event.rawEvent.error)
      : null) ??
    "The server reported a run error.";

  return {
    code: asTrimmedString(event.code) ?? null,
    message,
  } satisfies RunErrorState;
}

function SharpyRunErrorPanel({ threadId }: { threadId: string }) {
  const { agent } = useAgent({
    agentId: "sharpy",
    threadId,
  });
  const [runError, setRunError] = useState<RunErrorState | null>(null);

  useEffect(() => {
    setRunError(null);

    return agent.subscribe({
      onRunStartedEvent: () => {
        setRunError(null);
      },
      onRunErrorEvent: ({ event }) => {
        setRunError(getRunErrorState(event));
      },
    }).unsubscribe;
  }, [agent]);

  if (!runError) {
    return null;
  }

  return (
    <div className="runErrorCard" role="alert">
      <div className="runErrorHeader">
        <span className="runErrorEyebrow">Server Error</span>
        {runError.code ? (
          <span className="runErrorCode mono">{runError.code}</span>
        ) : null}
      </div>
      <p className="runErrorMessage">{runError.message}</p>
    </div>
  );
}

function getStepDividerId(stepName: string, messages: ReadonlyArray<Message>) {
  const stepCount = messages.filter((message) => {
    return (
      message.role === "activity" &&
      message.activityType === STEP_DIVIDER_ACTIVITY_TYPE
    );
  }).length;

  return `sharpy-step-${stepCount}-${stepName}`;
}

function SharpyStepDividers({ threadId }: { threadId: string }) {
  const { agent } = useAgent({
    agentId: "sharpy",
    threadId,
  });

  useEffect(() => {
    return agent.subscribe({
      onStepStartedEvent: ({ event }) => {
        const messages = agent.messages;

        return {
          messages: [
            ...messages,
            {
              id: getStepDividerId(event.stepName, messages),
              role: "activity",
              activityType: STEP_DIVIDER_ACTIVITY_TYPE,
              content: {
                stepName: event.stepName,
              },
            },
          ],
        };
      },
    }).unsubscribe;
  }, [agent]);

  return null;
}

function SharpyDebugLogger({ threadId }: { threadId: string }) {
  const { agent } = useAgent({
    agentId: "sharpy",
    threadId,
  });

  useEffect(() => {
    return agent.subscribe({
      onNewMessage: ({ message }) => {
        logSharpyMessage(message);
      },
      onActivitySnapshotEvent: ({ event, activityMessage }) => {
        logSharpyActivityEvent("activity snapshot", {
          event,
          activityMessage,
        });
      },
      onActivityDeltaEvent: ({ event, activityMessage }) => {
        logSharpyActivityEvent("activity delta", {
          event,
          activityMessage,
        });
      },
      onStepStartedEvent: ({ event }) => {
        logSharpyStepEvent("step started", event);
      },
      onStepFinishedEvent: ({ event }) => {
        logSharpyStepEvent("step finished", event);
      },
      onRunErrorEvent: ({ event }) => {
        logSharpyRunError(event);
      },
    }).unsubscribe;
  }, [agent]);

  return null;
}

function SharpyChat({ threadId }: { threadId: string }) {
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

  useEffect(() => {
    workflowIdRef.current = workflowId;
  }, [workflowId]);

  useEffect(() => {
    sendAllMessagesRef.current = sendAllMessages;
  }, [sendAllMessages]);

  const agent = useMemo(
    () => {
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
    },
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
            <SharpyChat threadId={threadId} />
          </div>
        </section>
      </main>
    </CopilotKitProvider>
  );
}
