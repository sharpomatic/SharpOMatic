# Entity Timestamp Plan

This document records the planned addition of creation and modification timestamps to user-managed persisted entities. It is a design and implementation checklist; the timestamp changes described here have not yet been implemented.

## Conventions

- Use non-null `DateTime` properties named `Created` and `Modified`.
- Store UTC values only and generate them on the server.
- On insert, set `Created` and `Modified` to the same value.
- On update, preserve `Created` and set `Modified` to the current UTC time.
- Clients must not be able to choose or overwrite either timestamp.
- Use one timestamp value for all changes committed as part of the same logical operation.
- Existing `Conversation.Updated` can remain unchanged for compatibility. New user-managed entities should use `Modified`; the UI may label either property as "Modified".

## Entities to Change

| Entity | Created | Modified | Required behavior |
| --- | --- | --- | --- |
| `Workflow` | Add | Add | Update `Modified` when the workflow is saved, renamed, moved between folders, copied, or materially changed. |
| `WorkflowFolder` | Existing | Add | Update `Modified` when the folder is renamed. |
| `ConnectorMetadata` | Add | Add | Update `Modified` whenever connector details, configuration, or credentials are saved. |
| `ModelMetadata` | Add | Add | Update `Modified` whenever model details or parameter values are saved. |
| `Asset` | Existing | Add | Update `Modified` when the asset is renamed, moved, replaced, or its metadata changes. |
| `AssetFolder` | Existing | Add | Update `Modified` when the folder is renamed. |
| `EvalConfig` | Add | Add | Update `Modified` for changes to the configuration and for changes to its columns, graders, rows, or row data. |
| `Setting` | Optional | Add | `Modified` is useful for editable settings. `Created` is optional because many settings are seeded rather than created interactively. |

`EvalConfig.Modified` should represent the modification time of the entire evaluation definition. Individual `EvalColumn`, `EvalGrader`, `EvalRow`, and `EvalData` records do not need their own timestamps.

## Entities That Do Not Need Generic Timestamps

- `ConnectorConfigMetadata` and `ModelConfigMetadata` are embedded system definitions whose versions already describe changes.
- `Run`, `Trace`, `EvalRun`, `EvalRunRow`, and `EvalRunRowGrader` have lifecycle timestamps such as `Started`, `Finished`, and `Stopped`.
- `StreamEvent`, `Information`, `ModelCallMetric`, and `WorkflowRunMetric` are append-only records and already have `Created` or equivalent lifecycle timestamps.
- `Conversation` already has `Created` and `Updated`.
- `ConversationCheckpoint` already records `CheckpointCreated` for the current checkpoint.

## Persistence and Migrations

1. Add the properties to the EF repository entities.
2. Create matching migrations for both `SharpOMatic.Engine.Sqlite` and `SharpOMatic.Engine.SqlServer`.
3. Update both provider model snapshots.
4. Backfill existing records because their real historical timestamps cannot be recovered. Set both new values to the migration/backfill time, or use a single explicitly documented baseline UTC value.
5. Add indexes where timestamps drive paged sorting:
   - `Workflow.Modified`
   - Asset scope/folder indexes that include `Asset.Modified`, matching the existing asset query patterns
6. Ensure database defaults are not used as a substitute for update logic. The repository or a shared EF timestamp mechanism must set `Modified` on every relevant update.

A shared timestamp interface or SaveChanges interceptor may reduce duplication, but it must also cover repository operations that update related children and need to touch a parent such as `EvalConfig`.

## Repository Update Paths

Review and update all mutation paths, including:

- Workflow create/upsert, copy, sample creation, import, rename, and folder move.
- Workflow-folder create/upsert and rename.
- Connector create/upsert.
- Model create/upsert.
- Asset create/upsert, rename, move, and replacement.
- Asset-folder create/upsert and rename.
- Evaluation configuration changes and all column, grader, row, and data mutations that should update the parent `EvalConfig.Modified`.
- Editable setting upserts if `Setting.Modified` is adopted.

Delete operations do not need to update timestamps immediately before deletion.

## Workflow API and JSON

- Add `Created` and `Modified` to `WorkflowSummary` so listing endpoints can return both values.
- Add at least `Modified` to the frontend workflow-summary snapshot/entity.
- Decide whether full workflow JSON and transfer exports should include timestamps:
  - Recommended: expose timestamps as read-only metadata when returning a workflow.
  - Ignore client-supplied timestamp values during normal saves.
  - For imports, either preserve the original timestamps or treat the import as a new local creation. The chosen behavior must be documented and tested consistently.
