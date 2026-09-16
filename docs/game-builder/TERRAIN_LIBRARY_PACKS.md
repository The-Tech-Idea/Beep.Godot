# Terrain library packs

## Status

The opt-in renderer foundation and terrain/elevation-profile Explicit Apply workflow are
implemented for `Tiles` and `IsometricAutotile`.
This is not completion of the terrain library rebuild. Dedicated isometric
artwork, cliff composition, structure placement, and the
remaining cartoon/pixel collections are still pending. No assets were promoted,
moved or deleted by this implementation.

## Renderer selection

Assign `LibraryPack` on `TerrainTileRendererComponent` or
`TerrainIsometricAutotileRendererComponent`. Leave it empty to retain their existing
atlas/direct-TileSet configuration. `Painted` and block `Isometric` do not consume
these packs. Map shape and climate do not select a pack or change projection.

New square packs use 64x64 cells without the legacy half-cell offset. Isometric
packs use 64x32 diamonds and Godot's native map transforms. The renderer rejects a
pack for the other projection. Source artwork must be authored for its projection.

## Water under a pack

A pack build clears the renderer's shader sea. A pack brings its own shoreline tiles, so the
shader sea would draw a second one over them. `Tiles` has behaved this way since packs were
introduced; `IsometricAutotile` gained a sea in VIEW-04 (2026-09-16) and applies the same rule.
Both keep `WaterShaderPath` and their [`TerrainWaterLook`](../terrain-engine/TerrainWaterLook.md)
assignment — clearing the pack and rebuilding brings the shader sea back; a pack does not
reconfigure them. Pack water tiles themselves remain part of the terrain art library's scope, not
this renderer's.

The current real-art candidate is:

`res://addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/godot/grass_dirt_library_pack.tres`

It supports `grass` and `dirt` only. Assign `CellDataPath` to a live
`GridCellDataComponent`, or `TerrainGeneratorPath` for a recipe-only preview, and
call `Rebuild()`. A generated map containing other kinds is rejected with diagnostics,
not silently drawn with substituted terrain. Use the adjacent `grass_to_dirt.tres`
directly in a native TileMapLayer when no C# engine is required.

## Contract

- Pack ID/version, projection and pixel/cartoon filtering are explicit.
- `Tiles` owns cell shape, terrain matching mode, regions and animation metadata.
- `TerrainBindings` maps normalized engine kind names to Godot terrain IDs.
- Complete binary validation requires two bound IDs and all 47 legal masks per
  nonnegative ID. One ID can be -1 with an explicit opaque background tile.
- An intentionally smaller side/corner connection set can disable the binary-47
  requirement, but remains responsible for its authored combinations.
- Elevation profiles bind explicit logical values to authored tile variants.
  Structure scene references remain reserved for a future structure renderer.
- Pack-backed ground currently uses one prepared TileSet on one native layer.
  Full multi-pack/layer composition is a later milestone.

Full builds prepare off-screen and publish only after all requested logical cells
are represented. A pack that fails validation or cannot draw a cell keeps the
previously published display, and both renderers report why through
`GetPaintDiagnostics()` (`valid`, `reason`). `TerrainWorldComponent` shows that
reason as `View incomplete: …` in its status line and fails generation with it. Individual live-cell edits update affected neighbors with a
context margin. Bulk changes still rebuild. Widely separated edits currently
share a bounding context rectangle; chunk-based performance tuning remains pending.
`LibraryCellsUpdated` reports published cells, not all context work or render cost.

## Native editing

1. Assign a valid pack and live `CellDataPath`, then rebuild the renderer.
2. Select the renderer in the Inspector and choose **Edit**. The plugin selects
   its persistent `TerrainEditSession/WorkingTerrain` TileMapLayer.
3. Paint with Godot's terrain Connect/Path brushes. Apply/Discard controls also
   appear when that working layer is selected. Do not paint the generated display.
4. **Apply** validates the complete working region, including adjusted neighbors,
   and commits terrain kinds and elevation profiles as one editor undo action. **Discard** closes the
   working copy without changing live cells. Undo Apply reopens the pending copy.

