# Model Call Fallback Design

## Status

This document records the requirements, design, and implementation plan for fallback models on the `ModelCall` workflow node. The initial implementation was completed in July 2026, including version 1 migration, ordered model configuration, safe sequential fallback, `ModelFallbackOverride`, editor support, attempt metrics, provider migrations, tests, and documentation. The final section retains possible future enhancements.

The intended outcome is that a model call can try an ordered set of configured model instances. If the primary model fails for an eligible transient reason, the next compatible model can be used. The design must preserve existing version 1 workflows and leave room for more than one fallback in the future.

## Agreed decisions

- Version 2 model-call nodes use a single ordered `Models` array.
- `Models[0]` is the primary model and later entries are fallbacks in priority order.
- Any model entry can be disabled without changing array order. Disabled entries are skipped, attempt numbering includes only enabled entries, and an all-disabled node fails clearly.
- Compatibility warnings and shared Tool Calling/Structured Output settings remain anchored to configured `Models[0]`, even while it is disabled for fallback testing.
- Each entry has its own `ParameterValues`, because call parameters and constraints vary between model configs and providers.
- Prompt, instructions, context paths, output paths, AG-UI options, selected tools, and other call-wide behavior remain on the model-call node.
- Primary Tool Calling and Structured Output values are copied to an attempt only when the target metadata declares a compatible field, type, enum value, and numeric range. Otherwise the target retains its own model-specific value and the editor warns.
- Existing version 1 workflows are upgraded to version 2 by one shared workflow schema upgrader.
- The repository invokes the shared upgrader when workflows are read and written.
- Historical/pinned workflow snapshots invoke the same upgrader when deserialized because they bypass repository retrieval.
- Upgrades should normally happen in memory on read and be persisted on the next normal save. A repository read should not silently write to the database.
- Fallback is conservative: it is intended for transient provider availability failures, not arbitrary model-call errors.
- A fallback must not be attempted after unsafe observable output or tool side effects have started.
- Fallback eligibility is host-extensible. SharpOMatic supplies a safe default decision policy, while hosts can replace or override the soft eligibility decision using a public engine interface.
- Backward compatibility and protection against workflow corruption are release-blocking requirements. Migration and primary-only behavior require extensive fixture-based, repository, execution, snapshot, transfer, and provider tests.

## Version 2 data model

The proposed model-specific definition is conceptually:

```csharp
public class ModelCallModelDefinition
{
    public required Guid ModelId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Disabled { get; set; }
    public Dictionary<string, string?> ParameterValues { get; set; } = [];
}
```

The version 2 model-call entity contains:

```csharp
public List<ModelCallModelDefinition> Models { get; set; } = [];
```

Example serialized node fragment:

```json
{
  "nodeType": 7,
  "models": [
    {
      "modelId": "11111111-1111-1111-1111-111111111111",
      "parameterValues": {
        "max_output_tokens": "8000",
        "reasoning_effort": "Medium"
      }
    },
    {
      "modelId": "22222222-2222-2222-2222-222222222222",
      "parameterValues": {
        "max_output_tokens": "4000",
        "thinking_mode": "Adaptive"
      }
    }
  ]
}
```

An empty `Models` array means no model is selected and produces the existing "No model selected" failure at runtime. If entries exist but all are disabled, execution fails with "No enabled models are configured". Disabled defaults to false and is omitted from serialized JSON at its default, preserving the existing version 2 shape. Null model IDs are not valid version 2 entries.

## Version 1 to version 2 migration

Version 1 stores these fields directly on the node:

```text
ModelId
ParameterValues
```

The version 1 to version 2 migration is:

```text
When ModelId has a value:
    Models = [{ ModelId, ParameterValues }]

When ModelId is null:
    Models = []

Workflow.Version = 2
```

Parameter values on a version 1 node with no selected model can be discarded because they cannot be reliably associated with a model.

The upgrader should be deterministic, idempotent, and structured as a sequence of individual migrations:

```csharp
while (workflow.Version < WorkflowSchema.CurrentVersion)
{
    workflow = workflow.Version switch
    {
        1 => UpgradeV1ToV2(workflow),
        _ => throw new SharpOMaticException("Unsupported workflow version.")
    };
}
```

Workflows newer than the running version must be rejected rather than interpreted as the current format.

### Legacy deserialization

Version 1 JSON must deserialize before the typed upgrader can run. A pragmatic implementation is to retain backend-only legacy ingestion properties on `ModelCallNodeEntity`:

- Map `modelId` to a nullable legacy property.
- Map `parameterValues` to a nullable legacy property.
- Do not mark `Models` as a required JSON member while version 1 is supported.
- Ignore null legacy properties when serializing.
- Have the upgrader populate `Models` and clear the legacy properties.

