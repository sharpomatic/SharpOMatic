# Upstream Open Issues

This document records open issues in the upstream libraries SharpOMatic depends on that currently constrain its design. It exists mainly to explain why tool-call history behaves the way it does, and to define what has to be true upstream before that behavior can be relied on.

Most entries concern `microsoft/agent-framework` (MAF), which supplies `Microsoft.Agents.AI.*`, and `Microsoft.Extensions.AI`, which supplies `FunctionInvokingChatClient` and the `ChatMessage`/`AIContent` model.

Last verified: 4 August 2026.

## Package Versions

| Package | Pinned in `SharpOMatic.Engine.csproj` | Latest available |
| --- | --- | --- |
| `Microsoft.Agents.AI.OpenAI` | 1.16.0 | 1.16.0 (stable) |
| `Microsoft.Agents.AI.Anthropic` | 1.16.0-preview.260730.1 | 1.16.0-preview.260730.1 (no stable release) |
| `Microsoft.Extensions.AI` | 10.8.3 | 10.8.3 |
| `Microsoft.Extensions.AI.Abstractions` | 10.8.3 | 10.8.3 |
| `Google.GenAI` | 1.16.0 | 1.16.0 |
| `Azure.AI.OpenAI` | 2.9.0-beta.1 | 2.9.0-beta.1 (stable 2.1.0 is older) |
| `Anthropic.Foundry` | 0.7.1 | 0.7.1 |

The AI stack is current, so every upstream fix released to date is present. `Microsoft.EntityFrameworkCore` (10.0.9, latest 10.0.10) and `Microsoft.CodeAnalysis.*` (5.3.0, latest 5.6.0) are intentionally held back: they are toolchain rather than AI packages, and keeping them fixed means a change in tool-call behavior can only be attributed to the AI stack.

## Native Tool Calls in Model Call Output

`ChatHistoryReplayHelper.CreatePortableOutputMessages` writes native `FunctionCallContent` and `FunctionResultContent` into **Chat Output Path**, preserving the roles, order, and per-message grouping the provider produced. Model reasoning is still dropped, and text/data/uri content is still restricted to conversational roles.

This replaced an earlier design that flattened each tool exchange into a single `ChatRole.Assistant` text message (`Invoked Tool Call, Name = …, Arguments = …, Result = …`). Flattening made a stored transcript trivially portable — it removed call-id pairing, message-role layout, and provider-specific metadata from the problem entirely — but the model never saw its own tool calls as tool calls on resume. It was removed deliberately, to establish empirically which providers can consume which stored tool history. The open issues below are the known hazards that experiment should be expected to meet.

One invariant is enforced rather than delegated to the provider: **only matched call/result pairs are portable.** An unanswered call is rejected by every provider, and a result with no call is invalid for the same reason, so both halves must be present for either to be written. This is load-bearing rather than defensive, because the engine manufactures the unanswered shape itself — `BaseModelCaller.RemoveModelCallExitToolResults` strips the exit sentinel's `FunctionResultContent` and leaves its `FunctionCallContent` behind whenever a workflow exits through a tool. Dropping that call invents nothing; synthesizing a placeholder result would put words in the tool's mouth.

Related behavior elsewhere in the engine:

- **Input** (`ClonePortableContent`, same file) replays whatever tool content a workflow stored, without the pairing filter. It is deliberately more permissive, because `Frontend Tool Call` can persist a call whose result only arrives on a later turn.
- `Backend Tool Call` (`src/SharpOMatic.Engine/Nodes/BackendToolCallNode.cs`) writes a native `Assistant(FunctionCallContent)` + `Tool(FunctionResultContent)` pair using its own GUID call ids.

Native tool content therefore already reached providers through those paths before this change; the model-call output path is now consistent with them.

`Drop Tool Calls` on the model-call **Details** tab omits tool content entirely, and shares the same code path: the set of portable call ids is simply left empty.

One consequence of storing structured arguments instead of serialized text: a conversation transcript is persisted as JSON between turns, so tool arguments and results that began as CLR strings return as `JsonElement` on replay. Both forms reach the provider through `IDictionary<string, object?>`, so each provider's serializer has to handle `JsonElement` values. This was invisible under flattening, which serialized everything to text.

## Open Issues Affecting Cross-Provider Replay

