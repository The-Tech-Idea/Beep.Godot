# Runtime terrain, map loading and persistence

Status: Proposed, 2026-09-11. Existing APIs are named explicitly; new contracts are design targets. All new terrain modes must support the populated [TileMapLayer output](TILEMAP_OUTPUT.md) requested by the owner.

## Use the same generator and tile mapping

Keep terrain math in the existing `TerrainFieldBuilder`/`TerrainGenerationJob` path. A detached generation request contains resolved data, not live Nodes or mutable Resources. Apply cell state, TileMapLayer updates, collision and scene objects on the main thread. Use separate editor and runtime publishers over shared generation and tile-binding code.

Existing `TerrainWorldComponent.BeginNewWorld()` already performs background generation, detects changed settings/targets and stages publication. Extend it through a session adapter instead of creating another generator. Existing `NewWorld()` is synchronous and destructive to the live generated base; it is not the general loading-screen API. `RestoreWorld()` reconstructs recipe data without replacing live cells, and `Redraw()` refreshes views without starting a new world.

## Three initialization routes

| Route | Initialization sequence | Required protection |
|---|---|---|
| Authored | Load native TileMapLayer scene → optional Beep baseline → placed objects → session delta if continuing → ready | Never regenerate on `_Ready` |
| Procedural | Resolve complete recipe/seed → generate → publish cells and tiles → objects/spawns → ready | Seed chosen once per new world; Continue does not reroll |
| Hybrid | Load/generate immutable base → authored overrides → authored objects → session delta → ready | Later layers win deliberately; generation never overwrites player changes |

Native-only games can load the TileMapLayer scene directly and implement their own simulation. The following orchestration applies to Beep gameplay integration.

## Readiness and cancellation

Proposed stages: Requested → Validating → Loading/Generating → PreparingCells → PublishingTiles → CollisionAndNavigation → RestoringEntities → Ready. Every stage reports status through one request identity and a shared load/session barrier.

Wrap generation with `GameStateManagerComponent.BeginWorldLoad` and `CompleteWorldLoad`, including failure/cancellation and owner removal. Disable gameplay commands, spawning, clock advancement and saves while the destination is not ready. Do not rely on the lab UI being present to enforce those rules. Standalone maps without GameApp need the same local readiness contract.

Before publication, cancellation discards staging and leaves the old map intact. The current async generator's live-cell publication is already a cancellation boundary: after that, do not pretend cancellation restores the old world. Finish required consistency work or enter a recoverable failed state. If rollback after commit is required, budget and retain the prior logical/render/collision version explicitly.

Runtime edits validate an affected region, update logical cells in a batch, notify with change kind and chunks, refresh TileMapLayer cells plus transition neighbors, and invalidate relevant navigation/collision. Admit simulation only when the affected gameplay surface is consistent. Pure visual changes must not alter terrain identity or navigation. Native TileSet physics/navigation and Beep grid navigation are distinct consumers; define which backend each game uses.

## Save layers and identities

| Layer | Persistence | Mutability |
|---|---|---|
| Published map | `res://` scene, TileSet dependencies, manifest and optional Beep baseline | Immutable during play |
| World instance | WorldInstanceId, MapId, published revision/hash or pinned effective recipe, seed | Fixed for that run except explicit migration |
| Session delta | Edited cells, tile overrides where needed, roads, resources, placed/destroyed entities, jobs and clock/session state | Mutable, slot-scoped |
| Residency/cache | Live chunk archive, temporary generation data, render caches | Rebuildable or staging data; not implicitly a durable save |

Stable entity records need EntityId, prefab/catalog ID, world/map identity, cell/transform and versioned component records. NodePaths remain convenient scene bindings but are not durable identities. Restore in passes: create missing entities → apply component state → resolve relationships → rebuild footprints/reservations/jobs → enable behavior. Detect duplicate IDs and missing prefab references before touching the live world. Destroyed authored entities need tombstones so they do not return on load.

A seed alone cannot guarantee compatibility across algorithm, catalog or TileSet changes. Store schema and generator versions, content hashes and exact effective settings. For unsupported versions, either use a stored full baseline, invoke a tested migration or reject with an actionable message. Never silently regenerate with new defaults.

## Resident and streamed saves

Resident worlds can build on the existing recipe and `GridWorldStateComponent` records, but still require unique keys, complete recipes and dynamic entity reconstruction. A streamed world must not call the full resident snapshot and suppress its unavailable-chunk error.

Integrate the existing [ENH-10 plan](../../plans/terrain-grid/ENH-10-streamed-world-save.md) through this transaction:

1. Establish a consistent save epoch by pausing relevant mutation or snapshotting versioned immutable chunk views.
2. Flush/copy every dirty or archived chunk referenced by the save, plus entity/session data, to a slot-specific staging revision.
3. Verify hashes, sizes and complete coverage of required chunks. Archive files must never be overwritten through shared mutable hard links.
4. Publish the new manifest/slot pointer last. Preserve the last complete revision if a write, space check or validation fails.
5. Restore from the immutable saved revision into a fresh writable live archive; never point gameplay writes into a saved slot.

`GameStateManagerComponent.Save(int)` currently returns a synchronous boolean. Add an explicit request state/result or awaitable API with progress, cancellation and failure. Preserve a documented synchronous compatibility path for small resident saves. Report “saved” only after durable publication succeeds, not when an archive flush was merely queued. Reconcile this with the older ENH-10 assumption about asynchronous saves.

On load, validate the entire manifest and its referenced files before publishing the new session. Required starting chunks must be resident and usable before Ready; distant chunks may remain archived. Corruption must not quietly produce unexplored/default terrain. Save-slot A and B must remain isolated across subsequent edits and evictions.

## Performance scope

Support finite maps first; “dynamic” does not automatically mean infinite. The initial target matrix is 64×64 authored, 256×256 procedural and 1024×1024 stress maps, not a promise that all configurations already meet a frame budget. Record hardware, cell/object counts, projection, peak memory, worker time, main-thread publication time, frame p95/p99, save latency and bytes written.

Proposed initial main-thread tile-publication budget: configurable 2–4 ms per frame on the agreed reference desktop, measured including relevant native update cost. Treat this as a target to calibrate, not an achieved benchmark. Enforce cancellation responsiveness and a bounded resident chunk budget. Instrument before optimizing. Reuse the existing ENH-03/04/05/06/07 and ENH-13 work rather than adding a separate streaming engine.

## Runtime acceptance

- Same effective recipe and compatible versions produce the same logical hash in editor and runtime.
- Generated TileMapLayer tuples match the committed logical map; repeated load preserves stored visual alternatives where required.
- Rapid New Game requests, cancellation and owner removal never publish stale results or leak workers.
- Continue restores edited water, mined resources, a placed building and jobs without a second base generation or duplicate entities.
- Save slot A, edit/evict/save B, then load A: A remains unchanged.
- Fail a chunk write or corrupt a manifest: retain the last complete save and report the failure.
- Run an exported game from a fresh folder without development caches; authored maps load and procedural maps generate.

Implementation: BGB-05 through BGB-08, BGB-11, BGB-12 and BGB-13.
