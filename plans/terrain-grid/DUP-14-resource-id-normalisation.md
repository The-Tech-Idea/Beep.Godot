# DUP-14 — Resource-id normalisation drift: one canonical key across wallet, storage and cost totals

**Type:** duplication + fix · **Area:** `GridResourceAmount`, `GridStorageComponent` (+ `.Reservations`), `GridHaulerComponent`, `GridExtractorComponent`, `GridIds` · **Status:** **IMPLEMENTED 2026-09-11** · **Effort:** M (1–1½ days) · **Risk:** medium

## Outcome (2026-09-11)

`GridResourceAmount.TryTotals` now keys by `GridIds.Normalize` into a default-ordinal dict; `GridStorageComponent` (`_stored`, `Load`/`Unload`/`Stored`/`CanAccept`/`AcceptsResourceType`/`RestoreState`) and `GridStorageComponent.Reservations` (`_reservedMaterials`/`Reserved`) key on it too, with the redundant `OrdinalIgnoreCase` comparers dropped. **The wallet needed no edit** — once `TryTotals` is canonical, `Spend`'s commit loop keys `_amounts` the same way `GetAmount`/`SetAmount` always did, which is precisely the phantom-key bug closed.

**Scope grew during implementation.** A consumer-map sweep of every `TryTotals` caller and resource-id-keyed store found the fix incomplete as first scoped: the two parallel **cargo ports** — `GridHaulerComponent` (`CanAccept`/`AcceptsResourceType`/`Stored`/`Load`/`Unload`/`RestoreState`) and `GridExtractorComponent`'s buffer (`CanAccept`/`Load`/`Unload`/`Stored`/`RestoreState`) — still compared ids with `Trim()`/`OrdinalIgnoreCase`/raw `==`, so a hauler with a canonically-authored `AllowedResourceIds` would mismatch a spaced haul id that storage accepted. Leaving them would be rule 3's "managed duplication," so both were moved onto `GridIds.Normalize` in the same change (their cargo-id fields normalise on set, so old saves migrate on load). The HUD bar, production, resource node, subsurface store and city economy were verified already-canonical or not resource-id-keyed, so no further sites.

Guarded by `tests/ResourceWalletKeySmoke.cs` (+ `resource_wallet_key_probe.gd`/`.ps1`, gate-registered), a standalone smoke — it drives the real components with `"Iron Ore"`/`"iron_ore"`/`"iron-ore"` and asserts one shared bucket, a real debit (`GetAmount == 90`, one `"iron_ore"` key), a clean save round-trip, and non-canonical accept lists on storage and the hauler. Mutation-proven on four branches: revert `TryTotals` → wallet `GetAmount == 100` (phantom key); storage `Load` raw → `CanProvide` false; hauler `AcceptsResourceType` `OrdinalIgnoreCase` → dash accept-list rejects a canonical query; extractor `Load` raw → `Stored` reads 0. Build clean (0 warnings); `grid_terrain_subsurface`, `grid_worker_build_effects` and `grid_resource_catalog_ports` probes green (no regression).

## Finding

Three components each normalise the same fact — the canonical resource id — a *different* way, and the seam where they meet silently loses a wallet debit for any spaced or dashed id. `GridIds` was written precisely to end this (`GridIds.cs:5-18` documents the 18 prior copies and names the wallet keying `"Iron Ore"` inconsistently as the exact bug it exists to prevent), but the cost-total helper and the two resource stores never adopted it.

### 1. Three independent normalisers of one id (the duplication / root)

- **`GridIds.Normalize`** (`GridIds.cs:23-24`) — `Trim().ToLowerInvariant().Replace(' ','_').Replace('-','_')`, the **underscored** canonical form. The wallet keys `_amounts` through it on every path: `GetAmount:56`, `SetAmount:60`, `AddAmount:79`, `RestoreState:164`, `LoadStartingResourceAmounts:184`.
- **`GridResourceAmount.TryTotals`** (`GridResourceAmount.cs:25-38`) — the one cost-total helper. Keys by `resourceId.Trim().ToLowerInvariant()` (`:31`) into an `OrdinalIgnoreCase` dict (`:27`): case-folded, **spaces and dashes kept**. Its output feeds both `GridResourceWalletComponent.Spend/CanAfford` and `GridStorageComponent.CanProvide/TryConsume`.
- **`GridStorageComponent`** — `_stored` is `OrdinalIgnoreCase` (`GridStorageComponent.cs:61`), keyed by raw `resourceId.Trim()` at `Load:142`, `Stored:174`, `Unload:158`; `_reservedMaterials` the same (`GridStorageComponent.Reservations.cs:11,14`). Case-folded, **spaces and dashes kept**.

