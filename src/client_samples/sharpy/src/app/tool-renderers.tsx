"use client";

import { useState } from "react";
import { z } from "zod";

export const areYouSureArgsSchema = z.object({
  title: z.string(),
  message: z.string(),
});

export const getWeatherArgsSchema = z.any();

export type RestoredFrontendToolCall = {
  toolCallId: string;
  toolName: string;
  argumentsJson: string;
  assistantMessageId: string;
};

type RestoredArgs = {
  __sharpomaticRestoredFrontendTool?: RestoredFrontendToolCall;
};

type AreYouSureToolCallProps =
  | {
      name: string;
      status: "inProgress";
      args: Partial<z.infer<typeof areYouSureArgsSchema>> & RestoredArgs;
      respond: undefined;
      result: undefined;
    }
  | {
      name: string;
      status: "executing";
      args: z.infer<typeof areYouSureArgsSchema>;
      respond: (result: boolean) => Promise<void>;
      result: undefined;
    }
  | {
      name: string;
      status: "complete";
      args: z.infer<typeof areYouSureArgsSchema>;
      respond: undefined;
      result: unknown;
    };

type AreYouSureToolCallWithRestoreProps = AreYouSureToolCallProps & {
  onRespondRestored?: (
    tool: RestoredFrontendToolCall,
    result: boolean,
  ) => Promise<void>;
};

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

function getBooleanResultLabel(result: unknown) {
  if (result === true || result === "true") {
    return "Yes";
  }

  if (result === false || result === "false") {
    return "No";
  }

  return String(result);
}

export function AreYouSureToolCall(props: AreYouSureToolCallWithRestoreProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const restoredTool =
    props.status === "inProgress"
      ? props.args.__sharpomaticRestoredFrontendTool
      : undefined;

  async function handleResponse(value: boolean) {
    if (isSubmitting) {
      return;
    }

    setIsSubmitting(true);

    try {
      if (props.status === "executing") {
        await props.respond(value);
      } else if (restoredTool && props.onRespondRestored) {
        await props.onRespondRestored(restoredTool, value);
      }
    } catch (error) {
      console.error("Failed to submit ask_a_question result.", error);
      setIsSubmitting(false);
    }
  }

  if (props.status === "inProgress") {
    if (restoredTool && props.onRespondRestored) {
      const restoredArgs = parseRestoredToolArguments(restoredTool.argumentsJson);
      const title = props.args.title ?? restoredArgs.title ?? props.name;
      const message = props.args.message ?? restoredArgs.message ?? "";

      return (
        <div className="sampleToolCard toolConfirmCard">
          <h3 className="toolConfirmTitle">{title}</h3>
          <p className="toolConfirmMessage">{message}</p>
          <div className="toolConfirmActions">
            <button
              className="toolConfirmButton toolConfirmButtonPrimary"
              disabled={isSubmitting}
              onClick={() => handleResponse(true)}
              type="button"
            >
              Yes
            </button>
            <button
              className="toolConfirmButton toolConfirmButtonSecondary"
              disabled={isSubmitting}
              onClick={() => handleResponse(false)}
              type="button"
            >
              No
            </button>
          </div>
        </div>
      );
    }

    return (
      <div className="sampleToolCard toolConfirmCard">
        <p className="toolConfirmPending">
          Waiting for the confirmation prompt from the backend.
        </p>
      </div>
    );
  }

  const outcomeLabel =
    props.status === "complete" ? getBooleanResultLabel(props.result) : null;

  return (
    <div className="sampleToolCard toolConfirmCard">
      <h3 className="toolConfirmTitle">{props.args.title}</h3>
      <p className="toolConfirmMessage">{props.args.message}</p>

      {props.status === "executing" ? (
        <div className="toolConfirmActions">
          <button
            className="toolConfirmButton toolConfirmButtonPrimary"
            disabled={isSubmitting}
            onClick={() => handleResponse(true)}
            type="button"
          >
            Yes
          </button>
          <button
            className="toolConfirmButton toolConfirmButtonSecondary"
            disabled={isSubmitting}
            onClick={() => handleResponse(false)}
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

function parseRestoredToolArguments(argumentsJson: string) {
  try {
    const parsed = JSON.parse(argumentsJson) as unknown;
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      const args = parsed as Record<string, unknown>;
      return {
        title: typeof args.title === "string" ? args.title : null,
        message: typeof args.message === "string" ? args.message : null,
      };
    }
  } catch {
    return { title: null, message: null };
  }

  return { title: null, message: null };
}

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

export function WeatherToolCall(props: WeatherToolCallProps) {
  if (props.status !== "complete") {
    return (
      <div className="sampleToolCard weatherToolCard">
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
    <div className={`sampleToolCard weatherToolCard ${visual.toneClassName}`}>
      <h3 className="weatherToolHeading">WEATHER REPORT</h3>
      <div className="weatherToolBody">
        <div aria-hidden="true" className="weatherToolIcon">
          {visual.icon}
        </div>
        {temperature === null ? (
          <p className="weatherToolTemperatureUnknown">Unknown temperature</p>
        ) : (
          <p className="weatherToolTemperature">{temperature}°C</p>
        )}
        <p className="weatherToolTitle">{visual.label}</p>
      </div>
    </div>
  );
}