This allows the existing `NodeEntityConverter` to create the current entity from either version. Version 2 serialization must emit only `models`, with no duplicate legacy fields.

An alternative is raw JSON migration or a complete version 1 DTO hierarchy, but that is more complex and is not currently preferred.

## Shared workflow schema upgrader

Create one repository-independent upgrader, for example `WorkflowSchemaUpgrader`, in the Engine. All entry points use the same implementation; migration logic must not be copied into controllers, services, or repositories.

Required callers are:

1. `RepositoryService.GetWorkflow`
   - Deserialize the stored workflow.
   - Upgrade it in memory.
   - Return only the current schema to the editor and engine.

2. `RepositoryService.UpsertWorkflow`
   - Upgrade supported older input before validation and serialization.
   - Assign `entry.Version = workflow.Version` for both inserts and updates.
   - The current implementation only sets the version during insert, which must be corrected.

3. `WorkflowSnapshotSerializer.DeserializeWorkflow`
   - Upgrade pinned workflow snapshots used by active, suspended, and conversation runs.
   - Do not rewrite historical snapshots merely because they were read.

4. Transfer import
   - Legacy ingestion properties allow deserialization.
   - `UpsertWorkflow` performs the actual schema upgrade.
   - Transfer export uses `GetWorkflow`, so it exports the current format.

5. Samples
   - `UpsertWorkflow` upgrades old embedded samples.
   - Built-in sample JSON should also be converted to version 2 as part of the implementation.

6. Test and third-party repositories
   - `IRepositoryService` documentation should state that returned workflows use the current schema and supported older inputs are normalized on upsert.
   - The test repository must apply the shared upgrader or store only current-version workflows.
   - The execution boundary may assert that a repository returned a supported/current workflow, but it should not contain separate migration logic.

Workflow nodes are stored as JSON in the existing `Workflow.Nodes` database column. The `Models` change therefore does not require SQLite or SQL Server schema migrations. Metric changes described below may require migrations.

## Backward and forward compatibility

- New SharpOMatic versions can load version 1 workflows and upgrade them to version 2.
- Version 2 is persisted on the next explicit save or import/upsert.
- The transfer envelope schema does not need to change solely for this additive workflow payload evolution.
- Old SharpOMatic versions do not understand `models` and must not be expected to edit version 2 safely.
- The existing version field is a schema version, not an optimistic concurrency token.
- If an older editor can submit a version 2 workflow while silently dropping unknown fields, fallback data can be lost. If mixed-version clients are supported, API/editor compatibility checks or an explicit minimum editor version are required.
- New workflows need a workflow-specific current schema version of 2. Do not change the global default version for every entity type.

### Compatibility and non-corruption guarantees

The implementation must treat the following as release-blocking invariants:

- Every valid version 1 workflow that runs before this feature must still load and run after the feature is installed.
- A version 1 workflow with one selected model must behave as a version 2 workflow with exactly one `Models` entry. Merely upgrading it must not introduce retries, fallback calls, parameter changes, duplicate events, metric changes, or output changes.
- Migration must preserve all workflow and node IDs, connector IDs, connections, ordering, coordinates, titles, shared model-call settings, and unrelated node payloads.
- Migration must not modify model, connector, asset, evaluation, run, conversation, or metric records.
- Reading an old workflow must not write or partially update its stored row.
- Saving an upgraded workflow must atomically store a matching version number and node payload. A failed save must not leave version 2 metadata with version 1 nodes, or the reverse.
- Repeated upgrades must be idempotent and must never create duplicate model entries.
- Unsupported future versions must fail before execution or persistence with a clear error. They must never be coerced to version 2.
- A malformed legacy workflow must fail without partially mutating persisted data.
- Historical run snapshots must retain their original stored JSON. Their in-memory upgraded representation may execute, but reading/resuming must not rewrite history.
- Existing hosts that implement `IEngineNotification` or `IRepositoryService` must have a documented and source-compatible transition path.
- A version 2 workflow must never be serialized with both authoritative `models` data and populated legacy `modelId`/`parameterValues` data.

Keep representative version 1 workflow JSON files as immutable golden test fixtures copied from a released format. Tests should load these files through every supported boundary rather than relying only on newly constructed C# objects, which can accidentally reflect the new schema instead of the real legacy shape.

Database integration tests should seed genuine version 1 workflow rows directly, including their version and node JSON, then exercise retrieval and saving through `RepositoryService`. Run these checks against both SQLite and SQL Server where the CI environment permits; at minimum, provider-independent repository behavior must be covered and provider-specific persistence verified proportionately.

## Capability compatibility requirements

The fallback does not need an identical capability list. It must support the functional capabilities actively used by the node.

Treat these as hard incompatibilities when applicable:

