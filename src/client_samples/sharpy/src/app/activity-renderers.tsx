"use client";

import { useEffect, useMemo } from "react";
import { z } from "zod";
import type { ReactActivityMessageRenderer } from "@copilotkit/react-core/v2";
import { useAgent } from "@copilotkit/react-core/v2";

import { STEP_DIVIDER_ACTIVITY_TYPE } from "./config";
import { asTrimmedString, humanizeLabel } from "./shared-format";

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

type StepDividerContent = z.infer<typeof stepDividerContentSchema>;

type StepDividerMessage = {
  role?: string;
  activityType?: string;
};

type SharpyActivityRenderer =
  | ReactActivityMessageRenderer<StepDividerContent>
  | ReactActivityMessageRenderer<unknown>;

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

const stepDividerContentSchema = z.object({
  stepName: z.string(),
});

const activityContentSchema = z.unknown();

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
  render: ({ content, message }) => (
    <ActivityMessageCard content={content} messageId={message.id} />
  ),
};

export const activityRenderers = [
  stepDividerActivityRenderer,
  defaultActivityRenderer,
] satisfies SharpyActivityRenderer[];

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
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
  content,
  messageId,
}: {
  content: unknown;
  messageId: string;
}) {
  const activity = useMemo(() => normalizeActivitySteps(content), [content]);

  return (
    <div className="sampleToolCard activityCard">
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

function getStepDividerId(
  stepName: string,
  messages: ReadonlyArray<StepDividerMessage>,
) {
  const stepCount = messages.filter((message) => {
    return (
      message.role === "activity" &&
      message.activityType === STEP_DIVIDER_ACTIVITY_TYPE
    );
  }).length;

  return `sharpy-step-${stepCount}-${stepName}`;
}

export function SharpyStepDividers({ threadId }: { threadId: string }) {
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
