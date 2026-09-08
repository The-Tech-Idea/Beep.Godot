# Game UI Kit — specification

The contract for `addons/beep_game_builder_cs/ecs/ui/`. Three source files already cite this
document (`SkinCatalog.cs`, `PanelFrameComponent.cs` twice); until now it did not exist, and the
kit's design rationale pointed at nothing.

## The three layers

| Layer | Where | What it is |
|---|---|---|
| Drawn widgets | `ecs/ui/kit/Kit*.cs` | Real Godot `Control`s that paint the genre's material stack themselves. 60 of them. |
| Behaviour components | `ecs/ui/*Component.cs` | Attachable behaviours that bind nodes a scene author placed, and compose the widgets above. |
| Genre HUDs | `ecs/ui/hud/*.cs` | One per genre, assembling readouts for that genre's screen. |

### When a thing is a widget and when it is a component

A **widget** draws. If it paints pixels through `KitControl`/`KitChrome`, owns a silhouette, and
would look wrong drawn by anything else, it is a `Kit*` widget.

A **component** wires. If it reads a `NodePath` the author set, listens to a signal, or decides
*when* something shows, it is a `*Component`. A component may build a widget to display its state
(`ChipComponent` holds a `KitRemovableChip`, `RatingComponent` a `KitStarRating`,
`AbilityBarComponent` drives a `KitSlotGrid`), but it never re-draws what a widget already draws.

The two layers overlapping is **not** by itself a defect. `TabGroupComponent` switching tabs over
Buttons the author placed is a different job from `KitTabStrip` drawing its own tabs, and both are
legitimate. Ask what varies, not what looks similar.

## Genre variants are meant to diverge

**This is the rule most easily got backwards.** Each genre owns its own UI, look and texture. The
per-genre class — a HUD, a `theme.json`, a `*_main.tscn` — is the seam where that divergence lives.

Two genres whose code currently reads the same is a statement about how far each has been styled,
not a shared implementation. Never merge them into a common base to remove the repetition: that
welds the coincidence shut and deletes the place they are supposed to grow apart.

The contrast that makes this concrete:

- ~90 copies of one redraw helper across the kit **were** a defect. Every widget wanted identical
  behaviour out of one mechanism, so one mechanism is what they now share.
- Two genre HUDs with matching bodies are **not** a defect. They are not meant to stay matched.

The test is whether the two should remain identical forever, not whether they are identical today.
Shared *mechanism* belongs in the base that already exists (`GenreHudComponent`, `KitControl`,
`KitChrome`, `UiSurface`). Shared *appearance* is a coincidence and stays where it is.

## Skinning: three inputs, one widget

```
genre   -> silhouette, proportions, register, font role   (KitGeometry, KitMaterial)
genre   -> material: procedural stack or nine-slice art   (KitGeometry.Material, KitSprite)
theme   -> colour identity                                (ColorSchema -> UiSurface roles)
theme's "kit" block -> style overrides on the genre       (KitStyleJson -> KitGeometry)
palette -> a tint applied inside the theme                (ColorPalette)
```

A widget never stores a colour. It asks `UiSurface` for a **role** — `Accent`, `Danger`, `Focus` —
and the theme decides the value. A `Color` written into a scene or a component is a palette pinned
where no skin can reach it.

### The material axis: procedural or artwork

A plate is drawn one of two ways, chosen per genre by `KitGeometry.Material` and overridable per
theme with the `kit` block's `material` key (`"none"` returns a genre to procedural drawing):

- **Procedural** (the default, and what every genre did before) — the register's band stack:
  flat fill, rim, keyline, a seven-band vertical shade, bevel and gloss, all arithmetic over the
  palette's face colour.
- **Artwork** (`KitSprite`) — one neutral nine-slice sprite per widget class, re-tinted to the
  palette and drawn in place of that stack.

**This is not the per-theme texture system that was removed.** That one shipped an art set per
genre *and per theme*, which cannot scale to 51 themes and drifts apart; the contract scan still
bans its nineteen `textures/<genre>/<theme>/` folders and should keep doing so. This is one neutral
set, tinted. The source art measures 0.056 centre saturation — pure shading with no colour of its
own — so a palette colour can be imposed on it.

Three rules bound it, and each exists because breaking it was tried and looked wrong:

1. **The art is re-tinted, not modulated.** A multiply preserves the artwork's ratios and vanishes
   on a dark plate: a 0.10 face compresses the sprite's 0.61-to-1.00 ramp into four hundredths of
   luminance. `KitSprite` anchors the face band on the palette colour and lets the ramp reach a
   fixed distance either side, handing the shadow side's unusable reach to the highlight when the
   skin is too dark to spend it. Same principle as `KitChrome.RecessFace`.
