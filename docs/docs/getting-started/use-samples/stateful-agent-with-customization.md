---
title: Stateful agent with customization
---

This sample shows a richer stateful agent that combines user interaction, model tools, backend tool calls, step markers, activity updates, and streaming events.

## Choose this when

Use this sample when you want to see how several agent-facing features work together in a single conversation flow.

## What it demonstrates

- Asking the user a frontend question before the agent responds.
- Preparing and synchronizing activity state with **ActivitySync** nodes.
- Wrapping the model call in **StepStart** and **StepEnd** nodes.
- Configuring a **ModelCall** with selected tools.
- Calling a backend tool and updating activity progress after the model responds.

## How it works

The workflow asks whether the user wants a customized response style, initializes an activity plan, publishes it to the frontend, then runs a model call that can use selected tools.
After the model response, Code and backend tool nodes update context values and activity state before the step ends.

## Setup notes

Create a connector and model before running this sample, then select that model in the **ModelCall** node.
The sample references selected model tools named `GetGreeting` and `GetTime` and uses backend/frontend tool events, so it is best explored in an interactive editor run.
