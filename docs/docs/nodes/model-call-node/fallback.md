---
title: Fallback Models
sidebar_position: 2
---

A **ModelCall** can contain an ordered list of model instances. The first model is the primary model. If an eligible call to that model fails, SharpOMatic can try the next configured model.

Use **Add Fallback** in the primary model card's header on the node's **Details** tab to add another model. Fallback models can use another model on the same provider or a completely different connector and provider. Using an independent provider or connector generally provides better protection from a provider-wide outage. Deleting a fallback from its card header requires confirmation.

The primary model and first fallback are shown in matching cards on the same row when space permits. Additional fallbacks continue in order, and the cards stack automatically on narrower screens.

Each fallback card shows the same available information fields as the primary card, using the fallback model's values.

Each model has its own call parameter values. This is important because providers can expose different reasoning controls, token limits, sampling settings, and structured-output options. Prompt, instructions, context paths, output paths, and AG-UI behavior remain shared by the node.

Use **Disable** in a model card header to temporarily skip that model without changing the configured order. A disabled primary is not called, so the first enabled fallback becomes attempt 1. Compatibility warnings remain relative to the configured primary because they describe the intended behavior when it is enabled again. Disabled cards remain visible and can be restored with **Enable**. If every model is disabled, the node fails with `No enabled models are configured`.

## Compatibility

The editor displays warnings when a fallback differs from the primary. The engine also validates required capabilities before starting the call.

A fallback must support the features actively used by the node, including:

- text input
- image input when an image input path is configured
- tool calling when tools are selected
- structured output when the output mode is not plain text

The editor also checks the active settings from the primary model's **Tool Calling** and **Structured Output** tabs. It warns when a fallback does not support tool calling for the selected tools, the selected structured-output type, or a selected capability value allowed by the primary metadata but not by the fallback metadata.

At execution time, SharpOMatic copies the primary Tool Calling and Structured Output values into a fallback when that fallback's metadata declares the same field and accepts the selected type, enum value, or numeric range. Selected tool names and structured-output schema details are also shared when their associated capability and mode are compatible. An incompatible value is not copied; the fallback retains its own configured value and the editor warning remains visible.

Differences such as a smaller context window, a different output-token limit, or provider-specific reasoning controls may require separate fallback parameter values.

## When fallback is used

The default behavior allows fallback for transient availability failures:

- provider rate limiting, including HTTP 429
- provider HTTP 5xx responses
- network failures
- timeouts

Configuration errors, invalid requests, missing credentials, cancellation, local context errors, and tool errors do not use fallback by default.

Fallback is deliberately conservative once a response has started. SharpOMatic does not try another model after assistant/reasoning/tool output or a tool invocation begins. This prevents duplicate visible output and repeated external tool side effects.

Hosts can customize the safe fallback decision with [`IEngineNotification.ModelFallbackOverride`](../../programmatic/run-workflow.md#model-fallback-override). Hard safety restrictions cannot be overridden.

Provider-specific callers can also translate custom SDK exceptions through `IModelCaller.ModelFallbackFailureOverride`. Returning null delegates to SharpOMatic's standard HTTP, timeout, network, cancellation, and configuration classifier.

The built-in OpenAI caller translates `ClientResultException` and Azure `RequestFailedException`; Anthropic translates its API, workload-identity, and I/O exceptions; and Google translates `ClientError`, `ServerError`, and WebSocket failures. This preserves provider status codes such as 401, 429, 500, 503, and Anthropic's overloaded 529 response in fallback metrics and policy decisions.

## Existing workflows

Version 1 workflows are upgraded in memory when loaded. Their existing `ModelId` and call parameters become the first entry in the ordered model list. Merely loading a workflow does not write to the database; the version 2 representation is persisted on the next normal save.
