# GridStorageComponent

A stationary cargo hold: a tank, a silo, a warehouse, a pipeline buffer. It implements `IStorage` (both `ILoadPort` and `IUnloadPort`) and `ISaveable`, holds material, and does nothing else — which the doc comment calls out as the point: an extractor fills it, a hauler draws from it, a pipeline segment hands through it, all via `Load`/`Unload` and `GridTransportManagerComponent.Transfer` (or the equivalent `GridPorts.Transfer`), and none of them need to know it is specifically a tank. Attached under a placed building beside its `GridObjectComponent`.

Its contents are world state, not generated-map state, so each instance is given its own `SaveKey` and round-trips through saves via `ISaveable`. Multiple resources can share one storage's `Capacity` simultaneously (it's a `Dictionary<string,int>` keyed by resource id, not a single-resource buffer like `GridExtractorComponent`'s or `GridHaulerComponent`'s hold) — `CurrentLoad` is the sum across every resource id currently stored.

## Public API
- `int CurrentLoad { get; }` — total units across every resource currently held.
- `bool CanAccept(string resourceId)` — true if `AllowedResourceIds` and `AllowedResourceTags` are both empty (accepts anything); otherwise delegates to `AcceptsResourceType` (see below).
- `protected virtual bool AcceptsResourceType(string resourceId)` — the acceptance rule once at least one allow-list is configured: an exact `AllowedResourceIds` match wins outright; otherwise, when `Catalog` is wired and `AllowedResourceTags` is non-empty, accepts if `Catalog.Find(resourceId)` exists and carries any of `AllowedResourceTags` (via `ResourceDefinition.HasTag`). A `resourceId` the catalog does not recognize can never match a tag, which is what turns a typo'd id into a rejection instead of a silent accept. A subclass can override this hook to replace the acceptance rule entirely (category-based, a per-project scheme) without touching `Load`/`Unload`.
- `int Load(string resourceId, int amount)` — adds up to free space (`Capacity - CurrentLoad`); emits `StorageChanged`.
- `int Unload(string resourceId, int amount)` — removes up to what's held; removes the dictionary entry entirely once it reaches zero; emits `StorageChanged`.
- `int Stored(string resourceId)` — units of one resource held, 0 if none.
- `Godot.Collections.Array<string> StoredIds()` — every resource id currently held (non-zero amounts only, since zero entries are removed on `Unload`).
- `Godot.Collections.Dictionary CaptureState()` / `void RestoreState(Godot.Collections.Dictionary state)` — public snapshot/restore of the `_stored` dictionary under a `"contents"` key, usable independent of the `ISaveable` plumbing.
- `void ISaveable.Save(GameBuilder.GameStateData state)` / `void ISaveable.Load(GameBuilder.GameStateData state)` — explicit interface implementations that write/read `CaptureState()`/`RestoreState()` under `SaveKey`, no-op if `SaveKey` is blank.
- Exports: `ParticipatesInSave`, `SaveKey`, `Capacity`, `AllowedResourceIds`, `Catalog` (`ResourceCatalog?`, optional), `AllowedResourceTags`.
- Signal: `StorageChanged(string resourceId, int stored, int currentLoad)`.
- `_GetConfigurationWarnings()` — warns if `AllowedResourceTags` is set but `Catalog` is empty (tag filtering would have no effect, and every id not also exact-listed would be rejected).

## Dependencies
- Resolves nothing via `NodePath` — no `ResolveReferences()`, no external component wiring at all; it is a pure port + save target.
- Joins `SaveableHelper.Group` on `_Ready` (if `ParticipatesInSave` and not the editor) and leaves it on `_ExitTree` (outside this batch — `SaveableHelper`/`ISaveable` live under `ecs/`, not `ecs/grid/`).
- Its `Load`/`Unload`/`CanAccept`/`Stored`/`StoredIds` are the target of `GridPorts.Transfer` calls from `GridHaulerComponent.TryDeliverCargo` (this batch, via `DepotStoragePath`) and from any `GridTransportChainComponent` link that names a `GridStorageComponent` in its `Chain` — not confirmed as an actual scene wiring from this batch alone, but the shape matches exactly.

## Notes
- `Catalog`/`AllowedResourceTags` link this component to `ResourceCatalog`/`ResourceDefinition` (`ecs/terrain/`, see `docs/resource-system/DEVELOPER_GUIDE.md`) — added after the batch this file was originally generated from, so it consults the resource-type registry the way `GridExtractorComponent.Catalog` already did, closing the gap where storage had no relationship to resource types at all. `GridHaulerComponent` gained the identical shape at the same time.
- The explicit interface implementation for `ISaveable.Save`/`ISaveable.Load` carries a code comment explaining why: an implicit/public `Load(GameStateData state)` would create an ambiguous overload alongside the cargo `Load(string resourceId, int amount)` for Godot's name-based `Call` dispatch, which the whole duck-typed port system depends on. This is a real, deliberate divergence from `GridSubsurfaceStoreComponent` and `GridProspectingComponent` in this same batch, which both implement `ISaveable.Save`/`.Load` as ordinary public methods — because neither of those two exposes a same-named `Load(string, int)` port method to collide with, so the explicit-interface guard isn't needed there. Not a duplicate-looking inconsistency; each file's choice matches whether it actually has a colliding member.
- `AllowedResourceIds` trims *both* sides of the comparison (`allowed?.Trim()` and `resourceId.Trim()`) in `CanAccept`, whereas `GridHaulerComponent.CanAccept` in this batch only trims the `allowed` side and compares against the raw, untrimmed `resourceId` parameter — a minor asymmetry between the two `CanAccept` implementations, though in practice resource ids appear to be authored without incidental whitespace.