An active session blocks renderer rebuilds and the configured world's regeneration,
restore, redraw and projection switching. Applying requires unchanged terrain-owned
baseline data, bounds, source and pack configuration. Conflicts retain both versions;
discard and restart against the current map after reviewing the pending copy.
Unrelated metadata, flags and crops are merged from the latest live records,
including during undo. Conflicting history operations are rejected with a warning.

Erasure maps to the pack's explicit background kind. Ambiguous reverse bindings,
out-of-bounds painting and incompatible manually placed connection tiles are rejected.
Compatible cosmetic alternatives persist per renderer and are restored only while
their terrain kind and neighbor connections still match.

Save the scene to persist the working layer and its baseline. A scene also stores
an editor-only cell seed, used only when reopening into an empty editor cell store.
This does not replace runtime persistence: save/load committed logical edits using
the existing cell/world save APIs. Pending runtime sessions require that live
baseline data to be restored before Apply. Recipe-only previews are not editable.

The working layer has collision and navigation disabled. It is not a second
gameplay surface. Existing world navigation rules remain authoritative.

## Elevation profiles

Elevation is opt-in. The existing grass/dirt art candidate has no authored height
variants, so it does not expose elevation controls. Do not enable profiles on flat
art and assume the engine will create cliffs.

For a prepared pack with separately authored height variants:

- Set `ElevationCustomDataLayer` to a String custom-data layer in its TileSet,
  for example `elevation_profile`.
- Define stable profile IDs in `ElevationValues`, with explicit normalized 0-1
  values for the engine's `terrain_elevation`. Pixel heights are rejected here.
- Define the same IDs in `ElevationRisePixels`, with measured visual rises such
  as base 0px, quarter 16px, half 32px and standard 64px. These are not a formula
  for normalized engine elevation. Keep profile identities/values consistent
  across projection packs; their artwork and pivots remain projection-specific.
- Assign each profiled tile its ID in the custom-data layer. Supply matching
  terrain connections for every profile, including the explicit background.
  Keep unprofiled base variants for each connection. Missing coverage is rejected.
- Set profiled variants' probability to zero. They are selected explicitly, not
  randomly by ordinary Connect/Path painting. Author texture origins, cliff faces
  and sorting deliberately; the engine does not move the logical cell footprint.

During Edit, choose a profile under **Elevation Region**, enter cell X/Y and
width/height, then **Set Elevation**. This stages an undoable working-layer change;
Apply commits it with any ground edits. Manually selecting a compatible profiled
tile also works. To lower a region, use an explicit zero-elevation/base profile.
Ordinary unprofiled terrain painting and erasure preserve the current elevation.

Committed cells store the profile ID as `terrain_library_elevation` and its
normalized value as `terrain_elevation`. Profile-only edits preserve water records,
relief, flags and other gameplay metadata. No stairs, ramps, jump rules or new
navigation levels are inferred. Both pack renderers consume the shared ID and
select compatible authored variants; cosmetic alternatives cannot override a
different elevation. A missing live profile retains the last working display
with a diagnostic. Packs without elevation authoring retain their flat behavior.

This is per-cell profile selection, not height-aware cliff-face adjacency or
multi-layer mountain composition. Those still need dedicated connectors,
structures and visually approved artwork. Tests use diagnostic atlases only.

## Verification

```powershell
dotnet build --no-restore -v quiet
& 'H:/dev/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --script tests/terrain_library_pack_probe.gd
& 'H:/dev/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --script tests/terrain_library_edit_probe.gd
& 'H:/dev/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . --script tests/terrain_library_elevation_probe.gd
node --test tests/terrain_library.test.mjs tests/river_motion.test.mjs
```

