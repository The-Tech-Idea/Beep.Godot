# UI widgets — 40 components

The drop-in widget library.

> **Read this first: INERT-BY-DESIGN is not a defect here — it is the product.**
> ~30 of these have 0 scenes and 0 callers *by design*. A widget library ships widgets a developer
> drops onto a parent; "nothing in our templates uses it" is the expected state, not a finding.
> **Do not delete a widget for being unused.** The real findings below are the ones with bugs —
> and several are the kind that look fine in review and fail on first use.

**Verified across the set:** every widget uses a **safe guarded cast** — `AccordionComponent.cs:31`
`GetParent() as Container`, `BadgeComponent.cs:28` `as Control`, `CounterComponent.cs:32` `as
Label`, `ToggleSwitchComponent.cs:30` `as CheckBox`, `TableComponent.cs:43` `as VBoxContainer`,
`VignetteComponent.cs:56` `is not CanvasItem → return`. None hard-casts. **But most return
silently on mismatch** — they should warn (`CLAUDE.md` § *Never fail silently*).

---

## Keep as-is — no known defects

`AccordionComponent`, `CarouselComponent`, `ChipComponent`, `ChromaticAberrationComponent`,
`ComboCounterComponent`, `ContextMenuComponent`, `CoroutineHostComponent`, `CrosshairComponent`,
`DataBinderHostComponent`, `FlipCardComponent`, `KeybindManagerComponent`, `MatchTimerComponent`,
`ModalComponent`, `ProgressRingComponent`, `RippleComponent`, `SafeAreaComponent`,
`SkeletonLoaderComponent`, `StepperComponent`, `UIEffectComponent`, `VignetteComponent`.

Notes worth keeping:
- **`RippleComponent`** is ALIVE — instantiated by `MenuComponent.cs:70-73`. Chain caveat: its only
  caller is itself in 0 scenes.
- **`ProgressRingComponent`** is ALIVE — instantiated by `BuffBarComponent.cs:82-89`.
- **`MinimapComponent`** reads group `"minimap_blips"` (`:59`) — **no producers exist.** Keep, but
  the group needs a producer or the widget can only ever draw an empty map.
- **`DataBinderHostComponent`** overlaps `GameInfoBinder`/`HudComponent` conceptually; neither uses
  it. Not redundant — just never adopted.

**Fix (S, whole set):** add a `PushWarning` to each parent-type early-return. A widget that
silently does nothing on the wrong parent is indistinguishable from a broken widget.

---

## BROKEN — will not work as written

### `TableComponent` — zebra striping and hover can never render
**Evidence:** `:121-122` builds a row `panel` that is **never added** to `row`. So
`UpdateRowBg` (`:153-159`) finds no `Panel` and the striping/hover it implements **never renders**.
`AddRow` (`:98`) / `SetData` (`:104`) have 0 callers, so nobody has seen it.

**Fix (S):** `row.AddChild(panel)`.

### `TooltipComponent` — shows itself on load, without hovering
**Evidence:** `:30-35` hooks parent `MouseEntered`/`MouseExited` — correct. But **`_Process`
(`:43-48`) shows the tooltip whenever `!_showing`**, regardless of hover: `_hoverTime` is only
reset on enter, so it fires on load.

**Fix (S):** gate `_Process` on `_hovering`.

### `RatingComponent` — hover lights the wrong star
**Evidence:** `:56` — the closure captures the **loop variable `i`**, not `idx`. Classic C#
capture bug.

**Fix (S):** capture into a local.

### `SearchBarComponent` — a repeater, not a debouncer
**Evidence:** `:79-90` re-emits `SearchChanged` **every 0.3s forever** while text is non-empty.
Named and documented as a debounce.

**Fix (S):** emit once on settle; reset the timer on keystroke.

### `ToastNotificationComponent` — stale static across scene changes
**Evidence:** `:24,:29` — a last-instance-wins `static _instance`, **never cleared on
`_ExitTree`** → a stale reference after a scene change. `Show()` (`:32`) 0 callers.

**Fix (S):** clear the static in `_ExitTree`; null-guard `Show`.

