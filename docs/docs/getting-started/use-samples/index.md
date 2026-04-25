---
title: Use Samples
sidebar_position: 3
---

SharpOMatic includes sample workflows you can load into your workspace.
Samples are useful for exploring the editor, exploring different features and seeing recommended node patterns.
Samples include orchestration patterns such as fan-out/fan-in, batch processing, loops and more.
If a sample needs to use a model then you will need to create a connector and then model.
Once added you can edit the **ModelCall** in the workflow and select the model you just defined.
Some sample sets can also be used as references when building evaluation scenarios.

## Choose a sample

| Sample | Use it to learn |
| --- | --- |
| [Stateless simple chatbot](./stateless-simple-chatbot.md) | A minimal one-shot chatbot workflow with a single **ModelCall** node. |
| [Stateful simple chatbot](./stateful-simple-chatbot.md) | How conversation-enabled workflows preserve chat history between turns. |
| [Stateful human in the loop](./stateful-human-in-the-loop.md) | How to pause for frontend input before continuing to a model call. |
| [Stateful agent with customization](./stateful-agent-with-customization.md) | How to combine tools, activity updates, steps, and streaming events in a stateful agent. |
| [Code only event stream](./code-only-event-stream.md) | How Code nodes can emit conversation stream events without a model call. |
| [Example activity events output](./example-activity-events-output.md) | How Activity Sync can show progress while a workflow is running. |
| [Example tool call event output](./example-tool-call-event-output.md) | How backend tool call events can drive custom frontend rendering. |
| [Call backend code](./call-backend-code.md) | How Code nodes can call classes and async methods from the host application. |
| [Looping around path](./looping-around-path.md) | How to build a fixed-count loop with **Edit**, **Switch**, and **Code** nodes. |
| [Switching paths logic](./switching-paths-logic.md) | How to route execution through different branches with **Switch** nodes. |
| [Parallel paths logic](./parallel-paths-logic.md) | How to split work with **FanOut** and merge output with **FanIn**. |
| [Batch processing of items](./batch-processing-of-items.md) | How to process a list in parallel batches and merge the results. |

## Create a workflow from a sample

1. Open the **Workflows** page in the editor.
2. Select **Samples** from the top right, then choose a sample from the list.
3. The sample is copied into your workspace as a new workflow.

<img src="/img/samples_example.png" alt="Custom model setup" width="700" style={{ maxWidth: '100%', height: 'auto' }} />

## Where samples come from

Sample workflows are embedded in the engine and surfaced by the editor.
If no samples are available, the **Samples** menu is hidden.
To explore evaluation-related flows, start from a sample workflow and then configure an eval from the **Evaluations** section in the editor.