- Text input, because all current model callers require it.
- Image input when the call supplies image input.
- Tool calling when tools are selected and available to the call.
- Structured output when the configured mode is not plain text.
- The editor warns when selected tool-calling or structured-output field values are absent from, use a different type than, or fall outside the enum/range declared by the fallback model metadata.
- Any future required output modality.

Treat these as warnings or parameter-specific validation issues:

- Smaller context window.
- Smaller maximum output limit.
- Unsupported sampling, reasoning, or thinking controls.
- Different structured-output strictness.
- Different accepted image formats.
- Custom-model capabilities, because they are user-declared.

Capability names alone are insufficient. Compatibility checking must also consider active parameter descriptors, including field name, type, enum options, numeric range, and provider-specific semantics.

Validation must run in the editor for immediate feedback and again in the engine because workflows can be imported or submitted through APIs.

## Failure policy

Default fallback eligibility should be restricted to transient provider availability failures:

- HTTP 429 or provider rate limiting.
- Provider HTTP 5xx responses.
- Network connection failures.
- Provider timeouts and HTTP 408.
- Explicit provider-unavailable errors.

Do not fallback by default for:

- User cancellation, host shutdown, or `OperationCanceledException`.
- Missing models, connectors, configs, or model-caller registrations.
- Missing or invalid credentials.
- Invalid node parameters or malformed structured-output schemas.
- Context-window overflow.
- Context path, image path, template substitution, or other local input errors.
- Tool implementation exceptions.
- Structured-response parsing failures.
- Content refusals or normal safety responses.

Authentication fallback may be useful when the fallback has independent credentials, but it can hide broken configuration. If supported, make it explicit rather than part of the default transient policy.

Provider SDK errors should be normalized into an engine-level provider exception or classification result containing, where available:

- Provider/connector identity.
- Failure category.
- HTTP status code.
- Retryability.
- Retry-after information.
- Whether response output had started.
- Original exception as the inner exception.

`IModelCaller` provides a default `ModelFallbackFailureOverride(Exception)` method returning null, and `BaseModelCaller` exposes it as virtual. Built-in or host provider callers return a classification for SDK-specific errors and return null for the standard classifier. Provider classification happens before standard HTTP/network translation and is separate from the fallback decision made by `IEngineNotification.ModelFallbackOverride`.

The built-in callers explicitly translate the installed SDK exception shapes: OpenAI `ClientResultException` and Azure `RequestFailedException`; Anthropic API, workload-identity, and I/O exceptions; and Google `ClientError`, `ServerError`, and WebSocket exceptions. Tests construct these SDK exception types directly so package changes that alter their status-code contracts are detected.

## Extensible fallback decision override

Hosts must be able to decide whether an otherwise safe failed attempt should advance to the next configured model. SharpOMatic provides the default transient policy above, but applications may need different rules for private gateways, provider-specific errors, tenant limits, authentication failover, cost controls, or custom exception types.

Reuse `IEngineNotification`, which already contains the connector and provider client creation override hooks. Add a default interface method named `ModelFallbackOverride` so it follows the existing `ConnectionOverride`, `OpenAIOverride`, `AzureOpenAIOverride`, and other override naming:

```csharp
public interface IEngineNotification
{
    ValueTask<bool?> ModelFallbackOverride(
        ModelFallbackDecisionContext context,
        CancellationToken cancellationToken = default
    ) => ValueTask.FromResult<bool?>(null);
}
```

The nullable result has explicit semantics:

- `true`: override the built-in recommendation and try the next configured model.
- `false`: override the built-in recommendation and stop fallback processing.
- `null`: this notification has no opinion.

Invoke registered `IEngineNotification` implementations in DI registration order and use the first non-null result, matching the first-explicit-override behavior already used by the provider client overrides. If every notification returns null, use SharpOMatic's built-in recommendation. A plain `bool` is not sufficient because multiple notifications require a neutral value.

The default interface implementation is required for source and binary compatibility with existing notification implementations. No new DI service or builder registration is needed: hosts use the existing `IEngineNotification` registration mechanism. The async `ValueTask` shape allows a host to consult tenant policy or other application state without blocking, even though the method name intentionally follows the existing `...Override` theme rather than adding an `Async` suffix.

The decision context should be immutable and contain enough information for a host to make a decision without querying or mutating engine internals:

- Run ID, workflow ID, conversation ID, and node entity ID/title.
- Failed attempt index and total configured model count.
- Failed model definition and its per-call parameter values, exposed read-only.
- Failed `Model`, `ModelConfig`, `Connector`, and `ConnectorConfig` metadata, with secrets removed or inaccessible.
- Next model definition and resolved metadata, when available.
- The original exception and normalized provider failure classification.
- HTTP status code and retry-after duration when available.
- Whether the failed attempt was the primary or already a fallback.
- Whether assistant text, reasoning, provider tool-call output, or tool invocation started.
- Whether any stream event was published or persisted.
- Whether node/thread context may have been mutated during the attempt.
- The built-in policy's proposed decision and reason.
- Cancellation state and any remaining logical-call time budget.

