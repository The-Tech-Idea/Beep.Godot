# GridObjectComponent

Common identity, inspection, and footprint-reservation component for anything placed on the grid — building, prop, machine, resource node, or unit. Attached under a placed `Node2D`, it exposes the object's grid cell, footprint, ids/category/description through ordinary Godot exports, and owns the machinery that reserves and releases that object's own cells against `GridPlacementComponent`'s occupancy set and `GridNavigationComponent`'s blocked set.

It is a `GameplayComponent` (→ `EntityComponent`), joins the `"grid_objects"` group in `_Ready`, and mirrors its identity fields onto its *parent* node's Godot metadata (`ApplyParentMetadata`) so generic scene tooling can read `grid_object_id`/`grid_object_cell`/etc. without knowing this component exists. The core design is that the object tracks exactly which cells *it itself* reserved in two private `HashSet<Vector2I>` sets (`_reservedPlacementCells`, `_reservedNavigationCells`), so `ReleaseFootprint()` — called automatically from `_ExitTree` by default — always releases precisely those cells regardless of what `Cell`/`Footprint`/`BlocksNavigation` happen to be at that moment; this is the mechanism `GridPlacementComponent` relies on (per its own comments) to stop a deleted placed building from leaking its occupancy/navigation marks on the grid forever. A second, explicitly-noted past bug: `BlocksNavigation` in `ReserveFootprint()` gates *only* the navigation-reservation half, not the whole method — gating the whole method used to mean a walkable object (`BlocksNavigation = false`) reserved no placement occupancy either, so anything could be built on top of it. Every mutator that can change `Cell`/`Footprint` (`Configure`, `SetCell`, `RestoreState`) follows the same release-if-reserved → mutate → re-reserve-if-was-reserved-or-`ReserveFootprintOnReady` sequence, so footprint bookkeeping stays correct across every path, not only the first placement.

## Public API

- `public const string ComponentGroupName = "grid_objects"`.
- `[Signal] GridObjectChangedEventHandler(string objectId, int x, int y)`.
- `[Export] public string ObjectId / DisplayName / ObjectKind / Category` / `[Export(PropertyHint.MultilineText)] public string Description`.
- `[Export] public Vector2I Cell` / `public Vector2I Footprint` / `public bool BlocksNavigation`.
- `[Export] public NodePath PlacementPath / NavigationPath`.
- `[Export] public bool ReserveFootprintOnReady / ReservePlacementFootprint / ReserveNavigationFootprint / ReleaseReservedFootprintOnExit`.
- `[Export] public bool Selectable` / `public bool Complete` / `public Godot.Collections.Dictionary Metadata`.
- `public string EffectiveCategory` — `Category` if set, else `ObjectKind`.
- `public override void _Ready()` — sets `ComponentGroup` if unset, joins `ComponentGroupName`, applies parent metadata, and (outside the editor) reserves its footprint if `ReserveFootprintOnReady`.
- `public override void _ExitTree()` — releases its footprint if `ReleaseReservedFootprintOnExit`.
- `public void Configure(string objectId, string displayName, string category, Vector2I cell, Vector2I footprint, bool blocksNavigation, bool complete = true)` — the main setup entry point used by `GridPlacementComponent` after instantiating a placed scene; normalizes `objectId`, defaults `ObjectKind` to `category` if unset, clamps `Footprint` to at least 1×1, and runs the release/mutate/re-reserve cycle.
- `public void SetCell(Vector2I cell)` — relocates the object, same release/mutate/re-reserve cycle.
- `public void ReserveFootprint()` / `public void ReleaseFootprint()` — reserve/release exactly this object's tracked footprint cells against the resolved `GridPlacementComponent`/`GridNavigationComponent`.
- `public void SetMetadataValue(string key, Variant value)` / `public Variant GetMetadataValue(string key)`.
- `public Godot.Collections.Dictionary CaptureState()` / `public void RestoreState(Godot.Collections.Dictionary state)` — full field snapshot/restore (including the reservation-policy bools), each defaulting missing keys to the object's current value; `RestoreState` also runs the release/mutate/re-reserve cycle.
- `public void ApplyParentMetadata()` — writes `grid_object_*` metadata keys onto `GetParent()`.

## Dependencies

- Resolves `GridPlacementComponent` (`SetOccupied`) and `GridNavigationComponent` (`SetBlocked`) by `NodePath` or scene-wide `EntityComponent.FindComponent`.
- Uses `GridVariantReader.Bool`/`.Vector2I` for `RestoreState`'s dictionary parsing.
- Called into by `GridPlacementComponent.ConfigurePlacedObject` (finds-or-creates this component on a newly placed node, sets its reservation-policy exports, and calls `Configure`) and by `GridWorldStateComponent` (finds every instance via the `grid_objects` group and calls `CaptureState`/`RestoreState`/`ReleaseFootprint`) — both confirmed by reading those two files in this same batch.

## Notes

- The `BlocksNavigation`-gates-only-navigation-half behavior is explicitly documented in the file's own comment as a fix for a real past bug (walkable objects failing to reserve placement occupancy), not a hypothetical.
- `NormalizeId` (lower-invariant, spaces → underscores) does not also replace dashes, unlike `GridTerrainRules.Normalize` (outside this batch, read for context), which replaces both spaces and dashes when normalizing terrain-kind strings — two independent id-normalizers in the grid subsystem, different domains (object id vs. terrain kind), with different rule sets.
- `HasReservedFootprint` is true if *either* reserved set is non-empty, so an object that reserved only placement cells (`BlocksNavigation = false`) still correctly triggers the release/re-reserve cycle on a later `Configure`/`SetCell`/`RestoreState` call.
