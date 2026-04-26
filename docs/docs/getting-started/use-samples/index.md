---
title: Use Samples
sidebar_position: 3
---

SharpOMatic includes sample workflows you can load into your workspace.<br/><br/>
Samples are useful for exploring the editor, exploring different features and seeing recommended patterns.
Samples include AG-UI conversation patterns and basic orchestration patterns such as fan-out/fan-in, batch processing, loops and more.<br/><br/>
If a sample needs to use a model then you will need to create a connector and then model.
Once added you can edit the **ModelCall** in the workflow and select the model you just defined.
To test AG-UI samples over the browser protocol, use the [Sharpy client sample](../../ag-ui/sharpy.md).

## Choose a sample

| Sample | Description |
| --- | --- |
| [Basic: Switching paths logic](./switching-paths-logic.md) | Uses the **Switch** node to run different paths. |
| [Basic: Looping around path](./looping-around-path.md) | Shows how to loop for a defined number of times. |
| [Basic: Parallel paths logic](./parallel-paths-logic.md) | Uses **FanOut** and **FanIn** to run parallel paths. |
| [Basic: Batch processing of items](./batch-processing-of-items.md) | Parallel processing of items in batches. |
| [Basic: Call backend code](./call-backend-code.md) | Uses backend classes in the Code node. |
| [AG-UI: Stateless simple chatbot](./stateless-simple-chatbot.md) | Simplest chatbot with a stateless workflow. |
| [AG-UI: Stateful simple chatbot](./stateful-simple-chatbot.md) | Simplest chatbot with a stateful workflow. |
| [AG-UI: Stateful human in the loop](./stateful-human-in-the-loop.md) | Human-in-the-loop chatbot. |
| [AG-UI: Stateful agent with customization](./stateful-agent-with-customization.md) | Stateful agent that uses tools and streaming events. |
| [AG-UI: Example activity events output](./example-activity-events-output.md) | Uses activity events to show progress in the frontend. |
| [AG-UI: Example tool call event output](./example-tool-call-event-output.md) | Uses tool events to show custom rendering in the frontend. |
| [AG-UI: Code only event stream](./code-only-event-stream.md) | Multi-turn workflow with hand-crafted output. |

## Create a workflow from a sample

1. Open the **Workflows** page in the editor.
2. Select **Samples** from the top right, then choose a sample from the list.
3. The sample is copied into your workspace as a new workflow.

<img src="/img/samples_example.png" alt="Custom model setup" width="700" style={{ maxWidth: '100%', height: 'auto' }} />

## Where samples come from

Sample workflows are embedded in the engine and surfaced by the editor.
If no samples are available, the **Samples** menu is hidden.