The Godot probe uses diagnostic, flat-color atlases for both projections, not
approved artwork. It checks 47-mask coverage, native placement/picking, incremental
versus full repaint, cancellation, and retention after invalid configuration.
It also loads the real grass/dirt candidate and switches back to the existing
15-piece atlases with identical tile data and the original half-cell offset.
Existing view-grid and isometric rebuild-staleness probes pass as well.
The edit probe covers both projections: native terrain painting, erasure, invalid
joins, bounds changes, cosmetic alternatives, atomic conflict rejection, undo/redo,
gameplay metadata preservation and pending scene-file save/reload with restored
live cells. Editor plugin startup is checked headlessly; interactive Inspector
buttons, scene undo/redo and editor-only seed restoration are exercised by the
maintained editor probe below. Pointer-driven brush ergonomics and visual layout
still need manual review.
The elevation probe checks profile coverage and validation, explicit normalized
values, retained authored pivots, staged edits, undo, shared cross-projection data,
incremental/full equivalence, invalid-profile display retention and scene-file
roundtrips with fractional elevation. These are contract tests, not visual approval.
Visual review and all-view regression/performance coverage remain separate gates.

### Editor workflow probe

Run the elevation probe first to generate diagnostic fixtures, then open the
maintained harness with its explicit test flag:

```powershell
& 'H:/dev/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --path . --quit-after 18000 res://tests/terrain_library_editor_probe.tscn -- --terrain-library-editor-probe
```

Require the `TERRAIN LIBRARY EDITOR: PASS` marker, not just a zero process exit:
Godot can exit successfully after a script parse error or a frame timeout.
Without the explicit flag, opening the harness scene does not run tests.

The harness makes uniquely named disposable scene copies in test output so an
already-open editor tab cannot mask a disk reload. It shows the Inspector during
the test and restores controls it temporarily exposed. It exercises the actual
Edit, Apply, Set Elevation and Discard buttons, the scene's editor undo history,
editor pre-save notifications, pending reload into an empty editor store, and
preservation of newer gameplay metadata in both projections. Thumbnail generation
is disabled for headless saves. Existing maps and source fixtures are not edited.

Elevation controls are disabled outside an active edit session. A failed Edit
start preserves the prior session bindings and cosmetic overrides; the runtime
edit probe also checks that regression. This run passed with sandbox-only root
certificate and global editor-settings write warnings; it is not visual approval.

