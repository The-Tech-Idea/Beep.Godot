# FIX-12 — Ports transfer remainder: an unload-only giver silently loses the un-accepted cargo

**Type:** fix · **Area:** `GridPorts.Transfer`, `IUnloadPort`, `ILoadPort`, `GridTransportChainComponent`, `GridHaulerComponent` · **Status:** **IMPLEMENTED 2026-09-11** · **Effort:** XS (¼ day) · **Risk:** low

## Outcome (2026-09-11)

`GridPorts.Transfer` now reads `FreeSpace(to)` before unloading and caps the draw to it (`want = Mathf.Min(amount, room)`), returns whatever remainder the receiver declines, and reports a shortfall it could not return through `GD.PushWarning` rather than dropping it; the class doc now states that guarantee accurately instead of promising an unconditional remainder return. New probe `tests/grid_ports_transfer_probe.gd` (registered in `run_actor_checks.ps1`) drives both cases through the real consumer, `GridTransportChainComponent`: an `IUnloadPort`-shaped source holding 10 into a capacity-4 sink conserves 10 (giver 6 / sink 4), and a sink that under-takes its own reported room returns the remainder to a giver that can re-accept it (giver 8 / sink 2). Mutation-proven: the old body reports `unload-only giver lost cargo: giver 0 + sink 4 != 10`, the 6 units the plan predicted. Build clean (0 warnings); `grid_resource_catalog_ports_probe`, `storage_material_reservations_probe` and `terrain_haul_demand_probe` stay green — no regression in the shared hand-off.

The plan asked to extend `tests/grid_resource_catalog_ports_probe.gd`; a dedicated probe was used instead, because that file's subject is resource-catalog acceptance on storage and hauler, while the conservation guard needs a transport chain rather than the duck-typed acceptance ports. The `Transfer` return type was left as `int` (the plan's own preference): a warning on the unrecoverable path is enough, and neither `MoveLink` nor `TryDeliverCargo` changed.

## Finding

`GridPorts.Transfer` is *the* one safe hand-off, shared by the transport chain, the pipeline and the hauler's depot delivery. Its class doc states the invariant the whole logistics layer leans on: "unload from the giver, load into the receiver, remainder BACK to the giver — cargo is never duplicated and never lost" (`GridPorts.cs:6-10`). The remainder return is conditional on the giver having a `Load` method, so a legitimate giver-only port breaks the invariant with no signal.

The transfer body (`GridPorts.cs:58-75`):

```csharp
int given = from.Call("Unload", resourceId, amount).AsInt32();   // :67  material leaves the giver first
if (given <= 0)
    return 0;

int taken = to.Call("Load", resourceId, given).AsInt32();          // :71  receiver may take less than `given`
if (taken < given && from.HasMethod("Load"))                       // :72  remainder returned ONLY if giver can Load
    from.Call("Load", resourceId, given - taken);                  // :73  return value discarded
return taken;
```

Two ways the `given - taken` remainder is lost:

1. **Giver exposes no `Load` (a source-only port).** `IUnloadPort` is a first-class atomic connector — `Stored` / `StoredIds` / `Unload`, with **no `Load` member** (`IUnloadPort.cs:9-23`); `Load` lives only on the receiving `ILoadPort` (`ILoadPort.cs:34`). A spawner / well-head / source that answers only the unload shape is exactly what `AnswersUnloadPort` admits — it demands only `Unload` (`GridPorts.cs:24-26`). When such a giver hands off to a capacity-limited sink, `from.HasMethod("Load")` is false at `:72`, the `if` is skipped, and `given - taken` units are gone: never delivered, never returned. `Transfer` still returns `taken > 0`, so the caller reads a partial success and never learns cargo vanished.

2. **Giver's `Load` re-accepts less than offered.** Even when the giver has `Load`, the `from.Call("Load", …)` return value at `:73` is discarded. If the giver's hold is itself near capacity and takes back only part of `given - taken`, the difference is again dropped with no accounting.

Why it matters: a partial sink is the *common* case — capacity limits are the point of `ILoadPort.Load` returning "how much was actually accepted" (`ILoadPort.cs:31-34`). The logistics layer is advertised as duck-typed "connect anything to anything" (`ILoadPort.cs:5-14`), and a giver-only port is squarely inside that contract. So the one invariant the system's safety rests on is false as written, on a supported port shape, silently.

`Transfer` is the shared primitive: `GridTransportChainComponent.MoveLink` routes every chain link through it (`GridTransportChainComponent.cs:199-200`), and `GridHaulerComponent.TryDeliverCargo` uses it for depot delivery (`GridHaulerComponent.cs:533`). The chain is the realistic trigger — its giver is whatever duck-typed node the scene wires in, and `MoveLink`'s override hook invites custom source links. (The hauler passes `this` as the giver, and the hauler implements both `Load` and `Unload`, so that call site is safe today.)

**Latent, not live.** Every shipped port — `GridHaulerComponent`, `GridExtractorComponent`, `GridStorageComponent` — implements both `Load` and `Unload`, so no in-tree port is unload-only and branch 1 cannot fire against stock components right now. That is why this is medium severity, not high: the guarantee is false, but no shipped configuration reaches it. A game author wiring an `IUnloadPort`-only source (the interface exists precisely to be implemented) hits it immediately.

## Design