Storage's key form happens to agree with `TryTotals` (both trim, case-fold, keep spaces/dashes), so `CanProvide`/`TryConsume` are internally consistent today. The wallet is the odd one out: it keys `_amounts` underscored via `GridIds.Normalize`, but consumes the *space-kept* `TryTotals` output. A single `TryTotals` key form can therefore align with only one of the two stores — which is what makes a clean fix impossible without moving all three onto one canonical form, and what produces the concrete bug below.

### 2. `Wallet.Spend` commits the debit to a phantom un-normalised key (the bug)

`GridResourceWalletComponent.Spend` (`:94-118`) with a cost whose id contains a space or dash, e.g. `"Iron Ore"`:

- `TryTotals` yields the key `"iron ore"` (space kept).
- **Pre-check** (`:97-105`): `GetAmount("iron ore")` re-normalises to `"iron_ore"` and reads the canonical balance (`:99`). A funded wallet passes.
- **Commit** (`:108-113`): `int remaining = GetAmount(id) - required;` reads the canonical `"iron_ore"` balance again, then writes `_amounts[id] = remaining` / `_amounts.Remove(id)` with `id == "iron ore"` — the **raw space-kept** key, never passed through `GridIds.Normalize`. Because `_amounts` is a plain case-sensitive `Dictionary<string,int>` (`:29`), `"iron ore"` and `"iron_ore"` are distinct entries: the debit lands on a new phantom `"iron ore"` key and the real `"iron_ore"` balance is untouched.
- `Spend` returns `true`. The debit silently no-ops; the read path (`GetAmount:56`) hides it by re-normalising, so the balance still reads full.

Consequences: infinite resources / free production — `GridProductionProcess.cs:72,123` treat `wallet.Spend(...) == true` as "paid", so a spaced input id yields free output. And the phantom key is durable: `CaptureState` (`:154-155`) serialises `"iron ore"=remaining`, then `RestoreState` (`:162-171`) normalises both `"iron ore"` and `"iron_ore"` to `"iron_ore"` and does `_amounts[id] = amount` — last-writer-wins, so the balance is corrupted (to whichever collides last) across save/load. The `ResourceChanged` emit (`:114-115`) also carries the raw spaced id with the *full* balance, misreporting to HUDs.

Spaced ids are real authored input, not a hypothetical: `GridResourceAmount.ResourceId` is a free `[Export] string`, `StartingResourceAmounts`/build costs are authored scene dictionaries, and `GridIds.cs:11-14` cites `"Iron Ore"` as an in-the-wild spelling.

## Design

One canonical form — `GridIds.Normalize` (underscored) — owned by `GridIds`, and every resource store routed through it. No compat shim, no second normaliser kept "for storage": rename the behaviour onto the one owner and let the compiler/tests sweep.

1. **`GridResourceAmount.TryTotals`** — replace `resourceId.Trim().ToLowerInvariant()` (`:31`) with `GridIds.Normalize(resourceId)`; skip empties. Its output is now the underscored canonical id that both consumers key on. The `OrdinalIgnoreCase` comparer (`:27`) becomes redundant with the lower-invariant canonicalisation — drop it to a default ordinal dict so there is exactly one case rule, not two.
2. **`GridStorageComponent`** — normalise the store's own keys so it agrees with the new `TryTotals`: `Load:142`, `Unload:158`, `Stored:174` (and `_reservedMaterials` via `Reserved:14`) key by `GridIds.Normalize(resourceId)` instead of `resourceId.Trim()`. With underscore/lower canonicalisation the `OrdinalIgnoreCase` comparers on `_stored` (`:61`) and `_reservedMaterials` (`Reservations.cs:11`) are redundant — reduce to default ordinal dicts. `CanAccept`/`AcceptsResourceType` compare against `AllowedResourceIds`, which are also author strings; normalise both sides there too so an allowed-list entry `"Iron Ore"` matches a stored `"iron_ore"`.
3. **`GridResourceWalletComponent.Spend`** — the commit loop (`:108-113`) and the emit loop (`:114-115`) now receive already-canonical ids from `TryTotals`, so `_amounts[id]` indexes the same key `GetAmount`/`SetAmount` wrote. No separate normalise call is needed once (1) lands; the fix is that the wallet no longer keys `_amounts` two different ways. (`CanAfford` and `CanProvide`/`TryConsume`/reservations inherit the fix through `TryTotals`.)

