# FIX-04 — Mountain prefab sprites lose their owner: owner set before parent, so editor-authored art vanishes on reload

**Type:** fix · **Area:** `MountainPrefabGeneratorComponent` (`NewPartSprite`, `AddBakedPrefabSprite`, `AddVisualSprites`), routed through `TerrainAuthoring.Adopt` · **Status:** **PROPOSED 2026-09-11** · **Effort:** XS (½ day) · **Risk:** low

## Finding

`MountainPrefabGeneratorComponent` generates its art sprites through one factory, `NewPartSprite`, that assigns the sprite's `Owner` **before the sprite has a parent**. Godot requires an owner to be an in-tree ancestor of the node, so the assignment is rejected (it prints an error and leaves `Owner` null); the sprite is then parented but never belongs to the scene *file*, and is dropped on the next reload.

1. **`NewPartSprite` owns before parenting** — `MountainPrefabGeneratorComponent.cs:798-815`. The factory builds the `Sprite2D`, and at `:812-813` does `if (Engine.IsEditorHint()) sprite.Owner = Owner;` while the sprite still has no parent. The sprite is *returned*, and only the caller adds it to the tree afterwards: `AddVisualSprites` at `:340`→`AddChild` `:352`, the placement loop at `:438`→`:445`, and `AddBakedPrefabSprite` at `:730`→`:731`. At the moment `Owner` is assigned, `this` is not yet an ancestor of the sprite, so Godot refuses the owner. `AddBakedPrefabSprite` (`:722-733`) never re-stamps the owner after its `AddChild` either, so both entry points leave every art/baked-prefab sprite ownerless.

2. **The sibling gameplay-node builders in the same file do it the right way**, proving the intended order: `AddWalkableAreas` calls `AddChild(area)` (`:509`) *then* sets `area.Owner`/`collisionNode.Owner` (`:510-514`); `AddRouteConnectorAreas` `AddChild` `:609` then owner `:610-614`; `AddAnchorNodes` `AddChild` `:713` then owner `:714-715`. So a scene generated with `GenerateInEditor` and saved through the editor keeps its walkable/anchor/route nodes but **drops all the art** — the most confusing possible failure, with only a console error to notice.

3. **Why it usually goes unseen** — the explicit save-scene API masks it. `SaveGeneratedSceneToPath` calls `PrepareOwnersForPacking(this, this)` at `:210`, which re-owns every child before `PackedScene.Pack`, so a save *through that API* is correct regardless. The bug is reached only by the ordinary developer path: `GenerateInEditor` (`:41`, `:95-96`, `GeneratePrefab` at `:108-142`) followed by a plain editor **Ctrl+S**, which packs the live tree as-is — ownerless art sprites are not written.

4. **This is also the owner-stamp rule implemented a third time, one copy wrong (rule 3).** The addon already owns exactly this concern: `TerrainAuthoring.Adopt` (`TerrainAuthoring.cs:60-82`) documents and enforces the Godot contract — a node must be `IsInsideTree()` (`:62-63`) and the owner must satisfy `root.IsAncestorOf(generated)` (`:78-79`) — and it deliberately is **not** gated on `Engine.IsEditorHint()` (`:53-58`) so the result can be proven headless by packing. Six renderer creators are already pinned to route through it (`tests/addon_contract_scan.ps1:196-206`, whose comment names this precise "vanishes on reload" failure). `MountainPrefabGeneratorComponent` is absent from that list and hand-rolls two divergent owner idioms instead (the correct-order-but-editor-gated sibling blocks, and the broken-order `NewPartSprite`), one of which is the bug.

## Design

Route every generated node in `MountainPrefabGeneratorComponent` through the existing owner (`TerrainAuthoring.Adopt`), and delete the file's three hand-rolled owner stamps.

