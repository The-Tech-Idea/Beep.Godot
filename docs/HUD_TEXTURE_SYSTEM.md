# HUD Texture System — per-component, per-genre

> **Status:** plan revised 2026-07-26. Supersedes the three-master (`plate`/`tile`/`bar`)
> consolidation, which was wrong — see *Why the previous model failed* below.

## The rule

**Every HUD component type gets its own texture.** Not a shared plate tinted differently — an
actual authored 9-patch whose *shape*, *border* and *shadow* are baked into the art, with 9-patch
margins and content margins chosen for that component.

This is how shipped games do it, and it is forced by three things Godot's `StyleBoxTexture` /
`NinePatchRect` model makes explicit:

1. **Shape is per component.** A button may be a pill in one genre and a hard-cornered slab in
   another. A hotbar slot is square. A minimap frame is round. One master cannot express these.
2. **Corner margins are per shape.** A pill button needs a 9-patch margin large enough to contain
   its full corner arc; a 2px-radius slab needs almost none. Share a texture and one of them
   smears — either the corner stretches into an oval, or the flat edge wastes half the bitmap.
3. **Borders and shadows live in the art.** A drop shadow has to sit *outside* the content rect
   (`expand_margin`), a border *inside* it (`content_margin`). Those offsets differ per component,
   so they must be authored per component, not derived from a generic box.

## Why the previous model failed

The first pass baked 350 files (7 slots × 50 themes) — genuinely redundant, because within a
genre all five themes shared one shape and differed only in palette. Correct fix: **palette via
`modulate`, one master per genre.**

The *over*-correction was collapsing to three generic masters per genre. That threw away the
per-component axis, which is the one that actually matters. `hud_tile` was being asked to serve as
button, tab, inventory slot and toggle at once — four different shapes, four different corner
radii, four different content margins.

**The right split:**

| Axis | Varies by | Mechanism | Cost |
|---|---|---|---|
| Shape, border, shadow, margins | **component × genre** | baked master PNG | 10 genres × N slots |
| Palette | **theme** (5 per genre) | `modulate` in theme.json | free |
| State weight (normal/hover/pressed/disabled) | **state** | `modulate` scale + own master where the shape itself changes | mostly free |

Modulate is safe for palette because it *multiplies*: a baked black shadow stays black
(`0 × accent = 0`) while a white border becomes the accent and a mid-grey body becomes a dark
tint of it. Shadows survive tinting; that is why the shadow must be baked dark rather than drawn
as a coloured shape.

## Slot set

Each is a separate master per genre. `res://addons/beep_game_builder_cs/textures/hud/<genre>/<slot>.png`

| Slot | Component | Shape notes | 9-patch margin | Content margin |
|---|---|---|---|---|
| `panel` | HUD plate / dock / top bar | genre frame, outer shadow | 30 | 12 / 10 |
| `button_normal` | HUD button | genre button shape + border + shadow | per-genre radius + 6 | 14 / 9 |
| `button_hover` | ” | same shape, lit rim | ” | ” |
| `button_pressed` | ” | same shape, inset shadow flipped | ” | ” |
| `button_disabled` | ” | same shape, flattened | ” | ” |
| `button_focus` | ” | ring only, `draw_center=false` | ” | ” |
| `tab_normal` | toolbar category tab | top-rounded only, no bottom border | asym: 18/18/18/4 | 16 / 7 |
| `tab_selected` | ” | joined to the panel below | ” | ” |
| `slot_empty` | hotbar / inventory cell | square, inner bevel | 20 | 4 |
| `slot_filled` | ” | square, accent rim | 20 | 4 |
| `bar_bg` | meter track | capsule, inner shadow | radius+4 | 3 / 2 |
| `bar_fill` | meter fill | capsule, top gloss | radius+4 | 0 |
| `frame` | minimap / portrait | genre frame, `draw_center=false` | 34 | 8 |
| `tooltip` | tooltip plate | soft, strong shadow | 24 | 10 / 7 |

**14 slots × 10 genres = 140 masters**, coloured into 50 themes by modulate.

## Per-genre shape language

Applies to `button_*`, and sets the family radius the other slots inherit.

| Genre | Button shape | Border | Shadow |
|---|---|---|---|
| citybuilder | soft rect, r5, corner ticks | 1px hairline | subtle drop |
| strategy | hard rect, r3, corner rivets | 2px | hard drop |
| shooter | notched corners (chamfer), r2 | 2px angular | tight |
| rpg | ornate, r8, inner gilt line + corner diamonds | 3px double | heavy drop |
| survival | rough rect, r4, chipped edge | 2px broken | dirty |
| cardgame | r9, gilt double border | 2px + 1px inner | soft |
| racing | chamfered leading edge, r6 | 2px + accent stripe | sharp |
| puzzle | pill, r13, top gloss | 2px soft | soft large |
| topdown | r6, corner brackets | 1px | light |
| platformer | chunky r11, thick outline | 4px | offset chunky |

## Authoring / regeneration

Masters are baked greyscale so `modulate` can take them anywhere:

- body ≈ 0.28 luminance (becomes a dark tint of the theme accent)
- border = 1.0 luminance (becomes the accent itself)
- shadow = pure black with alpha (stays black under any modulate)

Generator: `tools/hud_textures/bake_hud_masters.py`. Re-runnable; writes the PNGs, their
`.import` sidecars, and the `hud_*` block in each `theme.json`.

