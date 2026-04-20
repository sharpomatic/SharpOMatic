---
title: Text
sidebar_position: 2
---

If the model supports text input or text output, this tab is available.

On the **Details** tab, **Use Batch Output** appears after the model-specific custom fields, so the output mode option sits at the end of the general model configuration section.
The model-call dialog also has a **Stream** tab, positioned after **Chat**, where you can disable assistant text, reasoning, or tool-call stream events for that node.

- **Text Output Path** defaults to **output.text** and is the context path that receives text output from a successful model call.
If the same path is written by later model-call nodes, the later value overwrites the earlier value at that exact path.
You can leave this field blank to ignore the output. Text output is still generated but discarded.

- **Instructions** (Optional) system-level instructions for the call.
- **Prompt** (Mandatory) user-level prompt for the call.

When you use an OpenAI, Azure OpenAI, or Google model, runtime output is now incremental.
Assistant text is streamed during execution, visible reasoning is emitted as reasoning stream events, and tool calls are emitted as tool-call stream events.
Assistant, reasoning, and tool-call trace entries are also updated while the node is still running.
If you disable one of those categories in the **Stream** tab, only the matching stream events are suppressed. Final outputs and assistant/reasoning/tool trace entries are still produced.

<img src="/img/modelcall-text.png" alt="Text Settings" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

## Context substitution **\{\{\$path\}\}**

The **Instructions** and **Prompt** fields can use template substitution to insert parameterized content.

To insert a value from the context, use this pattern **\{\{\$path\}\}**, where **path** is the context path.
If the path does not exist or has no value, nothing is inserted and no runtime error occurs.
All values are converted to a string, so effectively **.ToString()** is called on the value.
This allows you to insert integers, floats, booleans, and other types because they can always be converted to a string.

<img src="/img/modelcall-text-context.png" alt="Context Substitution" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

## Asset substitution **&lt;&lt;$name&gt;&gt;**

You can also substitute text assets by using the **&lt;&lt;$name&gt;&gt;** pattern, where the **name** is the library, conversation, or run asset name.
If no matching asset is found, nothing is inserted and no runtime error occurs.
Note that only text media types such as **text/plain** can be inserted.
You cannot insert images, PDF documents, or binary data, only text.

<img src="/img/modelcall-text-asset.png" alt="Asset Substitution" width="800" style={{ maxWidth: '100%', height: 'auto' }} />
