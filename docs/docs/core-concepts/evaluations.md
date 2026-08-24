---
title: Evaluations
sidebar_position: 9
---

Evaluations help you measure workflow quality in a repeatable way.
Instead of checking outputs manually one-by-one, you define a reusable evaluation configuration and run it whenever your workflow changes.
This makes regression checks, model swaps, prompt updates, and release decisions more reliable.

## Why Evaluations Matter

AI workflow behavior can shift over time due to model updates, prompt changes, tool changes, and workflow logic edits.
Without evaluations, these shifts are hard to detect consistently.

Evaluations provide a structured test harness so you can:

- Compare quality across workflow versions.
- Catch regressions before promoting changes.
- Measure pass/fail and score trends over time.
- Debug weak cases using row-level run details.

## Evaluation Structure

An evaluation is defined by three core concepts: columns, rows, and graders.

### Columns

Columns define the schema for each test case row.
Each column has a type, can be mandatory or optional, and can map to an input path used when the workflow run is created.

The first column is a required **Name** column.
It is used to identify each row in run views and result tables.

### Rows

Rows are the individual test cases.
Each row has a fixed **Name** and **Repeat** value plus values for the configured columns.
Repeat defaults to `1`, can be set from `0` to `10000`, and controls how many times the row is executed in a full evaluation run.
A repeat value of `0` skips the row.
When repeat is greater than `1`, run results append the repeat number to the row name, such as `FRED (1)`, `FRED (2)`, and `FRED (3)`.
When you run the evaluation, each runnable row repeat becomes one workflow execution input.
In the editor's Rows tab, rows can be deleted one at a time or cleared in bulk with **Delete all rows** from the row actions menu after confirming the action.
Row deletion is saved when you save the evaluation.

You can also import rows from a CSV file from the row actions menu.
Save any pending evaluation changes before importing so the server uses the latest column definitions.
CSV headers are matched to evaluation column names case-insensitively, and extra CSV columns are ignored.
The optional `Repeat` CSV column sets the fixed repeat value; if it is omitted or blank, imported rows use repeat `1`.
Mandatory evaluation columns must be present in the CSV and must have a value in every imported row.
Optional columns can be missing from the CSV, or can contain empty values.
Custom column names cannot be `Name` or `Repeat` because those names are reserved for fixed row fields.

### Graders

Graders are workflows that score or assess the output from the main evaluation workflow.
You can define multiple graders, set a pass threshold for each one, and choose whether each grader contributes to the overall run score.
After the run completes, grader summaries provide statistics such as minimum score, maximum score, average score, median score, standard deviation, and pass rate.

Evaluation workflows and grader workflows must be standard one-shot workflows.
Conversation-enabled workflows are intentionally excluded from the evaluation workflow selectors because evaluations do not provide a way to answer suspend events during row execution.

### Grader Input Context

Each grader workflow starts with the row's input context merged with the output context produced by the
evaluation workflow run. Values written by the workflow overwrite the row inputs of the same name.

### Passing AG-UI Output to Graders

By default a grader only sees context values.
Anything the workflow streamed as AG-UI output — the assistant text a user would have read, tool calls and
their results, reasoning, activity snapshots — is invisible unless the workflow deliberately wrote it into the
context.

Enable **AG-UI Output** on the evaluation's Details tab to pass that stream into every grader for the row.
The **AG-UI Path** field chooses the context path it is written to; leave it blank to use `agui.messages`.

The payload is the assembled AG-UI **message list**, not the raw stream of protocol events.
Text deltas are folded into whole messages and tool call argument fragments are joined into complete
arguments, so a grader can read a reply directly instead of reassembling hundreds of fragments.
It is the same shape returned by the AG-UI history endpoint and posted back by an AG-UI client:

```json
[
  {
    "id": "call-1",
    "role": "assistant",
    "toolCalls": [
      {
        "id": "call-1",
        "type": "function",
        "function": { "name": "GetWeather", "arguments": "{\"city\":\"Perth\"}" }
      }
    ]
  },
  { "id": "tool:m1", "role": "tool", "toolCallId": "call-1", "content": "Sunny" },
  { "id": "m2", "role": "assistant", "content": "It is sunny in Perth." }
]
```

Reasoning messages appear with role `reasoning` under a `reason:` prefixed id, and activity snapshots appear
with role `activity` carrying an `activityType` and a JSON `content` object.
Events that were hidden from replay — for example a frontend tool call the workflow marked as handled — are
excluded, matching what a client would see on reconnect.
Run-level events that do not form messages, such as step and state events, are not included.