An explicit override returns `true` to try the next model and `false` to stop and surface the failure. Override exceptions should not silently trigger fallback; preserve the original model failure and report the override failure as additional diagnostic information.

### Non-overridable safety gates

`ModelFallbackOverride` controls soft eligibility, but it must not be able to override invariants that protect correctness and prevent duplicated side effects. The engine must reject fallback before calling, or regardless of the override result when:

- No next model exists.
- The run or host is being cancelled.
- A tool invocation has started.
- The attempt may have mutated node/thread context in a way that cannot be rolled back.
- Provider response output has already been published to a client, unless a future explicitly designed recovery protocol can retract it.
- Starting another attempt would otherwise violate an engine correctness invariant.

The engine should calculate these hard gates separately from the default transient classifier. Notifications receive the safety-state information for observability, but a `true` result cannot bypass a hard gate. This prevents custom code from accidentally enabling corruption or duplicate external actions.

Override invocation and its result should be recorded in diagnostics without recording secrets or full prompts by default. Include whether the built-in recommendation or a notification override made the decision and a machine-readable reason when possible.

## Streaming and side-effect safety

Fallback is not safe after an attempt has produced observable or irreversible effects.

Current concerns include:

- Provider callers emit the user prompt stream events before the provider request. A second attempt could duplicate the prompt.
- Streaming assistant text or reasoning may already have been pushed to clients before a late failure.
- Tool calls may mutate workflow context or external systems. Repeating them can duplicate side effects.
- A tool can succeed and a later provider round trip in the same agent cycle can then be rate limited.

Required conservative behavior:

- Emit the user prompt once per logical model-call node execution, not once per provider attempt.
- Give each attempt isolated progress/event state.
- Allow fallback only before provider response output or tool invocation has begun.
- Do not merge failed-attempt response events into the successful attempt.
- Write node text output and chat output only from the successful attempt.
- Preserve the original exception if no eligible fallback succeeds.

Buffering the complete response would permit fallback after partial generated output, but it would remove true streaming and still could not undo tool side effects. It is not the preferred default.

## Engine design

`ModelCallNode` should orchestrate the logical call because it already loads models and connectors, selects keyed `IModelCaller` implementations, writes outputs, and records metrics.

Conceptual flow:

1. Ensure `Models` is non-empty.
2. Resolve and validate every configured model, config, connector, and connector config.
3. Validate each fallback against the node's active capabilities.
4. Emit logical-call prompt events once.
5. For each ordered model definition:
   - Create isolated attempt/progress state.
   - Select the keyed model caller for its connector.
   - Invoke it with that entry's parameters.
   - Return immediately on success.
   - Classify an exception and evaluate non-overridable safety gates.
   - Invoke `IEngineNotification.ModelFallbackOverride` when another attempt is available and it remains safe, using the first explicit result or the built-in recommendation.
6. Write output/context/chat once from the successful result.
7. If all eligible attempts fail, throw an exception that retains useful attempt details and the final/original causes.

`IModelCaller` currently receives the complete `ModelCallNodeEntity` and reads `Node.ParameterValues`. Refactor it to receive an immutable attempt/settings object that contains the current model entry's parameters and the shared node settings it requires. Do not mutate or clone the node between attempts.

Also decide how provider-level retries interact with fallback. Avoid multiplying retries across every fallback or ignoring provider `Retry-After` guidance. Per-call time budgets and future circuit-breaker behavior may be worthwhile, but are not required for the first version.

## Frontend design

The frontend handles version 2 only because the backend returns upgraded workflows.

Replace the current node-level `modelId` and `parameterValues` signals with a `models` collection. Each item contains a model ID and its own parameter values.

Suggested dialog behavior:

- Display `Models[0]` as "Primary model".
- Display later items in a "Fallback models" section.
- Allow adding, removing, and reordering fallback entries.
- A first release may limit the UI to one fallback while retaining the array format.
- Use a collapsible row/card for each model with its picker, information, compatibility messages, and dynamic fields.
- Keep prompt, text, chat, image, tool, structured-output, and AG-UI settings at node level.
- Let the primary model determine which feature tabs are shown.
- Validate each fallback against the features configured through those tabs.
- Deep-compare array order, model IDs, and parameter dictionaries for dirty tracking.
- Warn when a selected model has been deleted or is unavailable; do not silently reorder entries.

The frontend workflow default must explicitly use schema version 2 rather than changing `Entity.DEFAULT_VERSION` globally.

