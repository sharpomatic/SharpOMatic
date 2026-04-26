---
title: "AG-UI: Stateful agent with customization"
---

This sample shows a richer AG-UI stateful agent that combines user interaction, model tools, backend tool calls, step markers, activity updates, and streaming events.

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
The frontend question result is kept as workflow context rather than persisted as model chat history.

## Setup notes

Create a connector and model before running this sample, then select that model in the **ModelCall** node.
It needs the 'Sharpy' client sample because it understands the `ask_a_question` tool and other details needed for the sample.
When using the 'Sharpy' client sample, make sure you uncheck the 'Send All Messages' checkbox and put the workflow ID into the input with the same name.
This sample depends on the 'DemoServer' host code that defines function tools used by the model.