`Transfer` must not release material it cannot place or return. One owner stays: `GridPorts.Transfer` remains the single hand-off; the fix is inside it.

Primary change — **do not over-draw the giver.** Ask the receiver's free space *before* unloading and cap the draw to it, using the existing `GridPorts.FreeSpace(to)` helper (`GridPorts.cs:34-41`, already `Capacity - CurrentLoad`, `int.MaxValue` for a port that omits the two properties):

```csharp
int room = FreeSpace(to);
if (room <= 0)
    return 0;
int want = Mathf.Min(amount, room);

int given = from.Call("Unload", resourceId, want).AsInt32();
if (given <= 0)
    return 0;

int taken = to.Call("Load", resourceId, given).AsInt32();
if (taken < given)
{
    // The receiver took less than it advertised room for. Return the
    // remainder to the giver if it can re-accept; a giver that cannot
    // (unload-only) means the hand-off could not honour "never lost" —
    // report the shortfall rather than swallow it.
    int returned = from.HasMethod("Load")
        ? from.Call("Load", resourceId, given - taken).AsInt32()
        : 0;
    int lost = (given - taken) - returned;
    if (lost > 0)
        GD.PushWarning($"GridPorts.Transfer: {lost} '{resourceId}' could not be delivered or returned to {from.Name}; giver is unload-only or full.");
}
return taken;
```

Capping the draw to `FreeSpace(to)` closes the common case: a capacity-limited sink now advertises its room, so an unload-only giver only ever releases what the sink will take, and nothing is lost. The remainder branch survives as the safety net for a receiver that accepts less than its own reported free space (type filters, per-id caps inside `Load`); that residue still returns to any giver with `Load`, and the one path that genuinely cannot honour the invariant — an unload-only giver *and* a receiver that under-takes its own free space — is now surfaced through the failure handler instead of dropped. `given - taken` becoming zero in the common case makes the warning unreachable in normal operation, which is the point: it fires only where the invariant would otherwise have been broken.

This keeps `Transfer`'s `int` return (how much moved) intact, so `MoveLink` and `TryDeliverCargo` are unchanged — no signature ripple. No compat shim; the behaviour change is internal to the one method.

Correct the class doc (`GridPorts.cs:5-11`) to state the guarantee accurately: the giver is not over-drawn, remainder returns when the giver can re-accept, and an unrecoverable shortfall is reported — not that a remainder is unconditionally returned.

## Guards (fail first)

Extend `tests/grid_resource_catalog_ports_probe.gd` (it already constructs duck-typed ports and drives `Load`/`CanAccept` end to end). Add a conservation guard driven through a real consumer so the internal `GridPorts.Transfer` is exercised as it ships:

- **Wire a source-only giver into a `GridTransportChainComponent`.** Build a GDScript node exposing only `Unload`, `Stored`, `StoredIds` (an `IUnloadPort`-shaped source with a fixed hold of, say, 10 `"iron"`, no `Load`) and a receiver with `Capacity`/`CurrentLoad`/`CanAccept`/`Load` whose free space is 4. Tick the chain once so `MoveLink` → `GridPorts.Transfer` runs.
  - **Assert:** `giverHeld + receiverHeld == 10` after the tick (nothing lost), the receiver holds 4, and the giver still holds 6.
  - **Mutation that trips it:** revert to the current body (`Unload` the full `amount` first, return remainder only `if (from.HasMethod("Load"))`). The source has no `Load`, so 6 units evaporate: `giverHeld + receiverHeld == 4`, and the assertion fails. Proven able to fail.

- **Direct partial-sink conservation (belt-and-braces), through the chain the same way:** a giver *with* `Load` whose hold is full, handing to a sink that under-takes → assert the returned units land back in the giver and the total is conserved. Mutation: discard the `from.Call("Load", …)` return and assume full re-acceptance → total drops → fails.

Both assertions are conservation equalities, so a silent drop always breaks them; neither can pass against the unfixed code.

## Dependencies / collisions

- **DUP-14** (resource-id normalisation) touches `GridResourceWalletComponent` / `GridStorageComponent` / `GridResourceAmount`, not `GridPorts`; independent, no overlap. If both land, the normaliser does not change `Transfer`'s control flow.
- **ENH-01** (typed `CellsChanged` + affected-chunks payload) and **ENH-02** (edit-kind classification) are terrain-invalidation contracts — unrelated subsystem.
- **DUP-13** (`TerrainKindCatalog`) and **ENH-12** (job-queue spatial claim index) — unrelated.
- No collision with the concurrent `TerrainWorldComponent` / streaming / archive session: `GridPorts`, the transport chain and the hauler are the resource/logistics layer, not terrain streaming.

## Out of scope

- Changing `Transfer`'s return type to a struct/out-param that reports moved-vs-lost to every caller. The `int` return plus a warning on the unrecoverable path is sufficient for the invariant; a richer result is a separate enhancement with its own consumers.
- Adding a `Load` member to `IUnloadPort` (would erase the giving/receiving split the two atomic connectors deliberately draw).
- Backpressure / routing policy in `GridTransportChainComponent` and depot-selection in `GridHaulerComponent` — this fixes only the shared hand-off primitive they call.
- Any change to `FreeSpace`'s "port without Capacity/CurrentLoad is treated as open" rule (`GridPorts.cs:34-41`); an open receiver still takes everything, and an unload-only giver into an open receiver loses nothing.
