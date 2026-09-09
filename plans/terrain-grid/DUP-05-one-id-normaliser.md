# DUP-05 — One id normaliser and one node-name sanitiser

**Type:** duplication fix (rules already diverge) · **Area:** `ecs/grid/**`, `ecs/terrain/TerrainGeneratorComponent`, `TerrainTransitionLayerComponent`, `SeededTerrainPropScatterComponent`, `MountainTileMapLayerGeneratorComponent` · **Status:** **PARTIALLY IMPLEMENTED 2026-09-09** (id normaliser done; the node-name sanitiser deferred) · **Effort:** S (1 day) · **Risk:** low–medium (a normaliser change can alter which ids match)

## Outcome (id normaliser)

`GridIds.Normalize` is the one rule now - trim, lower-invariant, `' '` and `'-'` to `'_'`, empty stays empty - with `GridIds.NormalizeOr(value, fallback)` for the callers that want an empty id to mean a default. Fourteen private copies are gone: the nine that treated empty as empty forward to `GridIds.Normalize`, and the five that invented a fallback (`"work"`, `"survey"`, `"misc"`, and the two terrain `"grass"` defaults) now say that fallback at the call site through `NormalizeOr`. `GridTerrainRules.Normalize` forwards to `GridIds.Normalize` in one line so its existing callers and pins stand.

The bug the drift caused is fixed and guarded: the wallet keyed amounts with a normaliser that kept spaces, the build catalogue lower-cased but kept them, and the resource bar kept the author's case, so `"Iron Ore"` in a cost and `"iron_ore"` in the wallet were the same resource to the catalogue and two resources to the wallet. `tests/grid_ids_probe.gd` credits a wallet `iron_ore` and reads it back as `Iron Ore` and `iron-ore`, spends against a third spelling, and checks the debit landed on the one key - and the two mutations trip it (a private `Normalize` re-added anywhere trips the scan pin; dropping the space replacement in `GridIds.Normalize` fails the wallet match).

The plan's second fix landed too: `SeededTerrainPropScatterComponent` used a copy that turned an empty terrain kind into `"grass"`, so an unassigned cell grew grass props. It uses `GridIds.Normalize` now, so an empty kind stays empty and scatters nothing.

Verified: `dotnet build` clean, zero warnings; 16 probes across the resource, economy, job, production, scatter and building systems green - the normalisation change moved which spellings match, and nothing that relied on the old matching broke; the scan pin (no private `Normalize`/`NormalizeKind`/`NormalizeId` outside `GridIds`, and `GridTerrainRules` forwards) passes and both its mutations trip.

**Deferred: the node-name sanitiser.** The four `SafeName` copies stay for now. They diverge on case - `GridPanelComponent.SafeName` (the HUD base, and the two forwarders on it) preserves the author's case, while the scatter and worker-spawner copies lower-case - and consolidating them means choosing one case rule, deleting the `protected` base every HUD panel calls, and updating its subclass callers, none of which is the id-matching bug this change was about. No test or scene references a `SafeName`-generated node name by literal string (checked), so the case change is safe to make; it is simply a separate, self-contained step, and `GridIds` gains its `NodeName` member when it lands. DUP-12 (HUD panels) is where that consolidation naturally belongs, since it already reworks every panel.

## Finding

`GridTerrainRules.Normalize` exists and is documented as *the* rule ("also replaces spaces and dashes — the private normalizer this replaced had quietly forgotten that", quoted at `GridWorkerSpawnerComponent.cs:302-306`). Eighteen private copies remain, and they do not agree:

| Method | Files | Rule |
|---|---|---|
| `private static string Normalize(` | `GridBuildCatalogComponent:132` (Trim+lower **only**), `GridProductionProcess`, `GridResourceNodeComponent`, `GridResourceWalletComponent` (omits space→`_`), `ui/GridResourceBarComponent:433` (Trim only, no lower), `TerrainTransitionLayerComponent:496`, `SeededTerrainPropScatterComponent:239` (lower + space + dash, **defaults empty to `"grass"`**), `MountainTileMapLayerGeneratorComponent` | 8 copies, ≥4 distinct rules |
| `private static string NormalizeKind(` | `GridJobEffectComponent`, `GridJobQueueComponent`, `GridProspectingComponent`, `ui/GridMinimapComponent`, `TerrainGeneratorComponent:639` | 5 |
| `private static string NormalizeId(` | `GridObjectComponent` | 1 |
| `private static string SafeName(` | `GridResourceScatterComponent`, `GridWorkerSpawnerComponent:265`, `ui/GridBuildToolbarComponent:375`, `ui/GridResourceBarComponent:414` (the last two already forward to `GridPanelComponent.SafeName`) | 4 |

Two consequences already exist in the tree:

- `GridResourceWalletComponent` keys amounts with a normaliser that keeps spaces, `GridResourceAmount.TryTotals` lowercases without replacing them, and `GridBuildCatalogComponent.CostSummary` uses Trim+lower. `"Iron Ore"` in a cost and `"iron_ore"` in the wallet are the same resource to the catalog and two resources to the wallet.
- `SeededTerrainPropScatterComponent.Normalize` turns an empty kind into `"grass"` — an unassigned cell grows grass props.

## Design

`GridIds` (static, `ecs/grid/`), replacing both `GridTerrainRules.Normalize` and `GridPanelComponent.SafeName` as the owners:

```csharp
public static class GridIds
{
    /// Canonical id: trim, lower-invariant, ' ' and '-' → '_'. Empty stays empty.
    public static string Normalize(string? value);
    /// Canonical id or the fallback when the input is empty. Callers say the default; the normaliser never invents one.
    public static string NormalizeOr(string? value, string fallback);
    /// Godot node-name safe: Normalize plus '/', '\\', ':' and invalid-filename chars → '_'.
    public static string NodeName(string? value, string fallback);
    public static bool Equal(string? a, string? b) => Normalize(a) == Normalize(b);
}
```

`GridTerrainRules.Normalize` forwards to `GridIds.Normalize` (one line) so the existing scan pins and callers stay valid. Every private copy is deleted; the compiler finds the call sites. `SeededTerrainPropScatterComponent` uses `GridIds.Normalize` and treats empty as "no palette" (which `PaletteKeyFor` already does for unknown kinds).

Terrain-side callers (`TerrainGeneratorComponent.NormalizeKind`, `TerrainTransitionLayerComponent.Normalize`) reference `GridIds` from `ecs/terrain/` — acceptable; the terrain engine already depends on `GridCellDataComponent`.

## Guards

- Pin: `private static string Normalize(`, `NormalizeKind(`, `NormalizeId(`, `SafeName(` appear in no file under `ecs/grid/` or `ecs/terrain/` except `GridIds.cs`. Mutation: restore one → fails.
- Smoke (`GridPlacementSmoke`): add `"Iron Ore"` to a build's `Costs`, fund the wallet with `iron_ore`, assert `CanAfford` is true and `TrySpend` debits the wallet. **Mutation:** reinstate the wallet's private `Normalize` → smoke fails.
- Smoke: scatter with a cell whose terrain kind is `""` places no prop. Mutation: restore the `"grass"` default → fails.

## Dependencies / collisions

`ecs/grid/` is being edited by another session — coordinate before the sweep; the change is mechanical and compiler-driven, so it can be staged per file. Independent of everything else in this set; DUP-12 (HUD panels) assumes it.

## Out of scope

Enum-name parsing (`TryParseMode`/`TryParseAction`, see DUP-12), display names, localisation.