## Texture source selection

Independent of all the above and already implemented (`TextureSlotDef.ResolvePath`,
dock §5): a project chooses where slot art comes from.

- **Built-in** — the shipped masters.
- **My own textures** — `res://<root>/<genre>/<theme>/<slot>.png`, or flat `res://<root>/<slot>.png`.
  Resolved **per slot**, falling back to built-in for anything not supplied, so replacing just the
  buttons does not blank the panels.
- **None** — ignore every slot; render the procedural `StyleBoxFlat` from theme colours.

Stored in `ProjectSettings` (`beep/ui/texture_source`, `beep/ui/texture_custom_root`) so an
exported build resolves the same art the editor previewed.

## Art source: real packs, not generated shapes

Procedurally drawn masters were tried and rejected — they read as an *effect*, not as game art.
Two layers now exist, and the second overwrites the first in place:

1. **Generated fallback** — `tools/hud_textures/bake_hud_masters.py` bakes 140 greyscale
   masters (252 KB). Always present, so no slot is ever empty and the addon works with no
   external dependency.
2. **Real art** — `plans/ui-asset-integration/import_kenney_hud.py` maps all 14 slots × 10
   genres onto Kenney's CC0 UI packs and copies them over the masters. 140/140 resolve, 0
   misses. Opt-in: the developer runs it against their own copy of the pack.

**Where each layer lives.** The addon ships **only** the generated masters — that is what keeps
Phase 1's "ship the feature, not the art" true for third-party assets. Real art is imported into
*your* project and selected there:

```
Beep.Godot/addons/.../textures/hud/<genre>/<slot>.png   generated masters, accent modulate
your-project/ui_textures/hud/<genre>/<slot>.png          real Kenney art, white modulate
                                                          + KENNEY_LICENSE.txt
your-project/project.godot                               beep/ui/texture_source="Custom"
                                                          beep/ui/texture_custom_root="res://ui_textures"
```

    python import_kenney_hud.py --dest "<your-project>/ui_textures"
    godot --path "<your-project>" --headless --import      # REQUIRED — see below

> **The import pass is not optional.** Copying a PNG and writing a `.import` sidecar beside it
> does *not* make the file loadable — Godot only produces the `.ctex` when its import pipeline
> runs. Until it does, `ResourceLoader.Exists()` returns false, `ResolvePath` falls back to
> built-in art, and **nothing reports an error**. This cost a full debugging cycle: the slots
> resolved 0/5 with correct paths, correct settings and correct catalog entries. Open the
> project in the editor once, or run `--headless --import`.

The importer also **calibrates 9-patch margins from each source's real pixel size**. The bake
script writes a flat `margin=30` because its masters are all 128×128; real art runs 18×18 to
192×64, and a 30px margin on a 64px source leaves a 4px stretchable centre, which smears. The
rule is `min(28, dim×0.28, dim/2 − 1)` per axis — always leaving a centre.

**Modulate differs between the two layers, and getting it wrong is a real defect.** Greyscale
masters carry the theme accent in `modulate` (that is how one master serves 5 palettes). Real art
is already coloured, so the same accent would tint it a *second* time and muddy it — the importer
therefore rewrites `modulate` to white-plus-alpha in the catalog of whichever project it imported
into, leaving modulate doing the only job a HUD still needs: letting the game show through.

> **Known constraint.** `modulate` lives in `theme.json`, so it is bound to the project's chosen
> art rather than to the slot. A project imported for real art carries white modulate; if it is
> switched back to Texture Source = *Built-in*, the addon's greyscale masters render untinted.
> Re-run `bake_hud_masters.py` against that project to restore accent modulate. Moving modulate
> into the source selection would remove this coupling.

Genre differentiation is explicit in the map: the four sci-fi genres take different colours
*and* different header cuts (`large` / `notch` / `blade` / `small`), because sharing one panel
made citybuilder, shooter, strategy and racing look identical.

## Open items

- [x] Bake the 140 masters — done, replaces the superseded 30 generic ones
- [x] Extend `ThemeTextureSlots` with the 14 HUD slots + `ParseTextures` keys
- [x] Real-art import path with per-genre mapping (140/140, 0 misses)
- [x] Texture source selector (Built-in / own folder / none), per-slot fallback
- [x] `ThemePresetComponent.HudMode` consumes the HUD slots — via `IHudTexturePreset`, resolved
      **per slot** with the procedural box as fallback, so a partial art set still works.
      Covers Panel/PanelContainer, Button family, TabBar/TabContainer, ProgressBar, tooltips.
- [x] 9-patch margins calibrated from each source's real pixel size
- [x] **Verified at runtime**, not assumed: `Button/normal`, `Button/hover` and
      `PanelContainer/panel` resolve to `res://ui_textures/hud/citybuilder/*.png` with white
      modulate. (The 2 unchecked slots in that probe are ProgressBar, which the citybuilder
      scene does not contain — not failures.)
- [ ] Asymmetric 9-patch margins in `TextureSlotDef` for the tab slots (currently symmetric)
- [ ] `expand_margin` so baked drop shadows sit outside the content rect
- [ ] Refine weak mappings: sci-fi `button_normal` maps to `button_square_header_*`, whose
      built-in header band splits a two-line label ("House ×2 / 1,200") across the seam — a
      plain button would sit better. rpg/cardgame `tab_normal` is a plain panel, and their
      `slot_empty` is a checkbox circle rather than a square cell.