The messages are added to the context after the workflow run completes and before any grader starts, so all
graders for the row see the same value.
When the box is unticked nothing is added, which is how evaluations created before this setting existed
continue to behave.

The value is stored as **JSON text**, not as a context list.
That is what makes `{{agui.messages}}` in a prompt or instructions insert the JSON exactly as shown above.
A context list would instead be re-serialized by the template in the format the context is persisted in, which
wraps every value in a `{"$type": ..., "value": ...}` envelope and is noise in a prompt.

:::caution
This is a change from earlier versions, which stored a context list.
A grader that reads the value with a context path such as `agui.messages[0].content`, or that walks it as a list
in a **Code** node, needs updating to parse the JSON text instead.
A grader that only inserts it into a prompt with `{{agui.messages}}` keeps working and produces cleaner output
than before.
:::

:::note
The payload reflects what the workflow actually streamed.
If a model call node has **Disable Tool Events** enabled, or a tool's per-tool
[AG-UI Output](../nodes/model-call-node/tool-calling.md) mode is **Never**, those events are never recorded and
therefore cannot appear in the grader's context.
:::

### Passing Stored Chat Messages to Graders

AG-UI output covers what *this* run streamed.
Sometimes the thing you want graded is a conversation the host application already has stored, for example the exact
exchange a user had in your chatbot, held in your own database rather than in SharpOMatic.
Only the host can look that up, so SharpOMatic asks it.

Enable **Chat Messages** on the evaluation's Details tab.
The **Chat Messages Path** field chooses the context path the messages are written to; leave it blank to use
`chat.messages`.
This setting is independent of **AG-UI Output**, so an evaluation can use either or both.

The payload is **JSON text** describing the messages, so `{{chat.messages}}` in a prompt or instructions inserts it
verbatim:

```json
[
  { "role": "user", "contents": [{ "$type": "text", "text": "How much for the deck?" }] },
  {
    "role": "assistant",
    "contents": [
      { "$type": "functionCall", "name": "GetQuote", "arguments": { "area": 24 }, "callId": "call-1" }
    ]
  },
  { "role": "tool", "contents": [{ "$type": "functionResult", "callId": "call-1", "result": "4800" }] },
  { "role": "assistant", "contents": [{ "$type": "text", "text": "About $4,800." }] }
]
```

Property names are camelCase and null members are omitted, so a prompt is not padded with empty fields.
The `$type` on each content entry is part of the message model rather than a context wrapper, and tells the model
whether it is looking at text, a tool call, or a tool result.

Because the value is JSON text and not a context list, it cannot be handed to a **Model Call** node's chat input path
to be replayed as history; it is material for a grader to read, which is what grading a stored conversation calls for.

For each row, SharpOMatic calls `EvalChatMessages` on every registered `IEngineNotification` in turn and uses the first
non-null result.
The call happens after the workflow run completes and before any grader starts, so all graders for the row see the same
value.
See [Evaluations](../programmatic/evaluations.md) for the host-side implementation.

The distinction between the two empty results is deliberate:

| Host returns | Result |
| --- | --- |
| `null` from every implementation | nothing is written, so the path is absent |
| an empty list | an empty list is written at the path |

A grader can therefore tell "no host claimed this lookup" apart from "the conversation exists and has no messages".

Messages are cloned on the way in, so nothing the host keeps a reference to can be changed through the grader context.
Text, tool calls, tool results, and file content are carried across; reasoning content is dropped, matching how stored
chat history behaves everywhere else in SharpOMatic.

Template markers inside a stored message are escaped before the JSON is written, so a conversation containing
`{{...}}` or `<<...>>` is inserted as the text the user actually typed.
Without that, a message mentioning `{{expected.answer}}` would be substituted from the surrounding grader context and
leak the expected answer into the prompt, and an unresolvable path would fail the row outright.
The escapes are ordinary JSON string escapes, so anything that parses the value still reads the original text.
The same applies to AG-UI output.

:::note
If **AG-UI Output** and **Chat Messages** are both enabled and resolve to the same path, one overwrites the other.
The editor warns when it can see that the two paths match.
:::

When the box is unticked nothing is added and the host is never called, which is how evaluations created before this
setting existed continue to behave.

### Grader Output Contract

A grader workflow is expected to write its score to the context path `score`.
This value is used for score statistics and pass-rate calculations.
The score can be numeric, or a string value that can be parsed as a number.