## Metrics and telemetry

The system records one `ModelCallMetric` per provider attempt. Primary and fallback attempts share a `LogicalCallId`, and their `AttemptNumber` values start at 1 and increase in execution order.

Required concepts:

- Logical model call.
- Provider attempt.
- Attempt number/order.
- Primary versus fallback.
- Model and connector actually attempted.
- Failure category/status.
- Whether fallback recovered the logical call.
- Tokens and cost for every attempt where reported.

Additional metric fields:

- `LogicalCallId`.
- `AttemptNumber`.
- `FailureCategory` and provider status code.

`IsFallback` is derived from `AttemptNumber > 1`. The entry with the highest attempt number is final by definition, so no `IsFinalAttempt` field is needed. Existing rows migrated with an empty logical call ID must be treated as independent calls, using their metric ID when grouping historical data.

Dashboard semantics must be explicitly updated:

- Model-call count means provider attempt count.
- A recovered call contributes a failed primary attempt and a successful fallback attempt.
- Dashboard labels distinguish provider attempts from logical calls and failed attempts from final logical failures.
- Logical-call grouping, recovery totals/rates, normalized failure category/status breakdowns, and primary-versus-fallback usage are derived from the existing attempt rows; no final-attempt or aggregate entity fields are stored.
- Recent and slowest logical calls expose expandable ordered attempt details. Historical empty logical IDs group by metric ID.
- Attempt failures should remain visible for provider reliability analysis.
- Cost/token totals should include chargeable failed attempts where usage is available.

Workflow-run model-call count and failure count therefore also describe provider attempts. The workflow itself can succeed while its model-call attempt metrics include a recovered failure. The additional metric columns require matching SQLite and SQL Server migrations.

Telemetry should record each provider attempt as a child span or event. Node-level tags should distinguish the requested primary model from the model that ultimately produced the result.

## Transfer considerations

- Workflow export currently does not automatically include models referenced by a workflow.
- Users must include the primary and all fallback model instances when exporting a portable package unless dependency-aware export is added.
- Imported workflows can refer to missing model IDs. The editor and engine need clear validation for every array entry.
- Connector and model IDs remain host-dependent, as with the existing primary model reference.

## Main implementation areas

Likely files/components include:

- `SharpOMatic.Engine/Entities/Definitions/ModelCallNodeEntity.cs`
- A new model-call model-definition type.
- A new shared workflow schema upgrader and current-version definition.
- `SharpOMatic.Engine/Services/RepositoryService.cs`
- `SharpOMatic.Engine/Helpers/WorkflowSnapshotSerializer.cs`
- `SharpOMatic.Engine/Nodes/ModelCallNode.cs`
- `SharpOMatic.Engine/Interfaces/IModelCaller.cs`
- An immutable fallback decision context, normalized failure classification, and the backward-compatible `IEngineNotification.ModelFallbackOverride` method.
- `SharpOMatic.Engine/Services/BaseModelCaller.cs`
- All provider-specific model callers.
- `SharpOMatic.Engine/Services/TransferService.cs` and transfer tests as needed.
- `SharpOMatic.Engine/Services/SamplesService.cs` and embedded workflow samples.
- `SharpOMatic.FrontEnd/src/app/entities/definitions/model-call-node.entity.ts`
- The model-call dialog TypeScript, HTML, and styles.
- Model-call metrics and both provider migrations if attempt fields are added.
- `SharpOMatic.Tests/Services/TestRepositoryService.cs`
- Workflow test builders and version defaults.
- Model-call node and models documentation.

## Required tests

### Schema upgrade

- Golden version 1 JSON fixtures deserialize through the actual repository, transfer, snapshot, and controller serializers.
- Repository retrieval upgrades a version 1 workflow to version 2 in memory.
- Retrieval does not persist merely because it upgraded.
- Saving an upgraded workflow persists version 2 and only the `models` format.
- Saving version 2 updates the stored workflow version and node JSON atomically.
- Upsert accepts supported version 1 input.
- Version 2 round-trips without legacy properties.
- Null version 1 `ModelId` becomes an empty array.
- Version 1 parameters move to the first entry.
- Upgrade is idempotent.
- Repeated get/save/get cycles do not duplicate entries or change parameters.
- Every non-model-call node and all workflow connections are structurally unchanged by migration.
- IDs, layout, shared model-call settings, and unrelated serialized fields remain unchanged.
- Malformed version 1 input fails without modifying the stored row.
- Failed persistence cannot leave the version and node JSON at different schema levels.
- Unsupported newer versions are rejected.
- Version 1 pinned workflow snapshots resume successfully.
- Reading/resuming a version 1 snapshot does not rewrite the stored snapshot.
- Version 2 snapshots remain unchanged.
- Version 1 transfer imports and exports as version 2.
- Directly seeded version 1 database rows upgrade correctly through repository retrieval.
- SQLite and SQL Server persistence behavior is covered where provider integration testing is available.

