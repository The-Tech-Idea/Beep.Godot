# Phase 2 — Leftover-child & material leaks

## Why

The dominant *new* pattern this round: a component builds a UI node and `AddChild`s it onto its
**parent** (so the visual lives in the parent's subtree), then never frees it in `_ExitTree`. Remove
the component while the parent survives and the built node is orphaned onscreen. `ChipComponent` (the
crash) is Phase 1; the rest here are orphans (visual litter, not crashes) plus two "material stuck on
parent" cases. `BossHealthBarComponent` already does this right (frees its `_vbox`) — make the rest
match it.

## The work

### Orphaned child nodes — free them in `_ExitTree`
| Component | Site | Orphan |
|---|---|---|
| `BadgeComponent` | `ecs/ui/BadgeComponent.cs:89-92` | `_badgePanel` (added to parent; tween killed but panel leaks) |
| `ComboCounterComponent` | `ecs/ui/ComboCounterComponent.cs:57-62` | `_label` "ComboLabel" (tween killed, label leaks) |
| `BuffBarComponent` | `ecs/ui/BuffBarComponent.cs:107-118` | `_container` HBox |
| `InteractionPromptComponent` | `ecs/ui/InteractionPromptComponent.cs:83-86` | created `_label` |
| `MatchTimerComponent` | `ecs/ui/MatchTimerComponent.cs` (`EnsureLabel`) | injected `TimerLabel` |
| `DialogUIComponent` | (low priority) | `_panel` on independent removal |
| `InventoryComponent.Display` | already fixed round 1 — pattern reference | — |

Fix each: in `_ExitTree`, `if (_x != null && GodotObject.IsInstanceValid(_x)) _x.QueueFree();` then
`base._ExitTree();`. (Round 1 did exactly this for `InventoryComponent` — copy that shape.)

### Material stuck on the parent — restore on exit
| Component | Site | Fix |
|---|---|---|
| `SkeletonLoaderComponent` | `ecs/ui/SkeletonLoaderComponent.cs:59` | `Stop()` restores `_priorMaterial` but there's **no `_ExitTree`** — a loader `QueueFree`'d on the normal path (data arrives → skeleton removed, Control reused) leaves the shimmer `ShaderMaterial` stuck on the parent. This is the **exact twin** of round-1's `VignetteComponent` fix, not applied here. Add `_ExitTree` restoring `_priorMaterial` (guarded) + `base._ExitTree()`. |
| `ChromaticAberrationComponent` | `ecs/ui/ChromaticAberrationComponent.cs:55-66` | `Apply()` sets `ci.Material` on the parent, no `_ExitTree` restore → shader stuck on parent after removal. Also `Apply()` runs in-editor (no `IsEditorHint` guard) and rebuilds a fresh `Shader` each call. Restore material on exit + add the editor guard. |
| `ToastNotificationComponent` | `ecs/ui/ToastNotificationComponent.cs:89` | per-toast tween `Finished` lambda captures `this`; no `_ExitTree` kills in-flight toast tweens → `EmitSignal` on a freed component if the parent outlives it. Track pending toast tweens + `Kill()` on exit. |

## Gotchas

- Free the child **only if this component created it** — some components adopt a pre-existing node
  (check how `_label`/`_panel` was obtained; if `GetNodeOrNull` found an authored node, don't free it,
  only free the `new`-and-`AddChild`'d case). Badge/Combo/BuffBar/MatchTimer all `new` theirs, so free.
- Material restore must cache the **prior** material (may be non-null) and only restore if this
  component replaced it — mirror `VignetteComponent`'s guarded save/restore exactly.
- `SkeletonLoader`'s reuse path (loader removed once real content loads) makes its leak *likely* in
  practice, not theoretical — prioritize it within this phase.

## Verify

1. Build + validator.
2. Editor: drop each listed component under a Control, then free the component (not the parent) →
   its built node is gone, no orphan left onscreen.
3. `SkeletonLoader`: let it finish/remove → the parent Control has no leftover shimmer material.
4. `ChromaticAberration`: open the scene in-editor → no shader running at edit time; remove it at
   runtime → parent's material cleared.