- Add `Modified` to `WorkflowSortField` in both the backend and frontend.
- Extend repository workflow sorting to order by `Workflow.Modified`, with a stable secondary order such as name and ID.

## Workflows Screen

The Workflows table must:

- Display a **Modified** column for every workflow.
- Format the value in the user's local time using the Angular date pipe, consistent with other date columns.
- Provide the full timestamp as hover/title text if the visible format is abbreviated.
- Make the column header sortable.
- Send `WorkflowSortField.Modified` and the selected `SortDirection` to the existing paged workflow endpoint.
- Show the existing ascending/descending sort indicator.
- Keep sorting on the server so pagination remains correct.
- Use a fixed column width that does not cause the Name or Description columns to become unusable.

The existing default Name/ascending sort can remain. When the user first selects Modified, descending order is recommended because the most recently changed workflows are normally the useful result.

## Asset API and Entities

- Add `Modified` to the persisted `Asset` and to every asset DTO/entity used by the library-assets listing endpoint.
- Add `Modified` to `AssetSortField` in both the backend and frontend.
- Extend repository asset sorting to order by `Asset.Modified`, using name and ID as stable secondary ordering.
- Preserve the existing `Created` field and sort capability.

## Connector, Model, and Evaluation Summaries

- Add `Created` and `Modified` to the backend and frontend connector summaries.
- Add `Created` and `Modified` to the backend and frontend model summaries.
- Add `Created` and `Modified` to the backend and frontend evaluation-config summaries.
- Add `Modified` to `ConnectorSortField`, `ModelSortField`, and `EvalConfigSortField` in both the backend and frontend.
- Extend the corresponding repository queries to sort by `Modified` on the server, using name and ID as stable secondary ordering.
- Keep the existing search and pagination behavior unchanged.

## Assets Screen

The Assets table already displays and sorts by **Created**. It must additionally:

- Display a **Modified** column.
- Format it in the user's local time using the same format as Created.
- Make the Modified header sortable through `AssetSortField.Modified`.
- Show the existing ascending/descending sort indicator.
- Keep sorting on the server so pagination remains correct.
- Retain the Created column so users can distinguish asset age from recent changes.

As on the Workflows screen, the first selection of Modified should preferably sort descending.

## Transfer Export Screen

The Transfer page has separate export-selection tables for workflows, connectors, models, evaluations, and assets. Every table must display a sortable **Modified** column so users can identify recently changed items that need exporting.

Required changes by export table:

| Export selection | Timestamp source | Sort field |
| --- | --- | --- |
| Workflows | `WorkflowSummary.Modified` | `WorkflowSortField.Modified` |
| Connectors | `ConnectorSummary.Modified` | `ConnectorSortField.Modified` |
| Models | `ModelSummary.Modified` | `ModelSortField.Modified` |
| Evaluations | `EvalConfigSummary.Modified` | `EvalConfigSortField.Modified` |
| Assets | `AssetSummary.Modified` | `AssetSortField.Modified` |

All five export-selection tables must:

- Show Modified in the user's local time using a consistent Angular date format.
- Provide the full timestamp as hover/title text if the visible format is abbreviated.
- Make the Modified header clickable using the table's existing sort handler and indicator.
- Perform Modified sorting on the server so the existing 50-row pagination remains correct.
- Prefer descending order the first time Modified is selected.
- Preserve selected export IDs when the user changes sort order or moves between pages, following the Transfer page's existing selection behavior.
- Continue supporting search while sorted by Modified.
- Use stable secondary ordering so items with identical timestamps do not move unpredictably between pages.

The Assets export table already shows and sorts by Created. Keep that column and add Modified alongside it. The other export tables only require Modified for this feature, although their summary contracts may also carry Created for future display or filtering.

The Transfer page reuses the same summary endpoints and sort enums as the main listing screens. The Modified implementation should therefore be shared rather than introducing transfer-specific timestamp queries or DTOs.

## Documentation and Validation

- Update the user documentation for the Workflows and Assets screens.
- Add repository coverage for insert timestamp initialization, preservation of `Created`, and advancement of `Modified` after updates.
- Verify workflow and asset sorting in both directions with pagination and tied timestamps.
- Verify Modified sorting for all five Transfer export-selection tables, including search, pagination, and retained selections.
- Verify SQLite and SQL Server migrations from an existing database.
- Verify that imports, copies, moves, and renames apply the chosen timestamp semantics.
- Verify that API clients cannot overwrite server-managed timestamps.