### Primary-only regression

- A migrated version 1 call produces exactly one provider attempt.
- Primary-only batch, streaming, chat replay, structured output, image input, and tool calling retain existing behavior.
- Prompt, assistant, reasoning, and tool event counts/order remain unchanged for primary-only calls.
- Context and chat outputs remain unchanged for primary-only calls.
- Existing model-call metric and workflow-run metric semantics remain unchanged when no fallback attempt occurs.
- Existing provider override notifications still run as before.
- Existing `IEngineNotification` implementations remain source/binary compatible and its new default method requires no host changes.
- Existing custom `IRepositoryService` implementations receive a clear compatibility contract and engine validation failure rather than corrupt execution if they return unsupported data.

### Execution

- Primary success does not invoke a fallback.
- Eligible 429, timeout, network, and 5xx failures invoke the next model.
- Different fallback entries use their own parameter values.
- Compatible primary Tool Calling and Structured Output values are shared, while incompatible values leave the fallback-specific value unchanged.
- Fallback can cross keyed provider callers.
- Primary plus two fallbacks cover success on attempt three, disabled middle entries, all-terminal failures, and grouped per-attempt metrics.
- OpenAI, Anthropic, and Google SDK exception instances preserve their expected status and fallback categories.
- All eligible failures surface a useful final exception.
- Cancellation, validation, configuration, and local input failures do not fallback.
- Missing fallback models produce clear validation errors.
- Active capability incompatibilities are rejected.
- Optional capability and constraint differences produce warnings.

### Fallback decision override

- The built-in recommendation returns true for each documented transient category and false for every documented non-transient category.
- Provider exception mappings supply the expected normalized status, category, and retry-after data.
- `ModelFallbackOverride` receives the failed and next model/connector metadata, attempt state, original exception, classification, and default recommendation.
- A notification override can allow a safe failure that the built-in recommendation rejects.
- A notification override can veto a fallback that the built-in recommendation allows.
- A notification override cannot force fallback after cancellation, published output, tool invocation, or unsafe context mutation.
- Secrets and sensitive connector values are not exposed in the decision context or diagnostics.
- Override cancellation is honored.
- An override exception does not hide the original provider failure or automatically start another attempt.
- Null, true, false, first-explicit registration ordering, multiple notifications, and default-interface compatibility are all tested.

### Streaming and tools

- The prompt event is emitted once when fallback occurs.
- A failure before output can fallback cleanly.
- A failure after assistant output begins does not fallback.
- A failure after reasoning/tool output begins does not corrupt event state.
- A failure after tool invocation does not fallback.
- Successful fallback writes only its text and chat output.
- Batch and streaming modes follow the same eligibility policy.

### Metrics

- Recovered calls distinguish logical success from failed provider attempts.
- Final failures are counted correctly.
- Attempt ordering and actual model/connector are retained.
- Workflow-run call/failure aggregation remains meaningful.
- Token and cost totals follow the documented attempt policy.

## Documentation requirements

Update the model-call and model documentation in the same implementation. Document:

- Primary/fallback ordering.
- Separate parameters per model.
- Capability errors and warnings.
- Exact default failure categories.
- How to implement and register `IEngineNotification.ModelFallbackOverride`, the complete decision context, handler lifetime/thread-safety expectations, first-explicit ordering, and non-overridable safety gates.
- Streaming cutoff behavior.
- Tool side-effect/idempotency limitations.
- Transfer requirements for referenced models/connectors.
- Metrics definitions for logical calls and attempts.
- Version 1 automatic upgrade behavior.

## Implementation plan

Implement this as gated stages. Each stage must leave the solution buildable and preserve all existing primary-only behavior. Do not combine schema migration, fallback execution, UI, and metrics into one unreviewable change.

### Stage 0: Lock down the version 1 baseline

Before changing production contracts:

1. Capture immutable version 1 golden workflow files from the released format. Include:
   - A minimal model-call workflow.
   - Batch and streaming model calls.
   - Chat input/output, image input, structured output, and selected tools.
   - A workflow containing every node type and representative connections/layout.
   - A conversation/suspended-run pinned snapshot.
   - Camel-case repository JSON and the casing used by embedded samples/transfers.
2. Add characterization tests proving the current repository, transfer, snapshot, and execution paths can consume those fixtures.
3. Record primary-only event ordering, context output, chat output, and metric behavior so later stages can assert equivalence.
4. Add direct database seeding helpers for version 1 workflow rows rather than creating legacy data through new entity classes.

Gate: the full existing backend suite and the new version 1 characterization suite pass before production schema code changes.