### `BossHealthBarComponent` — anchors screen-space under a Node2D; scrolls off-camera
**Evidence:** `:47` `_vbox.SetAnchorsPreset(TopWide)` — a **screen-space** anchor. But `:59`
`GetSiblingComponent<HealthComponent>()` means the parent is the **boss body (`Node2D`)**, so the
`VBoxContainer` joins the **world** canvas, anchors against an empty rect, and rides the
`Camera2D`. **This is the exact trap `GenreScreenComponent.cs:126-133` documents at length.**

**Second defect:** `SlideDuration` (`:17`) is declared and **never referenced** — the doc's
"slides in at the top, slides out on death" (`:8-9`) **does not exist**; `:65` is a bare
`_bar.Visible = true`. There is no death handler at all.

**Third:** `HealthBarComponent.cs:32` guards `Engine.IsEditorHint()` in `_Ready` **before** the
`CallDeferred`, with a comment explaining why; `BossHealthBarComponent.cs:30` defers first and only
guards inside `Setup` (`:35`). Same net effect, but it shows this file never got the same review.

**Not redundant with `HealthBarComponent`** — that one correctly stays in world space
(`:46 _bar.Position = BarOffset - Size/2f`). This is a legitimate HUD-vs-world split, executed
wrong.

**Fix (M):** rewrite **HUD-parented** with an exported boss `NodePath` — the
`HudComponent.cs:23 PlayerPath` / `WeatherHUDComponent.cs:29 WeatherSystemPath` pattern, both of
which are ALIVE and shipped. Or delete and let `HealthBarComponent` cover bosses.

### `DialogUIComponent` — builds its own UI over authored markup
**Evidence:** `dialog_template.tscn:3`. `GetParent() is not Control → return` (`:87`) is a **safe**
cast — *"parent-type-broken" overstated it.* The real defect: it **rebuilds the UI** over the
authored `DialogLayer/Dialog` markup (`topdown_main.tscn:58-77`), and
`DialogStarted → StartFromDialogComponent` (`:215`) has **0 connections**.

**Fix (M):** rewrite as a **pure binder** over the authored `.tscn`; add the subscription.

### `NavigationComponent` — double-fire waiting to happen
**Evidence:** **0 `.tscn` contain it.** Consumers (`BootComponent.cs:65`, `MenuComponent.cs:42,118`,
`SceneTransitionComponent`, `LoadingScreenComponent`) are themselves not in shipped scenes.
`GameInfo.cs:70` explicitly documents *"NavigationComponent does NOT…"*.

**Defect:** `:63` dedupes by testing `IsConnected(..., Callable(this, nameof(Dispatch)))` but
**connects a lambda at `:66`** → the check never matches → a `.tscn` `[connection]` **plus**
auto-wire = **double-fire**.

**Fix (S):** connect the named method, or track the connection. **A false friend** — it is a
scene-transition button auto-wirer, **not pathfinding**. Putting it on a unit silently hijacks
Buttons in the parent tree.

### `ScreenFlashComponent` — leaks a CanvasLayer across scenes
**Evidence:** `Flash()` (`:47`) / `FlashWith()` (`:50`) 0 callers. **`:43` adds a `CanvasLayer` to
`GetTree().Root`** — it survives scene changes.

**Fix (S):** parent to `CurrentScene`; wire `HealthComponent.HealthChanged → Flash()`.

### `MenuComponent` — fragile focus resolution
**Evidence:** `:38-48` auto-wires to a sibling `NavigationComponent`; self-sufficient; 0 scenes.

**Defect:** `:85` uses `GuiGetFocusOwner()` to resolve which button fired → a mouse click without
focus gives the wrong button or null.

**Fix (S):** bind the button identity at connect time.

### `BuffBarComponent` — allocating lookup every frame
**Evidence:** `:44-50` needs a sibling `StatusEffectComponent` (which exists); self-wiring; 0
scenes.

**Defect:** `:53-56` calls `GetSiblingComponent` **every frame** in `_Process`.

**Fix (S):** cache the sibling in `_Ready`.

