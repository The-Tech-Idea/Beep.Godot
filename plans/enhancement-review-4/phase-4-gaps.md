# Phase 4 — Gaps: dead API surfaces & missing wiring

Not crashes — features the code *claims* to have but doesn't wire, so they silently do nothing. This is
the "never fail silently" class applied to the utility layer. Plus the one round-3 `base._ExitTree()`
straggler the regression check found.

## Fixes

### 1. `BeepTreeView.cs` — selection events die for POCO/value payloads (Decision-free, wire it)
- **`:42,50`** — `ItemSelected`/`ItemActivated` read `GetMetadata(0).AsGodotObject()` → **null** for
  POCO / int / string payloads, but `BuildTree`/`AddNode` store arbitrary `T` via `Variant.From`. So
  selection events **silently never fire** for value/POCO data (the common case). Read the raw `Variant`
  and hand it back (or `.As<>()` at the call site) instead of forcing `AsGodotObject()`.

### 2. `BeepDropdown.cs` — "filter-as-you-type" with no search box (Decision A)
- **`:14-47`** — documented as a "searchable dropdown, filter-as-you-type" but **no search `LineEdit` is
  ever created or added** (`_searchBox`/`_popupContent` declared and unused); the public `Filter()` is
  unreachable from the UI. **Decision A: wire a `LineEdit` into the popup** (recommended over dropping the
  claim) — create it in the popup content, connect `TextChanged → Filter`.

### 3. `BeepDataBinder.cs` — two-way binding never writes back
- **`:75-83,120-127`** — `BindingMode.TwoWay` never propagates target → source: `RefreshTwoWay` is never
  called, `RefreshAll` only does source → target `Refresh()`. `BindCheckBox` **defaults to TwoWay**, so
  editing a bound checkbox never writes back. `OneWayToSource` refreshes the wrong direction too. Wire the
  target's change signal to `RefreshTwoWay`, and special-case `OneWayToSource`.
  *(This is the standalone `core/BeepDataBinder`, distinct from the `ecs/ui` `DataBinderHostComponent`
  fixed in round 2.)*

### 4. `BeepEncryptionPathfinding.cs` — silent save failure
- **`:41-47`** — `SaveEncrypted` is `void`; `f?.StoreString` is a **silent no-op when `Open` returns
  null**. Return `bool` and `PushWarning` on failure (the same rule that caught the corrupt-save-as-success
  bug in round 1).

### 5. `SceneTransitionComponent.cs` — round-3 `base._ExitTree()` straggler
- **`:87-90`** — overrides `_ExitTree` (kills `_tween`) but does **not** call `base._ExitTree()`, so
  `EntityComponent`'s group cleanup is skipped. The only file the round-3 base-call sweep missed. Add it.

### 6. Polish tail
- **`BeepStateMachine.cs:37-51`** — `Start`/`Transition` silent no-op on an unknown state → `PushWarning`.
  **`:93-99`** — `EventBus.Emit` iterates the listener list directly; a callback that sub/unsubscribes
  during dispatch throws. Snapshot with `ToArray()` before iterating.
- **`BeepEncryptionPathfinding.cs:18,31`** — `Rfc2898DeriveBytes` is `IDisposable`, never disposed →
  wrap in `using`. **`:114-125`** — A* diagonal cost 1 + a Manhattan heuristic is non-admissible and cuts
  obstacle corners → switch to octile (or drop diagonals).

## Verify
- `dotnet build` → 0 errors; `validate_scenes.sh` → PASS.
- Editor: bind a checkbox two-way and toggle it → the source object updates; a `BeepTreeView` over an
  `int[]` fires `ItemSelected`; `BeepDropdown` shows a search field that filters.
