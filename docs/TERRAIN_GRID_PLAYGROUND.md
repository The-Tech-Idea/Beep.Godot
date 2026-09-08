# Terrain Grid Playground

Run `res://tests/examples/terrain_grid_playground.tscn` with Godot's Run Current Scene.
It inherits the painted terrain demo and reuses the existing truck PackedScene.
No HUD controls are created in code; toolbar buttons are authored KitButton nodes.

## Actions

- Move: select a map cell to route the truck through GridNavigationComponent.
- Gather: select the stone marker. The truck travels there and gathers into GridResourceWalletComponent.
- Flatten: select a cell to reset its live relief/elevation metadata.
- Flood: select a cell to change its live kind to water, including during an active route.
- Regenerate: increment the seed and rebuild the map, resetting the demonstration deposit.
- Save: write one checkpoint when the truck is idle.
- Load: restore that checkpoint's recipe, live cells, wallet, deposit and truck cell.
- Clear job: select workable land to queue timed clearing for the truck.
- Cancel jobs: release the truck and remove pending jobs without applying their effects.
- Place shelter: create a construction site on valid, unoccupied ground while the truck is idle. The truck travels to a work face and completes a queued build job.
- Demolish: remove a shelter and release its placement/navigation footprint.

The camera supports the inherited pan/zoom controls and stays within map bounds.
The starting pair is chosen from traversable inland cells. No alternate terrain generator or
private resource balance is used. The wallet is retained across regeneration; this is not a new-game flow.

## Wiring

TerrainWorldComponent binds the live cell store, painted renderer, resource icons, relief,
navigation and grid. The truck uses GridPathFollowerComponent. Resource icons observe an authored
deposit subtree. ResourceChanged updates the view after gathering. Flooding changes the same cell
store read by both drawing and navigation, so active routes can fail rather than crossing water.

TerrainCollisionComponent supplies native water/rock collision from the same live grid.
GridToolActionComponent validates queued clearing. GridJobQueueComponent owns the work ledger,
GridWorkerComponent executes it after actual arrival, and GridJobEffectComponent applies the
clear effect through the same tool component. The authored KitMeter reads queue-owned progress.
The sample dispatches only when the truck is not following a direct command. Direct move/gather
and checkpoint capture are rejected while work is assigned or queued. Regeneration and loading
cancel current/pending work; active job persistence is not part of the demo checkpoint.
GridWorldStateComponent captures live cell/navigation state; the sample orchestrates its snapshot
with TerrainWorldComponent, the wallet and the deposit's existing CaptureState/RestoreState APIs.
Load bypasses new-world spawn initialization. The saved truck cell is projected through the restored
grid, not reused as a world-space position from another view.

The second authored toolbar drives GridPlacementComponent and a Buildings subtree. Shelters use
the repository's house sprite and an authored GridObjectComponent. Placement rejects the truck's
cell, a live deposit, water and occupied terrain. The sample disallows flooding occupied shelter
cells; demolition frees those cells for routing again. Regeneration removes previous shelters.
The authored BuildCatalog defines one turn of shelter work. BuildSites chooses a live navigation
approach and creates a normal build job. The same truck and KitMeter used for clearing execute and
display that work. Cancel Jobs cancels individual records before clearing the queue, allowing build
site teardown to remove unfinished shelters. There is no demo-side construction timer.
Checkpoint version 2 records completed shelter cells and recreates them through placement after
restoring terrain, with build-site subscriptions temporarily disconnected so load does not restart
construction. Saving unfinished construction is rejected. Previous demo checkpoints are intentionally
unsupported. Material delivery and construction-stage art are not yet demonstrated.

The single checkpoint uses Godot FileAccess variant serialization with object deserialization disabled,
at user://terrain_grid_playground.save. checkpoint_path is exported for isolated tests. Active routes
are not persisted; saving while moving is rejected. This is a demo checkpoint, not a transactional,
version-migrating multi-slot save service.

This is an integration example, not a full settlement game: no building catalog menu, multi-worker dispatch,
save slots, authored-isometric tiles or projection chooser are included yet. Gathering occurs on
arrival in the sample controller, through the existing GatherAllForJob API.

## Verification

`tests/terrain_grid_playground_probe.gd` instantiates the real scene, activates authored toolbar
signals, verifies movement/gathering into the wallet, disappearance of the deposit marker,
route invalidation after flooding, live relief editing, and disk checkpoint restoration after a
changed seed and stock/terrain edits. Depleted deposits stay depleted; live navigation and physical
collision recover from post-save flooding. It passes headless and OpenGL.
The same probe verifies queued clear work, intermediate progress, delayed vegetation removal,
cancellation without effects and rejection of clear jobs on water.
It also places and demolishes a shelter, checks navigation blocking and overlap rejection, restores
the shelter from a checkpoint, and verifies regeneration removes it. Construction checks observe
intermediate work progress and completion, cancel a queued site with footprint cleanup, and verify
loading a completed shelter does not create a build job.
Pass `-- --capture` to write `tests/output/terrain_playground/start.png` and `building.png`.
