---
title: Capabilities
sidebar_position: 1
---

The **ModelCall** node runs a language model API request using a configured model instance.
You select the model instance and the node uses its defined capabilities to drive the available settings.
You can also configure ordered [fallback models](./fallback.md). Each fallback must support the capabilities actively used by the call and has its own model-specific parameter values.

If no model is selected, the node fails at runtime.
Once a model is selected, the defined model capabilities determine which tabs are available and the options within those tabs.
It also presents extra fields on the details tab, depending on capabilities.
For example, setting the maximum number of output tokens and the reasoning effort are common capabilities.

<img src="/img/modelcall-selection.png" alt="Model capabilities" width="800" style={{ maxWidth: '100%', height: 'auto' }} />