The history and save integration uses Godot's documented
[EditorUndoRedoManager](https://docs.godotengine.org/en/4.4/classes/class_editorundoredomanager.html)
and [EditorInterface](https://docs.godotengine.org/en/4.5/classes/class_editorinterface.html)
APIs, verified against the installed Godot 4.7.2 build.

## Authored structure placement foundation

`TerrainStructureLayerComponent` is an opt-in scene placement API with an Inspector
workflow for staged additions. Assign `LibraryPack`, `MappingLayerPath`
to a TileMapLayer using that pack's TileSet, and `CellBounds`. Populate the pack's
existing `Structures` scene dictionary and the matching `StructureLayouts` entries.
Old scene catalogs without layouts remain untouched; placement requires both.

Each `TerrainStructureLayout` defines a positive cell footprint, scene-local pivot,
vertical rise in game pixels, and relative Z index. The anchor is the footprint's
minimum logical cell, not its geometric center. `Place(placementId, structureId,
anchor)` maps that cell's native center to the authored pivot, including transforms
on both layers. Authored root translation is replaced; rotation and scale remain.
Rise records the asset's measured height and does not lift it a second time.

Placement returns the saved Node2D instance or null with `Problem`. Duplicate IDs,
missing art/layouts, incompatible TileSets and out-of-bounds footprints are rejected.
It never writes terrain, collision or navigation cells. Collision nodes or scripts
already inside an authored scene retain their own behavior; use trusted scenes.
Overlapping structures are allowed; height-aware occlusion, connector validation,
and removing existing structures remain pending. Register this component
with its world as described below, or manage standalone visibility yourself. Repositioning the
mapping layer later does not automatically reposition placed scenes.

`tests/terrain_library_structure_probe.gd` checks both projections, transformed
anchors, pivots, sorting, footprint rejection, preservation and PackedScene
roundtrips using diagnostic scenes, not production artwork.

Select the structure layer in the Inspector, choose Edit Structures, a catalog
entry, unique placement ID and X/Y anchor, then Stage. Pending additions are saved
under `StructureWorkingCopy`. Apply moves the same instances into the live layer
as one editor undo action; Undo restores the pending instances. Discard removes
only the contents of this dedicated working container, not existing live structures.
Do not place unrelated nodes in that container. Discard after Undo intentionally
invalidates any redo that references the discarded instances; redo reports a conflict.

Apply checks names, pack/layout metadata, native anchors and footprint bounds
before moving a batch. Conflicts preserve both copies. Pending instances retain
authored scene behavior: this is an editor staging workflow, not a runtime physics
sandbox. Structure editing is separate from terrain-cell Apply; a combined
terrain/structure transaction remains pending.
Automated tests cover the underlying transactions, actual Inspector button handlers,
scene editor undo history and disk save/reopening. Pointer-driven visual review
remains pending; headless editor tests do not establish visual acceptance.

Run `tests/terrain_library_structure_probe.gd` first to generate disposable fixtures,
then run this opt-in editor harness:

```powershell
& 'H:/dev/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --path . --quit-after 18000 res://tests/terrain_structure_editor_probe.tscn -- --terrain-structure-editor-probe
```

Require `TERRAIN STRUCTURE EDITOR: PASS`, not merely exit code zero. The harness
uses unique test-output copies, invokes Edit/Stage/Apply/Discard, checks scene
Undo/Redo, and reopens both pending and published disk saves in both projections.
It does not run just by opening the scene without its explicit command-line flag.
The verified run had only environment root-certificate/editor-settings warnings,
with no structure or script errors. Test fixtures have explicitly named authored
scene roots so disk serialization does not produce empty-name instancing errors.

### Moving existing instances

In an active structure edit, enter an existing placement ID and destination X/Y,
then choose Stage Move and Apply. Moves are staged as saved placement data; the
live visual stays in place until Apply. Only one move is staged at a time, and
additions must be applied or discarded first. No duplicate scripted or collidable
preview instance is created.

The Existing Instance selector lists live structure IDs and fills in the selected
instance's ID and current anchor. Change X/Y before Stage Move. Both instance and
catalog selectors refresh after scene changes while retaining the selected item
when it still exists. Catalog staging lists only entries with a scene and layout;
the status area reports missing pack/mapping setup. Full validation still runs at
staging and Apply. Instance selection is covered by the headless Inspector harness.

The editor draws cyan destination cell outlines, an origin marker and a direction
arrow while a move is pending. Cell polygons use the mapping TileSet's square or
diamond shape and native centers, transformed into the structure layer. Apply and
Discard clear the overlay; Undo restores it. It creates no saved preview nodes and
does not draw in runtime games. Footprints above 1,024 cells omit the viewport hint
to bound geometry allocation; placement validation remains separate. Geometric
and Inspector checks do not substitute for rendered viewport visual acceptance.

Apply changes the original instance's transform and anchor only. Children and
gameplay metadata remain intact. Undo restores the original placement and pending
move; Discard clears the pending move without touching live art. Changes to the
live placement, bounds or grid transform cause conflict rejection. Pending moves
participate in world guards and survive disk save/reopen. Tests cover both
projections and the actual Inspector move action with scene Undo/Redo.

### World registration

Add each structure layer's path to `TerrainWorldComponent.StructureLayerPaths`.
Paths are relative to that world node. Registration is explicit: it does not scan
other maps or force dependencies onto Painted/block-Isometric worlds. Do not
register one structure layer with multiple worlds.

Registered pending sessions (including working children with a cleared active
flag) participate in the world's existing redraw, restore, generation and
projection-switch guards. Apply or Discard before switching. Beginning an edit
or applying history while the owning world generates or displays another
projection is rejected without moving instances. Return to the original view
before using its structure history.

At readiness, drawing and accepted projection changes, registered layers are
visible only when their pack matches Tiles or IsometricAutotile. They are hidden
in Painted and block Isometric; their resources and instances stay intact.
Call `RefreshStructureVisibility()` after changing registration or pack assignments
at runtime. Unregistered standalone layers retain caller-managed visibility and
are not protected by a world's pending-edit guard.