- **Remove** the `if (Engine.IsEditorHint()) sprite.Owner = Owner;` from `NewPartSprite` (`:812-813`). The factory returns an unparented, unowned sprite — nothing else changes about it.
- **At each caller, after `AddChild`, call `TerrainAuthoring.Adopt(sprite, this);`** — `AddVisualSprites` after `:352`, the placement loop after `:445`, and `AddBakedPrefabSprite` after `:731`. This matches the intended order the sibling methods already use, and `Adopt` re-checks `IsInsideTree`/`IsAncestorOf` so it is safe on every path.
- **Fold the sibling stamps onto the same call** (rule 3, one owner of the fact): replace the editor-gated `area.Owner = Owner; collisionNode.Owner = Owner;` blocks in `AddWalkableAreas` (`:510-514`), `AddRouteConnectorAreas` (`:610-614`) and `AddAnchorNodes` (`:714-715`) with `TerrainAuthoring.Adopt(...)` after their existing `AddChild`. `Adopt` owns to `EditedSceneRoot` at edit time — strictly more correct than `= Owner` when the generator's own `Owner` differs from the scene being saved — and, being un-gated, is what lets the guard below run headless.
- **Add the generator to the creator pin list** in `tests/addon_contract_scan.ps1:196-206` so the routing cannot silently regress to a hand-rolled stamp.

No new type, no new public surface; the consumer of the change is the mountain-prefab generation path itself, which now packs its art. `PrepareOwnersForPacking` stays as-is (it is a superset re-own that remains correct and harmless once the nodes are already adopted).

## Guards (fail first)

- **Headless pack probe** (new `tests/mountain_prefab_owner_probe.gd`, modelled on `terrain_world_ownership_probe.gd:110-155` `check_saved_tile_view`): instance a `MountainPrefabGeneratorComponent` under a `Node2D` host pointed at a tiny fixture manifest + texture (test fixtures under `tests/`), call `GeneratePrefab()`, then `PackedScene.pack(host)` → `free` → `instantiate` → `add_child`. Assert the packed-and-reloaded tree still contains a child in `GeneratedPartGroup` (the art sprite) whose `owner` is the reloaded scene root.
  - **Assertion:** `generated_sprite != null and generated_sprite.owner == reloaded_root`.
  - **Mutation that trips it:** restore `sprite.Owner = Owner` inside `NewPartSprite` (before `AddChild`) and drop the `Adopt` call — at runtime the owner is never set (assignment rejected: no ancestor), the sprite is packed with no owner, and the reloaded tree has no art sprite → probe fails. This is why the fix must route through the un-gated `Adopt`: with the old `Engine.IsEditorHint()` gate the sprite is ownerless headless too, so the probe already fails on unfixed code and passes only once parenting-then-adopt lands.
- **Contract-scan pin** (`tests/addon_contract_scan.ps1`): add `"MountainPrefabGeneratorComponent.cs"` to the creator list at `:196-206` (must contain `TerrainAuthoring.(Adopt|EnsureLayer)(`), and add a straggler check that `NewPartSprite` contains no `.Owner =` assignment.
  - **Mutation that trips it:** put the `Owner = Owner` back into `NewPartSprite`, or remove the `Adopt` routing → scan `Fail`s.

## Dependencies / collisions

- **No plan-level dependency.** `TerrainAuthoring.Adopt` and its scan pin already exist; this only extends them to one more creator.
- **DUP-13 (`TerrainKindCatalog`)** — unrelated (kind resolution, not node ownership); no overlap.
- **Concurrent-session collision:** a separate session owns `TerrainWorldComponent`/streaming/archive. This change is confined to `MountainPrefabGeneratorComponent` + a test fixture + one scan block and does not touch streaming, so no collision is expected — but `tests/addon_contract_scan.ps1` is a shared file; append the pin rather than reflowing the existing creator block to avoid a merge conflict.

## Out of scope

- The sibling area/anchor/route builders are only *consolidated onto `Adopt`*; their behaviour under editor save is already correct, so no functional change to gameplay nodes is intended beyond the shared owner call.
- `PrepareOwnersForPacking` / `SaveGeneratedSceneToPath` are left unchanged.
- No change to what art is generated, the manifest schema, or the `GeneratedPartGroup`/`WalkableAreaGroup` grouping.
