---
title: ModelCall Node
sidebar_position: 5
---

The ModelCall node runs a language model API request using a configured model instance.
You select the model instance and the node uses its defined capabilities to drive the available settings.

## Model Capabilities

If no model is selected, the node fails at runtime.
Once a model is selected, the defined model capabilities determine which tabs are available and the options within those tabs.
It will also present extra fields on the details tab, depending on capabilities.
For example, setting the maximum number of output tokens and the reasoning effort are common capabilities.

<img src="/img/modelcall-selection.png" alt="Model Capabilities" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

## Text Tab

If the model supports text input or text output, this tab is available.

- **Text Output Path** defaults to **output.text** and is the context path that receives text output from a successful model call.
You can leave this field blank to ignore the output. Text output is still generated but discarded.

- **Instructions** (Optional) system-level instructions for the call.
- **Prompt** (Mandatory) user-level prompt for the call.

<img src="/img/modelcall-text.png" alt="Text Settings" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

### Context Substitution **\{\{\$path\}\}**

The **Instructions** and **Prompt** fields can use template substitution to insert parameterized content.

To insert a value from the context, use this pattern **\{\{\$path\}\}**, where **path** is the context path.
If the path does not exist or has no value, nothing is inserted and no runtime error occurs.
All values are converted to a string, so effectively **.ToString()** is called on the value.
This allows you to insert numbers, booleans, **DateTimeOffset**, and other types because they can always be converted to a string.

<img src="/img/modelcall-text-context.png" alt="Context Substition" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

### Asset Substitution **&lt;&lt;$name&gt;&gt;**

You can also substitute text assets by using the **&lt;&lt;$name&gt;&gt;** pattern, where the **name** is the library asset or run asset name.
If no matching asset is found, nothing is inserted and no runtime error occurs.
Note that only text media types such as **text/plain** can be inserted.
You cannot insert images, PDF documents, or binary data, only text.

<img src="/img/modelcall-text-asset.png" alt="Asset Substition" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

## Image Input

If the model supports image input or image output, this tab is available.

**Image Input Path** must point to an **AssetRef** or a list that contains nothing but **AssetRef** values.
You can insert these **AssetRef** values using the editor for interactive development purposes.
Programmatically, you can add them when creating the initial context to insert into the workflow or inside a **Code** node.
Note that these assets must be image types and cannot be text, PDF documents, or binary data, only images.

See the [Assets](/docs/core-concepts/assets) page in Core Concepts for programmatic examples.

<img src="/img/modelcall-image.png" alt="Asset Substition" width="600" style={{ maxWidth: '100%', height: 'auto' }} />

## Chat Tab

The **Chat Input Path** can point to a single chat message or a list of chat messages.
The **Chat Output Path** stores the full message history, including the model response.

<img src="/img/modelcall-chat.png" alt="Asset Substition" width="600" style={{ maxWidth: '100%', height: 'auto' }} />

## Tool Calling

If tool calling is enabled, you can choose which tools are available to the model.
Tool availability is configured in the host application with **AddToolMethods**.

<img src="/img/modelcall-tools.png" alt="Asset Substition" width="600" style={{ maxWidth: '100%', height: 'auto' }} />

## Structured Output

If structured output is enabled, the model response can be parsed as JSON.
Parsed output is stored at the configured text output path.
If the response is not valid JSON, the node fails.

<img src="/img/modelcall-output-schema.png" alt="Asset Substition" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

<img src="/img/modelcall-output-type.png" alt="Asset Substition" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

