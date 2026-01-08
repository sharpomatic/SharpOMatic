---
title: Text
sidebar_position: 2
---

If the model supports text input or text output, this tab is available.

- **Text Output Path** defaults to **output.text** and is the context path that receives text output from a successful model call.
You can leave this field blank to ignore the output. Text output is still generated but discarded.

- **Instructions** (Optional) system-level instructions for the call.
- **Prompt** (Mandatory) user-level prompt for the call.

<img src="/img/modelcall-text.png" alt="Text Settings" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

## Context substitution **\{\{\$path\}\}**

The **Instructions** and **Prompt** fields can use template substitution to insert parameterized content.

To insert a value from the context, use this pattern **\{\{\$path\}\}**, where **path** is the context path.
If the path does not exist or has no value, nothing is inserted and no runtime error occurs.
All values are converted to a string, so effectively **.ToString()** is called on the value.
This allows you to insert integers, floats, booleans, and other types because they can always be converted to a string.

<img src="/img/modelcall-text-context.png" alt="Context Substitution" width="800" style={{ maxWidth: '100%', height: 'auto' }} />

## Asset substitution **&lt;&lt;$name&gt;&gt;**

You can also substitute text assets by using the **&lt;&lt;$name&gt;&gt;** pattern, where the **name** is the library asset or run asset name.
If no matching asset is found, nothing is inserted and no runtime error occurs.
Note that only text media types such as **text/plain** can be inserted.
You cannot insert images, PDF documents, or binary data, only text.

<img src="/img/modelcall-text-asset.png" alt="Asset Substitution" width="800" style={{ maxWidth: '100%', height: 'auto' }} />
