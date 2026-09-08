# GridJobEffectComponent

`GridJobEffectComponent` is the translator between "a job finished" and "the world changed": it listens for `GridJobQueueComponent.JobCompleted` and turns the job's kind into a concrete mutation — clear land, till, water, harvest a crop, or gather from a resource node. The class doc comment states its purpose plainly: "This turns settler/worker jobs into land state changes without custom project glue." Without it, `GridJobQueueComponent` would complete jobs that do nothing, and every project would need to hand-wire its own `JobCompleted` handler.

It exists as a separate component from the queue itself, and from `GridToolActionComponent`, so that job-kind vocabulary is not the queue's concern — the queue only ever sees an opaque `kind` string. This component owns the mapping from that vocabulary (`"clear_land"`/`"clear"`, `"till"`/`"hoe"`/`"prepare_soil"`, `"water"`, `"harvest"`, and the gather synonyms `"gather"`/`"collect"`/`"forage"`/`"chop"`/`"mine"`/`"fish"`) to an effect, and it is opt-out via `AutoConnect` and disconnect-safe (`DisconnectQueue` checks `GodotObject.IsInstanceValid` before unsubscribing) so an orchestrator can drive `ApplyJobEffect` manually instead. Gather jobs are checked before the `_cells == null` guard specifically because they route to a `GridResourceNodeComponent` instead of `GridCellDataComponent` and don't need cell data at all.

## Public API
- Clear jobs preflight `GridToolActionComponent.CanClearCell` before collecting a resource node. A terrain rejection leaves the deposit and wallet untouched. This does not provide transactional rollback for application signal handlers that mutate state during collection.
- Clear, till, water and harvest reject `missing_tool_action` when their tool-routing flag is enabled and an explicit tool path is missing. An empty path can discover a tool; direct cell mutation remains available when tool routing is disabled or no tool is configured/discovered.
- After replacing a queue, call `ConnectQueue()` to bind its completion signal. Resolution checks node identity, not only the path string; a missing or changed target detaches the previous subscription. A completion from a stale queue cannot look up its job ID in the replacement queue.
- `void ConnectQueue()` — resolves references and subscribes to the target queue's `JobCompleted`; no-ops if already connected to the same queue, and disconnects any prior queue first if the target changed.
- `void DisconnectQueue()` — unsubscribes if still validly connected.
- `bool ApplyJobEffect(string jobId)` — looks up `kind`/`cell` from the resolved `GridJobQueueComponent` and delegates to the cell/kind overload; rejects with `"missing_job_queue"` if no queue is resolved.
- `bool ApplyJobEffect(string jobId, string kind, Vector2I cell)` — the actual dispatcher: normalizes `kind`, routes gather synonyms to `ApplyGather`, otherwise requires `_cells` to be resolved and switches on `"clear_land"/"clear"`, `"till"/"hoe"/"prepare_soil"`, `"water"`, `"harvest"`; anything else rejects as `"unknown_job_kind"`.
- Signals: `JobEffectApplied(jobId, kind, x, y, effect)`, `JobEffectRejected(jobId, kind, x, y, reason)`.
- Exports: `JobQueuePath`, `CellDataPath`, `ToolActionPath`, `ResourceNodesRootPath`, `AutoConnect` (default on), `UseToolActionForHarvest` (default on), `ClearLandGathersResourceNode` (default on).

## Dependencies

- Subscribes to `GridJobQueueComponent.JobCompleted` (this batch) and calls its `GetJobKind`/`GetJobCell`.
- Calls `GridToolActionComponent.ApplyToCell(cell, ToolAction.Harvest)` (this batch) for harvest jobs when `UseToolActionForHarvest` is true and a tool-action component is resolved — this lets a completed "harvest" job reuse the same wallet/crop-catalog-aware harvest path a player's direct tool click would use, instead of a second, cruder implementation. Falls back to `GridCellDataComponent.HarvestCrop` directly otherwise.
- Calls `GridCellDataComponent.ClearLand`/`Till`/`Water`/`HarvestCrop` (outside this batch) for the non-gather, non-tool-routed effects.
- Calls `GridResourceNodeComponent.GatherForJob`/`GatherAllForJob`/`CurrentCell`/`IsDepleted` (outside this batch) for gather jobs and for `ClearLandGathersResourceNode`; these are real typed calls, not reflection-based duck typing, so `GridResourceNodeComponent` is treated as a known sibling type rather than a duck-typed participant.

## Notes

- `FindResourceNodeAt` searches twice: first the `GridResourceNodeComponent.ResourceNodeGroup` group (fast, but only finds nodes that joined the group and are a descendant of the resolved root), then falls back to a full recursive tree walk from the root if the group search comes up empty. The recursive fallback makes the group search redundant for correctness — it only saves a full-tree walk when the group happens to contain the answer.
- `OnJobCompleted` discards `ApplyJobEffect`'s bool return; the job either raised `JobEffectApplied`/`JobEffectRejected` itself, so nothing is lost, but there's no way for the queue's own completion path to know whether the effect landed.
- The seed/reward economy this component touches (via `ApplyHarvest` → `GridToolActionComponent`) is shared, not duplicated, with the tool-action path — see that file's notes for the deposit logic.