2. **A nine-slice is a rounded rectangle, so it stands in for one.** `KitSprite.FitsSilhouette`
   admits `Rect` and `Round` and refuses everything else — the pill, the chamfer, the speed wedge,
   the torn edge, the pixel staircase. A genre whose identity is its outline keeps it. The Chip
   class carries no artwork at all for this reason: every genre's chip is a pill.
3. **Only a widget's own plate takes it.** `KitControl.DrawPlate` and `KitChrome.DrawWidgetPlate`
   say "this rect is the widget"; `DrawShape` says "this is a knob, a tick, a badge, a well". The
   distinction is stated at the call site because it cannot be inferred — a slider's track is drawn
   with the genre's class shape too, and it is not a plate.

The tinted textures are cached on the drawing control's own metadata, not in a static field.
A static C# reference to a Godot `RefCounted` outlives the engine's teardown of the mono runtime
and takes the process down with it; node metadata is released during normal scene teardown.

### Two geometry owners, deliberately

`theme.json` carries **two** unrelated geometry vocabularies, and they do not meet:

- The `geometry` block (`corner_radius`, `border_left`, `pad_*`, `font_size`, in pixels) reaches
  **native Godot controls only**, through the generated `Theme` that `ThemePresetComponent` builds.
- The `kit` block (`corner`, `rim`, `bevel`, `height_ratio`, as fractions and ratios) reaches
  **kit widgets only**, through `KitStyleJson` patching the genre's `KitGeometry`.

A plain `Button` and a `KitButton` side by side can therefore disagree about corner radius, and
nothing reconciles them. That is the intended split — the kit's shape is a property of the genre,
not of the theme — but it is worth knowing before chasing a "bug" where two controls differ.

### Unknown keys are reported

All four blocks (`colors`, `geometry`, `animation`, `kit`) validate their key names and warn on
anything unrecognised. A mistyped colour key would otherwise fall back to **white**, which is loud
on screen and says nothing about why. `KitStyleJson.WarnUnknownKeys` and
`SkinCatalog.WarnUnknownKeys` are the two implementations, one per side of the split.

## Looking like a game control

Three rules, each learned from a widget that stopped looking like one. All are measured by
`tests/kit_check_controls_contrast_probe.gd`, which samples real rendered pixels on a dark stub
theme.

**A recess must be visible on every skin.** Darkening by a shade under 1 works on a mid or light
plate and does nothing on a dark one. Measured on the probe's theme, a panel well came out at 0.040
luminance and a gem socket at 0.019 — black voids, drawn correctly and invisible. Every well goes
through `KitChrome.RecessFace`, which darkens when the skin leaves room and lifts when it does not,
with a floor so the deep readout shade cannot bottom out at black. Never write
`face.R * WellShade` directly; a pin rejects it.

**A bevel highlight is a light, not paint.** The theme names its tint — warm on brass, cold on steel
— but the authored value is a surface colour. `oilfield_days` declares `#657275` for
`border_bevel_light`, and drawing that literally at the bevel's alpha damps the highlight to
nothing: every widget in the kit flattened the moment those colours were first read.
`UiSurface.Bevel` pushes each to its pole, keeping the hue and restoring the relief.

**A widget should look like a game control before it is configured.** Most of the kit ships an
opinionated default — `KitRow` says "Recover the Cargo", `KitInputHint` says `[E] Gather Wood`.
`KitToast` and `KitDialogBox` shipped empty and drew as blank rectangles wherever they were dropped,
including the browser. Defaults are short enough that the widget's natural width still fits a phone
column, since that text feeds `_GetMinimumSize`.

A note on measuring rather than eyeballing: a render made the inventory slot grid look like black
squares too, and it measured 0.111 — dark, but never a void. Only the panel well and the gem socket
were genuinely broken. The probe reports all of these numbers on success so the next change can be
judged against them.

## Input and accessibility contract

Every interactive widget answers this, and `tests/addon_contract_scan.ps1` enforces it.

1. **Activation is `ui_accept`; cancel is `ui_cancel`.** Never a key code. Godot's built-in actions
   carry gamepad bindings, and `BeepInputMapGenerator` already binds pad buttons to both. A widget
   testing `Key.Enter` is unreachable by every controller.
2. **Direction is `ui_left`/`ui_right`/`ui_up`/`ui_down`**, plus `ui_home`/`ui_end` for the jump to
   either end. WASD stays as a keyboard-only extra inside `KitChrome.DirectionOf`, never bound into
   the project's `ui_*` actions — binding it there would make walking move the UI selection.
3. **A directional widget consumes the event only if the selection actually moved.**
   `KitChrome.NavigateOrRelease` is the one implementation. At an edge it declines, so Godot's focus
   traversal carries the player out. This is what stops a widget trapping them, and it matters far
   more on a controller, where the D-pad is the only way out.
4. **Sideways movement stays within a row.** A grid laid out in rows must not turn a right-press at
   the last column into a jump to the next row.