If a grader completes without providing a numeric `score`, the grader run can still complete, but score-based aggregates will not include that row.
For consistent evaluation metrics, ensure every grader writes a valid numeric value to `score`.

### Score Calculation

Each completed grader result has its own raw score.
The grader's **Pass Threshold** is used only for pass-rate calculations: a scored grader result passes when `score >= passThreshold`.
Failed grader runs and completed grader runs without a numeric score are excluded from score statistics and pass-rate denominators.

The evaluation's **Row Score** setting controls how each row score is calculated from that row's grader scores:

- **First grader**: use the first grader's score, based on grader order.
- **Average**: average all available grader scores for the row.
- **Minimum**: use the lowest available grader score for the row.
- **Maximum**: use the highest available grader score for the row.

The evaluation's **Run Score** setting controls which per-grader summary metric contributes to the run score:

- **Average score**: use each selected grader's average score.
- **Average min score**: use each selected grader's minimum score.
- **Average max score**: use each selected grader's maximum score.
- **Average pass rate**: use each selected grader's pass rate.

For each grader, the **Run Score** checkbox controls whether that grader is included in the run score calculation.
The final run score is the average of the selected metric across included graders that have a value.
If no graders are included, or none of the included graders produced a usable metric, the run score is empty.
Pass thresholds still appear in grader summaries even when the run score is not based on pass rate.

## Running an Evaluation

From the evaluation page, select **Start Run**.
The start dialog allows:

- **Run name (optional)**: give the run a readable label. If omitted, the system uses a timestamp-based default name.
- **Run random sample**: enable this to execute only a subset of rows.
- **Sample count**: choose how many runnable rows to run in the random sample.

Sampling is useful for quick checks when you want faster feedback before running the full dataset.
If sampling is enabled, sample count must be between `1` and the number of rows whose repeat value is greater than `0`.
Rows are selected randomly each run, so two sampled runs can execute different subsets.
Sampled runs execute each selected row once, regardless of that row's repeat value.
If sampling is disabled, all rows are executed according to their repeat values.

## Runs

Each run is stored with status and progress information.
You can review run summaries, open detailed results, and inspect grader outcomes.
Runs can be canceled while in progress, and completed/failed/canceled runs remain in history for later comparison.

Evaluation execution creates underlying workflow runs for the main workflow and any grader workflows.
Those child workflow runs are stored like normal runs, but they are treated as background execution by the editor.
This means the evaluation pages continue to show evaluation progress, while the workflow page trace panel does not live-follow those evaluation-driven runs or show workflow completion toasts for them.

## Transfer Import Behavior

When evaluations are imported from a transfer package, SharpOMatic creates new evaluation entries with new identifiers.
The import does not replace an existing evaluation by ID.

This clone-on-import behavior is intentional so existing evaluations keep their original run history.
In other words, importing an evaluation configuration does not delete previous runs from existing evaluations.

Evaluation transfer includes the full configuration:

- EvalConfig
- EvalGraders
- EvalColumns
- EvalRows
- EvalData

Evaluation transfer also includes terminal run result data:

- EvalRun
- EvalRunRow
- EvalRunRowGrader
- EvalRunGraderSummary

Runs that are still running at export time are skipped because they cannot be resumed in the target instance.

## Troubleshooting

Common causes of evaluation run failures:

- **Missing workflow reference**: the evaluation workflow or grader workflow is not set or no longer exists.
- **Conversation workflow selected previously**: if an older configuration references a conversation-enabled workflow, SharpOMatic clears that selection because evaluations cannot execute conversation turns.
- **Missing mandatory row data**: a required column has no value for one or more rows.
- **Invalid sample count**: the sample count is outside the valid range for the current row total.
- **Missing grader score**: the grader workflow does not output a numeric value at `score`, so score summaries may look incomplete.
- **Empty AG-UI output**: **AG-UI Output** is enabled but the grader finds nothing at the configured path because the evaluation workflow suppressed its stream events, or because it produced none.
- **Missing chat messages**: **Chat Messages** is enabled but the path is absent, which means no `IEngineNotification` returned a list. Either the host does not implement `EvalChatMessages`, or it returned null because the row did not identify a conversation it could find.
- **Failed chat message lookup**: the host's `EvalChatMessages` threw, which fails the row. The row error names the row and wraps the original exception.
- **Grader reads the payload as a list**: both grader payloads are JSON text. A grader written against the older context-list form of `agui.messages` needs to parse the JSON instead of indexing it.

When troubleshooting, open the run details and inspect row-level errors and grader results to identify the exact failure point.
