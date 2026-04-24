"use client";

import { useEffect, useState } from "react";
import { useAgent } from "@copilotkit/react-core/v2";

import { asTrimmedString } from "./shared-format";

type RunErrorState = {
  code: string | null;
  message: string;
};

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

export function SharpyRunErrorPanel({ threadId }: { threadId: string }) {
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
