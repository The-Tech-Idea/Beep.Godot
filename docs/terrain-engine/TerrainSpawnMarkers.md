# TerrainSpawnMarkers

Support utility: a stateless `internal static class` that writes and reads a map's player starts as ordinary scene nodes (FEAT-12). Not a stage or a component.

The convention is a `Spawns` node under the map root holding one `Marker2D` per start, named `Start_<k>` and standing on that start's headquarters anchor. That is what lets a generated map be saved as a `.tscn` and opened as a plain Godot scene without losing its starts: the per-cell reservation (`terrain_start_area`) and the start order the data layers publish both need the generator, or the Beep gameplay baseline, to exist — a marker needs neither, which is exactly the native-map profile [the TileMapLayer output contract](../game-builder/TILEMAP_OUTPUT.md#spawns-implemented-feat-09feat-12) requires. A designer can drag the same nodes into an authored map, and `GridStartAreaComponent` reads them the same way. The names and metadata keys live here rather than being spelled out again at each end.

## Public API

- `const string RootName = "Spawns"` — conventional name of the node holding the markers, under the map root.
- `const string NamePrefix = "Start_"` — marker name prefix; the suffix is the start index, so `Start_0` is start 0.
- `const string IndexMeta = "start_index"` — which start a marker is: its index in the generator's start order.
- `const string FootprintMeta = "hq_footprint"` — the headquarters footprint the start was validated for, as a `Vector2I`.
- `const string UnusableMeta = "unusable"` — set on a start whose area the generator reported as unplayable, and absent otherwise.
- `static int Emit(Node2D spawnsRoot, IReadOnlyList<TerrainStartAreaReport> starts, GridProjectionComponent grid, Vector2I boundsOrigin)` — `Clear`s the previous emission, writes one marker per start, and returns how many were written. Report cells are generator-local, as `TerrainStartAreaReport` carries them, so `boundsOrigin` is added to make them the absolute cells the grid draws. Each marker gets `IndexMeta` and `FootprintMeta`, plus `UnusableMeta` only when the report is not `Usable`. A start whose cell has no finite position on the bound grid — no geometry bound — pushes a warning and is skipped, rather than writing a marker at a NaN position.
- `static void Clear(Node spawnsRoot)` — removes and frees every `Marker2D` child whose name starts with `Start_`, leaving anything else under the node alone.
- `static Marker2D? Find(Node spawnsRoot, int index)` — the marker for start `index`, or null. Matched on `start_index` metadata, not the node name: the name is the convention a human reads, the metadata is what survives a rename in an authored scene.

## Dependencies

- Reads `TerrainStartAreaReport` (`Index`, `Origin`, `Footprint`, `Usable`) and calls `GridProjectionComponent.CellToWorld`; nothing else.
- Called by `TerrainWorldComponent.Drawing.cs`'s private `EmitSpawnMarkers`, once per `Draw` and only when `TerrainWorldComponent.SpawnsPath` is set. BGB-13's native publisher is to call the same helper rather than a second emitter.
- `GridStartAreaComponent.OriginOf` calls `Find` through its private `MarkerOrigin` when `SpawnsRootPath` is set, and reads the marker's `GlobalPosition` back as a cell through its own `GridPath`.

## Notes

- Positions are written in the **map root's** space: `Emit` converts the grid's world point with `spawnsRoot.ToLocal`, so the marker's stored `Position` is an offset from the `Spawns` node while its global position is the anchor cell's. The root is what gets packed and instanced, and a marker measured in some intermediate node's space moves when that node does.
- Three methods, three callers: a `Count` helper was written and then dropped because nothing called it — the probe counts the node's children directly.
- Emission is a rewrite, not a merge — `Emit` calls `Clear` first — so a rebuild with fewer starts than the last one leaves no orphan markers.
- `faction_id` and `locked` are not written, though the FEAT-12 design listed them: a faction assignment is FEAT-10's and neither has an engine consumer yet. `unusable` took their place because an unplayable start is a fact a native map has no generator report to carry. An authored marker can still hold whatever extra metadata a game wants; `Clear` and `Find` only care about the name prefix and `start_index`.
- `tests/terrain_spawn_markers_probe.gd` (headless, registered in `tests/run_terrain_integration.ps1`, marker `[terrain-spawn-markers] OK`) puts the `Spawns` node at a deliberate offset from the map root, so a marker written in the wrong space cannot land on the right cell by accident. Emitting in global instead of map-root space is one of the mutations the probe was proven against; it read the markers back ten cells out.
