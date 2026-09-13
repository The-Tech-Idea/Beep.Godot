# FIX-03 — TerrainTextures.Load NRE: the external-file branch crashes instead of returning null

**Type:** fix · **Area:** `TerrainTextures.Load` (`ecs/terrain/TerrainTextures.cs`) · **Status:** **IMPLEMENTED 2026-09-11** · **Effort:** XS (¼ day) · **Risk:** low

## Outcome (2026-09-11)

`TerrainTextures.Load` now guards the null before the emptiness test in the external-file branch (`image is null || image.IsEmpty()`), so a missing, unreadable or undecodable file takes the existing warn-and-return-null path instead of throwing. New `tests/TerrainTexturesLoadSmoke.cs` (compiled through an explicit `Beep.Godot.csproj` entry) plus `tests/terrain_textures_load_probe.gd` assert both directions: a missing absolute path returns null without throwing, and a real on-disk PNG (the bundled `surf_foam_streaks.png`, globalized) still loads with a usable size. The smoke wraps the calls in a try/catch so the unguarded version reports a clean failure rather than aborting the probe script before `quit()`. Mutation-proven: restoring the bare `image.IsEmpty()` yields `Load threw for a missing external path: NullReferenceException` and exit 1.

Two things landed differently from the plan:

- The probe is registered in `run_actor_checks.ps1`, not `run_terrain_integration.ps1`: `Image.LoadFromFile` on a missing path makes Godot itself log `ERROR: Error opening file …`, and the terrain-integration runner fails any probe whose output matches `^ERROR:`. `run_actor_checks.ps1` judges by exit code, which is the signal this probe produces.
- The optional contract-scan pin was not added: the behavioural probe is mutation-proven, and the scan currently throws earlier in the file (see below).
- **A second copy of this defect was found and removed.** `MountainPrefabGeneratorComponent.LoadTexture` repeated the same external-file branch with the same unguarded `Image.LoadFromFile` / `Image.IsEmpty()`. It now delegates to `TerrainTextures.Load` (the one terrain-art loader), so the rule this fix established has one implementation rather than two — see FIX-04's Outcome.

**Pre-existing gate failure (not this change).** `tests/addon_contract_scan.ps1` throws at its `TerrainWorldComponent` pin — it requires `NewWorldOnReady` to contain `if (_restoredFromSave)\n return;`, but that method now guards with a compound condition (`BuiltSize.X > 0 || _restoredFromSave || saves?.HasPendingSaveRecord(SaveKey) == true`). `TerrainWorldComponent.cs` is unmodified in this session's working tree, so the pin is stale at HEAD; it blocks the scan before any pin covering this fix's files is reached. Correcting it belongs to the `TerrainWorldComponent`/streaming owner.

## Finding

`TerrainTextures.Load` is the addon's one terrain-art loader, written to fail safe: log a warning naming what failed and hand the caller a `null` it can branch on (`TerrainTextures.cs:34-43`). The `res://` branch honours that contract — `GD.Load<Texture2D>` returns null on failure, the code warns and returns the null (`:54-60`). The external-file branch does not.

1. **The null dereference.** For an absolute (non-`res://`) path the loader calls `Image.LoadFromFile(path)` and immediately tests `image.IsEmpty()` (`TerrainTextures.cs:63-64`):

   ```csharp
   Image image = Image.LoadFromFile(path);
   if (image.IsEmpty())
   ```

   Godot's `Image.LoadFromFile` returns a **null** `Image` reference when the file is missing, unreadable, or not a decodable image — it does not return an empty non-null `Image`. So on exactly the failure the method exists to report, `image.IsEmpty()` dereferences null and throws a `NullReferenceException` instead of taking the warn-and-return-null path two lines below. The intended failure output (`GD.PushWarning(...); return null;` at `:66-67`) is unreachable for a genuine load failure — it only runs for the case that cannot happen (a non-null-but-empty image).

