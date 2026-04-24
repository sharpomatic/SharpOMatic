"use client";

import { useState } from "react";
import { z } from "zod";

export const areYouSureArgsSchema = z.object({
  title: z.string(),
  message: z.string(),
});

export const getWeatherArgsSchema = z.any();

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

export function AreYouSureToolCall(props: AreYouSureToolCallProps) {
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
