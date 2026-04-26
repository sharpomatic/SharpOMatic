---
title: Activity Sync Node
sidebar_position: 11
---

# Activity Sync Node

The `Activity Sync` node emits AG-UI activity updates from a JSON object stored in workflow context.

It wraps the same high-level sync behavior as `Events.AddActivitySyncFromContextAsync(...)`:

- first use for an instance emits an `ActivitySnapshot`
- later uses compute a JSON Patch delta
- if the delta would be larger than a snapshot, the node emits a replacement snapshot instead
- if nothing changed, the node emits nothing

## Fields

- `Title`
- `Instance Name`
  The activity instance id used for AG-UI `messageId` and hidden state tracking.
- `Activity Type`
  The AG-UI activity type such as `PLAN`.
- `Context Path`
  The path to a JSON object in workflow context that represents the current activity state.
- `Replace on first snapshot`
  Controls the `replace` flag on the first emitted snapshot only. Replacement snapshots used later as a fallback are always emitted with `replace: true`.
- `Snapshots only`
  When enabled, the node always emits snapshots and never attempts to emit deltas.

## Notes

- The context path must resolve to a JSON object.
- The node stores hidden baseline state in workflow context so it can compare later executions.
- If the same `Instance Name` is reused with a different `Activity Type`, execution fails.
- In `Snapshots only` mode, each execution emits a snapshot even when a smaller delta would be possible.
- The node continues through its single output after processing the sync.
