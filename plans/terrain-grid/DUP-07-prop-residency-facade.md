# DUP-07 — One streaming façade for the three prop renderers

**Type:** duplication fix · **Area:** `TerrainFeatureRendererComponent.Streaming.cs`, `TerrainReliefRendererComponent.Streaming.cs`, `TerrainIsometricFeatureRendererComponent.Streaming.cs`, `TerrainPropResidency<T>` · **Status:** proposed 2026-09-08 · **Effort:** S (1 day) · **Risk:** low

## Finding

`TerrainPropResidency<T>` is the shared engine (resident chunk set, wanted set from the camera, per-frame merge, stamp sort). Each of the three prop renderers then wraps it in its own `*.Streaming.cs` partial with the same members:

- `BeginStreaming()` / `ResetStreaming()` / `UpdateStreaming(double delta)` — identical bodies modulo the stamp type.
- `_residency` field, `StreamingThresholdCells = 65536`, `PreloadChunks` export, `ResidencyChanged` signal.
- `Rebuild()` calling `ResetStreaming()` then `BeginStreaming()` (e.g. `TerrainFeatureRendererComponent.cs:186`), which is the line that makes every `CellsChanged` drop all resident prop chunks (see ENH-01).

Three copies of the façade is why the fix for "reset drops everything" (ENH-06) would have to be written three times.

## Design

Fold the façade into `TerrainPropResidency<T>` itself, or — once DUP-01 lands — into a `TerrainPropRendererComponent<TStamp> : TerrainRendererComponent` intermediate base that the three renderers derive from:

```csharp
public abstract partial class TerrainPropRendererComponent : TerrainRendererComponent
{
    [Export(PropertyHint.Range, "0,4,1")] public int PreloadChunks { get; set; } = 1;
    [Signal] public delegate void ResidencyChangedEventHandler(int residentCells, int residentChunks);
    protected TerrainPropResidency<Stamp> Residency { get; }
    protected abstract void BuildChunk(Vector2I chunk, List<Stamp> into);   // the one thing that differs
    protected abstract void PlaceStamp(in Stamp stamp);                        // Sprite2D vs Polygon2D vs iso block
    public sealed override void Rebuild() => Residency.Invalidate(changedChunks: null); // whole-map only when nothing narrower is known
}
```

Godot generic `partial` classes cannot be `[GlobalClass]`; the base is non-generic with a shared `Stamp` record (texture, region, position, scale, z, tint) — the three current stamp types already carry the same fields.

## Steps

1. Introduce the shared `Stamp` record; adapt the three renderers' stamp builders to emit it.
2. Move the façade members onto the base; delete the three `*.Streaming.cs` partials.
3. Keep `PreloadChunks`/`ResidencyChanged` names so `actors/streaming_lab.tscn` and probes are unchanged.

## Guards

- Pin: no `*.Streaming.cs` partial under `ecs/terrain/` other than the base; `BeginStreaming(` declared once. Mutation: re-add one → fails.
- `terrain_prop_residency_probe` (existing streaming probes): resident counts after a camera jump identical before/after the refactor for all three renderers.

## Dependencies / collisions

Depends on DUP-01 (base type). Prerequisite for ENH-06 (chunk-scoped prop invalidation lands once, on the base).

## Out of scope

Residency algorithm changes (ENH-06), art.