2. **Why it reaches callers as a crash, not a degraded texture.** The class doc states the renderers are wired to art folders *outside* the project (absolute paths), and the `res://`-only `GD.Load` is precisely why this external branch exists (`TerrainTextures.cs:12-15`). A mistyped, moved, or removed external art path is therefore the realistic trigger, and it lands only on the branch that crashes. All seven call sites store the result as a nullable and branch on it — e.g. `TerrainFeatureSheets.cs:128`, `TerrainTileRendererComponent.cs:291`, `TerrainTransitionLayerComponent.cs:463`, `TerrainReliefRendererComponent.cs:268`, `TerrainIsometricRendererComponent.cs:1382`, `TerrainResourceRendererComponent.cs:302`, `SeededTerrainPropScatterComponent.cs:219` — so a `null` would degrade cleanly to "art absent + warning". Instead the NRE propagates up through the renderer's rebuild, taking down the whole terrain rebuild for one missing sheet rather than skipping the one slot.

This is the loader defeating its own reason to exist (rule 2: fail safe *and* log the reason; the two are not the same, and here the code does neither for a real failure).

## Design

Guard the null before the emptiness test in the external-file branch, and route it through the warning the branch already writes:

```csharp
Image image = Image.LoadFromFile(path);
if (image is null || image.IsEmpty())
{
    GD.PushWarning($"[{owner}] could not load {what} '{path}'.");
    return null;
}
```

This is the minimal fix that makes the non-`res://` branch honour the same null-with-warning contract the `res://` branch already keeps (`:55-56`). No new API, no new consumer — every existing caller already handles the `null` return this restores; the change only removes the crash between them and it. The `IsEmpty()` test is kept, not replaced: a decoded-but-empty image is still a load that produced nothing usable, and the warning text is identical for both, so callers cannot tell (and do not need to tell) the two apart.

One owner per fact stays intact — this is a single loader with one failure path; the fix makes both branches reach that one path rather than adding a second.

## Guards (fail first)

A headless `.gd` probe under `tests/` that exercises `TerrainTextures.Load` with an absolute path to a file that does not exist:

- **Assertion:** `TerrainTextures.Load("/no/such/file.png", "probe", "test sheet")` returns `null` and does not throw. Godot surfaces an unhandled `NullReferenceException` from a C# static as a script error / non-zero probe result, so the probe asserts both "no exception raised" and "return value is null".
- **Mutation that trips it:** revert the guard to `if (image.IsEmpty())`. `Image.LoadFromFile` returns null for the missing path, `image.IsEmpty()` throws, and the probe fails on the raised exception — proving the guard catches the exact defect. (A second confirming mutation: point the probe at a real on-disk PNG and assert a non-null `Texture2D` with a mip chain, so the guard also pins that the fix did not break the success path.)

The probe must run in a headless context that can call the static directly. This is not a determinism-sensitive terrain-generation change, so `tests/terrain_generation_baseline_probe.gd` is not the vehicle; a small dedicated probe plus, optionally, a `tests/addon_contract_scan.ps1` pin that forbids the bare `image.IsEmpty()` without a preceding `image is null` guard in this method.

## Dependencies / collisions

- Independent of the resource-id and streaming work (DUP-05, ENH-01, ENH-02, DUP-13, ENH-12) — this is a self-contained null-guard in a static loader.
- No collision with the concurrent `TerrainWorldComponent`/streaming/archive session: `TerrainTextures` is a leaf utility that session does not touch.
- Touches one method; safe to land in isolation.

## Out of scope

- The mip-chain generation and the `res://` branch behaviour — both already correct.
- The `Bind` overload (`:89-100`) — it consumes `Load`'s null correctly and needs no change.
- Any change to how renderers react to a missing sheet beyond receiving the `null` they already expect; per-renderer fallback art is a separate design question, not this fix.
- Rationalising the seven call sites or the loader's caching — not implicated by this defect.