| Issue | Area | Updated | Effect |
| --- | --- | --- | --- |
| [#6865](https://github.com/microsoft/agent-framework/issues/6865) | MAF, both SDKs | 3 Jul 2026 | No framework support for carrying chat history across providers. |
| [#2699](https://github.com/microsoft/agent-framework/issues/2699) | MAF .NET, AG-UI | 18 Jun 2026 | Multi-turn tool-call replay can produce history OpenAI rejects with HTTP 400. |
| [#7453](https://github.com/microsoft/agent-framework/issues/7453) | MAF Python, Gemini | 3 Aug 2026 | Provider-specific opaque metadata is lost across a replay and triggers HTTP 400. |

**#6865 — Allow switching from provider A to provider B without losing the chat history.** Open, created 1 July 2026, assigned to westey-m, no comments. The filer pauses conversations and resumes them on a different provider, and reports that context is lost; their stated workaround is manually replaying history to the new provider.

Read this precisely. It does **not** say that replaying tool content to a different provider is impossible — manual replay of a message list is exactly what SharpOMatic does, and `ChatMessage`/`FunctionCallContent` are provider-neutral types with no type-level barrier. What is missing is any framework-level *normalisation* of call ids or message layout, and therefore any guarantee. Cross-provider tool replay is unsupported and unverified, not proven broken. That is what makes it worth testing rather than assuming.

**#2699 — AG-UI multi-turn tool-call replay produces invalid OpenAI `tool_call` history.** Open, .NET, created 8 December 2025, last updated 18 June 2026, assigned to javiercn. When one prompt triggers several simultaneous tool calls, replayed history groups all calls and then all results:

```
assistant(tool_call: call_1)
assistant(tool_call: call_2)
tool_result(call_1)
tool_result(call_2)
```

OpenAI rejects that with `An assistant message with 'tool_calls' must be followed by tool messages responding to each 'tool_call_id'`. The valid shape pairs each call with its own result. Maintainer discussion attributes the reordering to the `@ag-ui/client` npm package rather than the .NET client; no fix has been merged, and reporters confirmed the failure on 1.0.0-rc3 in March 2026.

This is now directly relevant, because model-call output carries real tool calls. SharpOMatic preserves the provider's own grouping rather than regrouping messages, which is the shape the provider already accepted, so parallel tool calls are expected to survive a same-provider resume. Watch for this failure specifically when history passes through an AG-UI client round-trip.

A commenter on that issue reports that disabling parallel tool calls avoids the problem. That is their workaround, not a recommendation for SharpOMatic. SharpOMatic already exposes `parallel_tool_calls` as a per-model capability field — mapped to `ChatOptions.AllowMultipleToolCalls` for Anthropic and `ParallelToolCallsEnabled` for OpenAI — and leaves it unset by default so models keep parallel calling enabled. Turn it off per model only if the experiment shows grouped replay actually failing.

**#7453 — Gemini `thought_signature` is lost when a tool approval is answered.** Open, Python. Included as a structural constraint rather than a bug awaiting a fix: providers attach opaque, required, provider-scoped metadata to tool calls, and that metadata cannot survive a hand-off to a different provider. Expect same-provider resumption to be materially more reliable than cross-provider for any reasoning-model path.

## Open Issues Affecting Tool Calling Generally

These apply regardless of how tool history is stored, because SharpOMatic constructs its own `FunctionInvokingChatClient` in `BaseModelCaller.CreateFunctionInvokingChatClient` and uses both declaration-only and invocable tool nodes.

| Issue | Area | Updated | Effect |
| --- | --- | --- | --- |
| [#6922](https://github.com/microsoft/agent-framework/issues/6922) | .NET | 28 Jul 2026 | `FunctionInvokingChatClient` skips invocable backend tool calls that share an iteration with a declaration-only call. |
| [#7067](https://github.com/microsoft/agent-framework/issues/7067) | .NET | 20 Jul 2026 | Intermittent HTTP 400 on approval resume through the stateless Responses path. |
| [#6268](https://github.com/microsoft/agent-framework/issues/6268) | .NET | 30 Jul 2026 | `RunStreamingAsync` can end with no assistant text on multi-tool turns with reasoning models. |
| [#5621](https://github.com/microsoft/agent-framework/issues/5621) | .NET | — | Checkpointing fails on approval content with no matching response. |

**#6922** is the most directly relevant. `ShouldTerminateLoopBasedOnHandleableFunctions` returns true for the whole iteration once it sees a non-invocable call, so a backend tool that could have run does not. Both calls still stream to the client, the client returns a result for the frontend one only, and the next request fails with `No tool output found for function call`. SharpOMatic's `Frontend Tool Call` node is declaration-only and its `Backend Tool Call` node is not, so a workflow that mixes them in one model turn is exposed. Note that the model-call output pairing filter does not mask this: the orphaned call is dropped from the stored transcript, but the live request that MAF assembles during the run is what fails.

## Resolved Upstream

Recorded so the original reasons for flattening are not re-investigated from scratch.

| Issue | Area | Closed | Was |
| --- | --- | --- | --- |
| [#2724](https://github.com/microsoft/agent-framework/issues/2724) | .NET | 18 Dec 2025 | Anthropic rejecting `tool_use ids must be unique`; reproduced on Sonnet and Opus but not GPT 5.1. |
| [#6953](https://github.com/microsoft/agent-framework/issues/6953) | .NET | 9 Jul 2026 | `TodoProvider` inserting a synthetic user message between tool calls and their results. |
| [#5941](https://github.com/microsoft/agent-framework/issues/5941) | Python | 7 Jul 2026 | Second turn failing because replayed history contained unpaired tool results. |
| [#7212](https://github.com/microsoft/agent-framework/issues/7212) | Python | 30 Jul 2026 | Compaction orphaning `function_call`/result pairs when the two were non-adjacent. |

Per-provider id uniqueness is fixed. General cross-provider replay is still unsupported.

## Not Covered by This Tracker

Only the OpenAI, Azure OpenAI, and Anthropic paths run through `Microsoft.Agents.AI.*`. Two providers are reached through their own SDKs:

- Google, via `Google.GenAI`'s own `AsIChatClient(modelName)` in `GoogleGenAIModelCaller.GetChatClient`.
- Anthropic on Foundry, via `Anthropic.Foundry`.

Their `ChatMessage`-to-wire mapping lives outside `microsoft/agent-framework`, so fixes tracked here do not necessarily clear those paths. All providers do share `Microsoft.Extensions.AI`'s `FunctionInvokingChatClient` through `BaseModelCaller`.

## Known Stale Reference in the Code

`OpenAIModelCaller` contains a `WORKAROUND` comment for `AsIChatClientWithStoredOutputDisabled`, describing an intermittent HTTP 400 `No tool call found for function call output with call_id` when the Responses API returns a `ConversationId` on one turn but not the next. It cites `microsoft/agent-framework` issue 3795.

That number is wrong. Issue #3795 is a Python bug about `OpenAIResponsesClient` sending `output_text` where the Responses API requires `input_text`, closed 11 February 2026. The nearest live .NET matches for the described symptom are #6922 and #7067. The comment should be corrected before anyone uses it as grounds to revert the workaround.

## What to Watch During the Cross-Provider Experiment

The point of storing native tool calls is to find out what actually works. Useful axes to vary, roughly in order of expected reliability:

1. **Same provider, resume** — the baseline. If this fails, the problem is SharpOMatic's, not a portability limit.
2. **Same provider, parallel tool calls** — tests the #2699 shape without changing provider. Grouping is preserved from the provider's own response, so this is expected to pass.
3. **Cross-provider, single call/result pair, non-reasoning models** — the most likely cross-provider success. Watch for call-id format and length constraints (OpenAI historically caps `tool_call_id` length; Anthropic ids look like `toolu_…`).
4. **Cross-provider, parallel calls** — combines id translation with layout requirements.
5. **Cross-provider from a reasoning model** — expected to be the least reliable, per #7453. Opaque signatures cannot cross.
6. **Google path in either direction** — mapping is owned by `Google.GenAI`, not MAF. Gemini matches function results by name rather than id, so id-based pairing may not translate.

Failures worth capturing verbatim, because each identifies a distinct cause:

- `An assistant message with 'tool_calls' must be followed by tool messages responding to each 'tool_call_id'` — layout or ordering.
- `No tool output found for function call` / `No tool call found for function call output with call_id` — pairing lost in the live request, likely #6922 or #7067 rather than the stored transcript.
- `tool_use ids must be unique` — id collision after a hand-off.
- `missing thought_signature` — opaque provider metadata, unfixable across providers.

If a shape proves reliably unportable, prefer expressing that as a third model-call output mode (for example `Native`, `Text`, `Drop`) over reinstating text flattening for everyone. `ToolCallChatPersistenceMode` is the precedent for the enum shape; the current `Drop Tool Calls` boolean cannot express three states.

## Verification Notes

Issues #2699, #2724, #3795, #6865, and #6922 were read directly, including comment threads. The remaining entries come from issue-search listings and have not been opened individually; confirm state and dates before acting on any one of them.