This is one-owner-per-fact restored: `GridIds.Normalize` is the sole id normaliser, `TryTotals` emits it, both stores key on it, and a resource can no longer read as two ids across wallet, storage and cost totals.

Note on save compatibility: storage/wallet `RestoreState` already re-normalises keys on load (`GridResourceWalletComponent.cs:164`; storage `RestoreState:228` currently keeps raw — change it to `GridIds.Normalize` in step 2), so an old save written with space-kept storage keys migrates to the canonical form on the next load rather than needing a migration pass.

## Guards (fail first)

Host in `tests/GridPlacementSmoke.cs` (the existing wallet/resource smoke, per ENH-15), plus a contract-scan pin.

- **Wallet debit probe (the bug):** `SetAmount("Iron Ore", 100)`, then `Spend([{ ResourceId="Iron Ore", Amount=10 }])`; assert the call returns `true` **and** `GetAmount("Iron Ore") == 90` **and** `CaptureState()` contains exactly one key (`"iron_ore"`). Mutation: revert the `TryTotals`/commit fix → the spaced key is written to a phantom `"iron ore"` entry, `GetAmount` still reads `100`, and `CaptureState` holds two keys → probe fails. (Confirm it can fail by running it against unfixed code first.)
- **Cross-store key-agreement probe:** into a `GridStorageComponent`, `Load("Iron Ore", 50)`, then assert `CanProvide([{ResourceId="iron_ore",Amount=50}]) == true` and `TryConsume` of the same dashed spelling `"iron-ore"` drains it to `Stored("iron_ore") == 0`. Mutation: leave storage keying raw `Trim()` while `TryTotals` normalises → `Available("iron_ore")` misses the `"iron ore"` bucket, `CanProvide` returns false → probe fails. This is the guard that proves step 2 is required alongside step 1.
- **Save round-trip probe:** `SetAmount("Iron Ore",100)`, `Spend` a spaced cost, `CaptureState` → `RestoreState`; assert the restored balance equals the post-spend balance (`90`), not a collided value. Mutation: restore the phantom-key write → the two keys collide on restore and the balance flips → probe fails.
- **Scan pin** (`tests/addon_contract_scan.ps1`): forbid `Trim().ToLowerInvariant()` as a resource-id key form in `GridResourceAmount.cs` and `GridStorageComponent*.cs` (the canonical keying must go through `GridIds.Normalize`). Mutation: restore either raw keying → scan fails.

## Dependencies / collisions

- **DUP-05** — GridIds as the one normaliser; this plan is a direct extension of it (closes the wallet/storage/totals holdouts DUP-05 did not reach). ENH-15's `ResourceCatalog.Find` already keys on `GridIds.Normalize`, so the catalog side is consistent with the canonical form this adopts.
- **DUP-13** (`TerrainKindCatalog`), **ENH-01** (typed `CellsChanged`), **ENH-02** (edit-kind classification), **ENH-12** (job-queue spatial index) — no overlap; different subsystems.
- No collision with the concurrent `TerrainWorldComponent`/streaming/archive session — this is entirely within `ecs/grid` resource components.

## Out of scope

- Economy/balance tuning and the `GridProductionProcess` recipe flow (only relied on here to show the bug's blast radius).
- `GridPorts.Transfer` remainder handling (FIX-12) and the transport/extraction registries (DUP-16) — separate resource-subsystem items.
- Introducing a strongly-typed resource-id value type; the canonical form stays a normalised `string`, matching the rest of the addon.
