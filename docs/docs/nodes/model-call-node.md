---
title: ModelCall Node
sidebar_position: 101
---

The ModelCall node runs an LLM request using a configured model instance.
You select the model instance defined under **Models**, and the node uses its capabilities to drive the available settings.

## Select a Model

Choose a model from the **Model** drop-down.
If no model is selected, the node fails at runtime.
Model capabilities determine which tabs appear, such as text, image, tool calling, or structured output.

## Text Input and Output

If the model supports text input, you can provide **Instructions** and a **Prompt**.
Text output is written to the **Text Output Path**.
If the output path is left blank, the node writes to `output.text`.

## Chat Input and Output

The **Chat Input Path** can point to a single chat message or a list of chat messages.
The **Chat Output Path** stores the full message history, including the model response.

## Image Input

If the model supports image input, the **Image Input Path** must point to an asset or a list of assets.
Non-image assets or non-asset values cause the node to fail.

## Structured Output

If structured output is enabled, the model response can be parsed as JSON.
Parsed output is stored at the configured text output path.
If the response is not valid JSON, the node fails.

## Tool Calling

If tool calling is enabled, you can choose which tools are available to the model.
Tool availability is configured in the host application with **AddToolMethods**.