5. **Focus is visible and legible.** `KitChrome.DrawFocusRing` takes the theme's `border_focus`
   first — the same colour the generated `Theme` stamps into native controls' focus boxes, so one
   theme cannot paint two different focus colours — and only if it clears
   `KitChrome.MinFocusContrast` (3.0, per WCAG 2.2 SC 1.4.11) against the plate behind it.
6. **Contrast is measured, not asserted.** `UiSurface.ContrastRatio` uses sRGB-linearised relative
   luminance. `UiSurface.Luminance` is a cheap *tone* weight for "is this dark, lift it" decisions
   and is **not** a contrast metric; never build a compliance claim on it.
7. **Tooltips are the kit's own.** `KitControl._MakeCustomTooltip` returns a `KitTooltip`, carrying
   the genre meta and the theme across the popup boundary where neither is inherited. Per-item text
   comes from `_GetTooltip(position)`. `TabBar` resolves tooltips in C++, so `KitTabStrip` publishes
   native per-tab tooltips instead — a script override there is never consulted.

### Known limitations, stated rather than discovered

- **Removing a chip has no controller gesture.** `KitRemovableChip` maps removal to Delete and
  Backspace. Godot defines no built-in action for "remove", so this stays keyboard and mouse only.
  The chip's *activation* is reachable, inherited from `BaseButton`.
- **`kit_button_badge_probe` and `kit_check_controls_contrast_probe` need
  `--display-driver windows`**, because they sample rendered pixels. Every other kit probe is
  headless.
- **The `animation` block reaches only `Button`-derived nodes.** `SetupButtonAnimations` walks for
  `Button`, so sliders, panels and custom-drawn widgets animate not at all, whatever a theme
  declares. `shadow_lift` animates a 2px rise, not a shadow property; `focus_glow` tweens the
  node's `modulate`, not a drawn glow.

## Authoring rules

Enforced by the contract scan; each exists because it was broken once.

- **No `AddTheme*Override` outside `KitChrome`.** Use the change-aware helpers, which compare the
  inherited value first and guard against the synchronous theme notification Godot fires mid-write.
- **No `DrawString`.** Text goes through `KitControl.DrawText` or `KitChrome.DrawText`, so a theme's
  `text_treatment` reaches every string rather than the ones that remembered to ask.
- **No direct `CustomMinimumSize =`.** Publish through `KitChrome.SetAutoMinimumSize` /
  `RefreshAutoMinimumSize`, which are idempotent and do not ratchet.
- **No live `[Export]` auto-properties.** An export writes through a setter that compares, then
  refreshes; a bare auto-property changes nothing on screen.
- **Shared helpers live on the base.** `KitControl` exposes `Genre`, `RefreshVisualAndRedraw`,
  `RefreshMinimumAndRedraw` and `DrawFocusRing` as `protected`. Widgets deriving from a native Godot
  type cannot inherit them — C# gives one base and it must be the real control — so they forward to
  the `KitChrome` static instead of restating the body.
- **Never re-resolve the genre inside `_Draw`.** `KitChrome.GenreOf` walks ancestors; `Genre` is
  cached and refreshed on theme change.

## A note on the citations in widget comments

Many widget summaries cite `CATALOGUE-FROM-ART.md`, `plans/game-ui-kit/PLAN.md`, files under
`plans/game-ui-kit/art/`, or images under `Example_Art/`. **None of those are in this repository.**

They are deliberately left in place rather than stripped or repointed here. Each records where a
specific measurement came from — the 0.58 interior-to-pitch ratio on a slot grid, the 2:1 label to
value proportion, the eight independent sheets that put a welded footer under a card — and a
citation naming its source is worth more than one redirected to a document that does not contain
that measurement. Repointing them at this spec would read as provenance while being none.

If you are looking for those files: they were a design-time art analysis, and the conclusions that
survived it are the rules stated above. Treat a citation as an explanation of *why* a number is
what it is, not as a document you are expected to open.

## Where the pieces are

| Concern | File |
|---|---|
| Widget base, layer stack, draw helpers | `ecs/ui/kit/KitControl.cs` |
| Static drawing, theme overrides, input, focus, tooltips | `ecs/ui/kit/KitChrome.cs` |
| Enums, `KitGeometry`, `KitMaterial`, per-genre tables | `ecs/ui/kit/KitCore.cs` |
| The `kit` JSON block's schema and validation | `ecs/ui/kit/KitStyleJson.cs` |
| Palette roles, font sizes, contrast | `ecs/ui/UiSurface.cs` |
| Catalog loading, theme/palette/geometry parsing | `ecs/ui/SkinCatalog.cs` |
| Generated `Theme` for native controls | `ecs/ui/ThemePresetComponent*.cs` |
| Showcases | `templates/scenes/kit_gallery.tscn`, `kit_browser.tscn`, `theme_gallery.tscn` |