### Stage 1: Introduce the version 2 contracts and shared upgrader

1. Add `WorkflowSchema.CurrentVersion = 2` and keep the minimum supported version explicit.
2. Add `ModelCallModelDefinition` with non-null `ModelId` and per-model `ParameterValues`.
3. Add `Models` to `ModelCallNodeEntity`.
4. Retain JSON-only legacy ingestion properties for `modelId` and `parameterValues`, including internal "property was present" markers so null/empty legacy values can still be detected reliably.
5. Ensure version 2 serialization omits cleared legacy properties.
6. Implement the shared sequential `WorkflowSchemaUpgrader` and the v1-to-v2 mapping.
7. Add post-upgrade validation:
   - Version 1 accepts the legacy representation and migrates it.
   - Version 2 accepts only `models` as authoritative.
   - Version 2 payloads containing legacy fields are rejected instead of merged.
   - A version newer than current is rejected.
8. Make upgrade idempotence and preservation of unrelated graph data explicit tests.

Rejecting legacy fields on a version 2 payload is an important mixed-version protection. An older editor may preserve the workflow version while dropping the unknown `models` field and writing `modelId`/`parameterValues`; accepting that payload would erase fallback configuration.

Gate: every golden fixture upgrades to the expected canonical version 2 JSON, a second upgrade makes no changes, and no unrelated workflow data differs.

### Stage 2: Integrate schema upgrading at every boundary

1. In `RepositoryService.GetWorkflow`, deserialize and upgrade in memory without writing.
2. In `RepositoryService.UpsertWorkflow`, upgrade and validate before persistence, then always assign `entry.Version` as well as node JSON.
3. Keep version and node JSON changes in the same EF Core save operation.
4. In `WorkflowSnapshotSerializer.DeserializeWorkflow`, invoke the same upgrader without rewriting stored snapshots.
5. Verify transfer import reaches the upgrader and export emits canonical version 2.
6. Convert built-in samples to version 2 while retaining at least one legacy sample fixture for tests.
7. Update `TestRepositoryService` and workflow builders to use the current schema; keep explicit legacy helpers for migration tests.
8. Set the frontend's new-workflow default to the workflow schema version 2 without changing the default version of other entity types.
9. Document the current-schema contract on `IRepositoryService` and add a clear engine assertion for unsupported repository output.

Gate: repository get/save/get, transfer round trips, sample creation, new runs, suspended-run resume, and conversation snapshot resume all pass using both legacy and current fixtures. Repository reads demonstrably perform no writes.

### Stage 3: Refactor callers to use an explicit attempt request without changing behavior

1. Introduce an immutable/read-only `ModelCallAttemptRequest` or equivalent containing:
   - Shared node call settings.
   - The selected `ModelCallModelDefinition`.
   - A read-only copy/view of its parameter values.
   - Attempt index and logical call ID where useful.
2. Change `IModelCaller` and every provider caller to consume the attempt request instead of `Node.ParameterValues`.
3. Update all `BaseModelCaller` capability/parameter helpers to read the current model entry.
4. Continue executing only `Models[0]` in this stage.
5. Update provider caller unit tests for OpenAI, Azure OpenAI, Google, Anthropic, and Foundry Anthropic.

Gate: the primary-only characterization tests remain equivalent, including provider request options, streaming events, tools, outputs, and metrics. No fallback is possible yet.

### Stage 4: Add compatibility analysis and the extensible decision override

1. Add a reusable compatibility analyzer that calculates active node requirements from the primary configuration and shared node fields.
2. Return structured errors and warnings rather than formatted strings only, so the engine and frontend can present the same findings.
3. Add normalized failure categories and provider exception mapping while always retaining the original exception.
4. Add immutable `ModelFallbackDecisionContext`.
5. Add the default `ValueTask<bool?> ModelFallbackOverride(...)` method to `IEngineNotification`.
6. Implement the built-in recommendation using the documented transient rules.
7. Invoke registered notifications in DI registration order and use the first non-null result; use the built-in recommendation when all return null.
8. Document notification lifetime and require override implementations to be safe for their configured lifetime and under parallel evaluation runs.
9. Keep hard correctness gates in the engine, outside the notification override.

Gate: table-driven tests cover every default category; notification overrides can allow and veto safe fallback decisions; existing notification implementations require no changes; secrets are absent from the context; and no override can bypass hard safety gates.

### Stage 5: Implement isolated attempts and safe sequential fallback

1. Refactor `ModelCallNode` into logical-call orchestration plus an attempt executor.
2. Resolve and validate the complete ordered model list before calling the primary, preventing a late discovery that a configured fallback is missing or structurally invalid.
3. Add an attempt-state tracker with atomic flags for:
   - Prompt event emitted.
   - Assistant/reasoning/tool response received.
   - Event published and event persisted.
   - Tool invocation started.
   - Context mutation known or possible.