### `InteractionPromptComponent` — complete, and 4 lines from working
**Evidence:** `Show(string)` (`:59`) / `Hide()` (`:69`) — **0 callers.** Meanwhile
`InteractableComponent.cs:20-21` emits `PlayerEnteredRange`/`PlayerExitedRange` (`:51`,`:60`) and
holds `PromptText` (`:13`) that **nothing reads**. Its own doc (`:7-8`) names the exact intended
edge. It has even been debugged for this use — `:48-51` carries a fix comment about the parent
being a `CanvasLayer`, not a `Control`.

**Someone walked up to this wire and stopped.**

**Fix (S — ~4 lines in `InteractableComponent`, zero here):** it is a **cross-tree** wire (prompt
on the HUD, interactable on a world `Area2D`), so `GetSiblingComponent` won't do — use
`FindComponent<T>(GetTree().Root)`, the pattern `WeatherHUDComponent.cs:102-103` already uses. It
activates the dead `PromptText` export at the same time.

### `LoadingScreenComponent`
**Evidence:** `LoadScene` (`:58`) 0 callers. Its doc (`:10-11`) says *"Pairs with
NavigationComponent: connect BeforeNavigate →"* — **that edge does not exist.**

**Fix (S):** `NavigationComponent.BeforeNavigate → LoadScene`.

### `WeatherHUDComponent`
**Evidence:** `:92` self-discovers via group `"weather_system"` — **which is joined**, so it would
work. 0 scenes. The only external mention is a comment (`WeatherSystemComponent.cs:224`).

**Fix (S):** add to a HUD `.tscn` with the named Labels. **Possibly redundant** with
`WeatherForecastUI` (shipped, 6 scenes) — **UNCERTAIN**: both read the same system, but that is a
`Control` renderer and this is a `Label` binder. Decide before wiring.

---

## Not what their names suggest — do not plan around them

- **`DragComponent` is UI drag only.** `:33` `GetParent() as Control` → null on a unit; `:36`
  subscribes `GuiInput`, which world-space nodes never emit; **`:84` `_control.Position = newPos`
  moves the node it is attached to.** Selection moves nothing. **0% overlap with the proposed
  `SelectableComponent`** — which needs a screen-space rubber-band + a hit-test against world
  entities, the inverse operation. Write it fresh.
- **`MarqueeComponent` is a text ticker**, not an RTS drag-select marquee. `:29` `GetParent() as
  Label`; exports are `Speed`, `PauseAtStart`, `Bounce`. A news crawl.
- **`NavigationComponent` is not pathfinding** (see above).

---

## REDUNDANT — `UIEffectComponent` wins

### `PulseComponent`, `ShakeComponent`, `SlideInOutComponent`
**Evidence:** `UIEffectComponent.cs:7-8` has all three as `EffectType` values plus 4 scopes — a
strict superset — **and it is the one ported to GDScript** (`beep_ui/effects/ui_effect.gd:5`,
*"Ports UIEffectComponent.cs"*). `SlideInOutComponent.cs:98-110` also fakes multi-target completion
off `_activeTweens[0]` (dead `_finishCount` at `:30,:75`).

**Fix (M):** absorb `EffectComponent.ApplyToChildren`'s cascade into `UIEffectComponent`, **then**
delete all three. → `DELETE.md`

---

## Order

1. The five silent-bug fixes (S each): `TableComponent` panel, `TooltipComponent` gate,
   `RatingComponent` closure, `SearchBarComponent` debounce, `ToastNotificationComponent` static.
   **All five look correct in review and fail on first use.**
2. `InteractionPromptComponent` wire (S) — 4 lines, activates a dead export.
3. `NavigationComponent` dedupe (S); `ScreenFlashComponent` reparent (S); `BuffBarComponent` cache
   (S); `MenuComponent` focus (S).
4. `BossHealthBarComponent` rewrite or delete (M); `DialogUIComponent` → binder (M).
5. Absorb the cascade into `UIEffectComponent`, delete the three effect dupes (M).
6. Add parent-mismatch warnings across the widget set (S, mechanical).
7. Decide `WeatherHUDComponent` vs `WeatherForecastUI`.
