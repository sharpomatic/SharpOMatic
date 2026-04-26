---
title: State Sync Node
sidebar_position: 12
---

# State Sync Node

The `State Sync` node emits AG-UI state updates from `agent.state`.

It wraps the same high-level sync behavior as `Events.AddStateSyncAsync()`:

- if `agent.state` is missing, the node emits nothing
- if no hidden baseline exists, it emits a `StateSnapshot`
- later uses compute a JSON Patch delta
- if the delta would be larger than a snapshot, the node emits a replacement `StateSnapshot` instead
- if nothing changed, the node emits nothing

## Fields

- `Title`
- `Snapshots only`
  When enabled, the node always emits snapshots and never attempts to emit deltas.

## Notes

- The source is always `agent.state`.
- The node stores hidden baseline state at `agent._hidden.state`.
- AG-UI requests seed `agent._hidden.state` from the incoming `state` payload on each start or resume.
- State snapshots can contain any JSON root: object, array, scalar, or `null`.
- In `Snapshots only` mode, each execution emits a snapshot even when a smaller delta would be possible.
- The node continues through its single output after processing the sync.