4. Move prompt event emission behind a logical-call coordinator or progress API so it occurs once even when provider attempts change.
5. Give each attempt isolated response/progress buffers while sharing only the logical prompt state.
6. Mark tool invocation before calling a host tool, including batch paths where streamed tool events may not be available.
7. On failure:
   - Preserve the exception and attempt diagnostics.
   - Check cancellation, next-model availability, and hard safety gates.
   - Build the decision context and invoke `ModelFallbackOverride` only when safe.
   - Start the next attempt only on `true`.
8. Write text, chat, image/future output, exit context, and persisted response events only from the successful attempt.
9. If all attempts fail, throw a model-call exception that preserves the individual attempt failures without hiding the most relevant original cause.
10. Keep provider retry behavior bounded and do not add engine retries in addition to fallback during the first implementation.

Gate: failure-injection tests cover every point before and after prompt, first response, reasoning, tool-call receipt, tool invocation, context mutation, and output persistence in batch and streaming modes. Fallback occurs only before the safety cutoff.

### Stage 6: Convert the editor to ordered models

1. Replace frontend `modelId` and node-level `parameterValues` with an ordered `models` signal and snapshot contract.
2. Update dirty tracking and serialization with deep, order-sensitive comparison.
3. Show the first entry as the primary and later entries as fallback cards/rows.
4. Support adding, removing, and reordering any number of fallback entries; the data format and UI should not impose a one-fallback-only migration later.
5. Bind dynamic fields independently to each model entry.
6. Keep shared call settings in the existing tabs and use the primary model to determine available feature tabs.
7. Display structured compatibility errors and warnings for each fallback.
8. Prevent saving hard-incompatible fallbacks, while preserving warnings for allowed differences.
9. Clearly show missing/deleted model references and never silently reorder or replace them.

Gate: Angular production build succeeds; manual checks cover creation, editing, reordering, reopening, v1 auto-upgrade display, warning/error presentation, and a primary-only node. Capture screenshots for the eventual PR because this changes the editor UI.

### Stage 7: Record each provider attempt with existing model-call metrics

Reuse `ModelCallMetric` for provider attempts:

1. Write one `ModelCallMetric` for every attempted primary or fallback provider call.
2. Group attempts with `LogicalCallId` and order them with `AttemptNumber`.
3. Store the attempted model/connector snapshots, duration, success, failure classification/status, usage, and cost on each row.
4. Derive primary, fallback, and final state from attempt order rather than storing redundant summary/finality fields.
5. Keep metrics independent of live model/connector foreign keys so they survive configuration deletion.
6. Add matching SQLite and SQL Server migrations and snapshots.
7. Add attempt spans/events and distinguish attempt order on the node span.
8. Define model-call and workflow-run metric counts as provider attempt counts.

Gate: primary-only metric totals are unchanged; recovered calls write one failed primary row and one successful fallback row with the same logical call ID; the highest attempt number represents the final outcome; and cost/token aggregation follows the documented rules.

### Stage 8: End-to-end hardening, documentation, and release review

1. Run the complete backend test suite, including the Angular production build triggered by the Editor project.
2. Run focused suites repeatedly for migration, repository, transfer, snapshots, streaming, tools, policy, metrics, and each provider caller.
3. Test SQLite end to end and SQL Server migrations/integration in the available CI environment.
4. Exercise parallel evaluation runs to verify scoped policy behavior and attempt-state isolation.
5. Perform a manual upgrade rehearsal using a copy of a real version 1 database and verify that merely viewing workflows does not mutate it.
6. Save selected upgraded workflows and confirm canonical version 2 JSON, preserved graph data, and successful execution.
7. Test export from version 1 storage and import into a clean current instance.
8. Update model-call, models, metrics, transfer, and programmatic configuration documentation.
9. Document rollback limitations: once a workflow is explicitly saved as version 2, an older SharpOMatic version cannot safely edit it.
10. Include compatibility evidence, tests run, migrations, and UI screenshots in the PR description.

Release gate: no known version 1 workflow regression, corruption path, hard-gate bypass, duplicate tool invocation, mixed-version silent data loss, or unexplained primary-only metric/event difference remains.

## Decisions still to make

- Whether credential/authentication failures can be configured as fallback-eligible.
- Exact provider exception mappings and handling of `Retry-After`.
- Whether context-window and output-limit differences are warnings or blocking errors in particular cases.
- Whether to add a total logical-call time budget.
- Whether to add a circuit breaker so repeated calls do not continually hit a known-down primary.
- Whether model dependencies should be automatically included in transfer exports.
- Whether mixed-version editor/API clients need enforced compatibility protection.
