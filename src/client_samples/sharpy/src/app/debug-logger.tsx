"use client";

import { useEffect } from "react";
import { useAgent } from "@copilotkit/react-core/v2";

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

export function SharpyDebugLogger({ threadId }: { threadId: string }) {
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
