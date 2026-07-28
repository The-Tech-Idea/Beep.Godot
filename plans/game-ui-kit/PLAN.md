# Game UI Kit — a library of game interface elements

> **Status:** planning. No code written against this yet.
> **Supersedes:** the incremental "skin the generated theme harder" work tracked in
> `plans/MASTER_TODO.md` Stages 31–32g.

---

## 1. Why the current approach cannot get there

Everything shipped so far generates a Godot `Theme` and points it at 50 skins. That makes a
`Button` prettier. It does not make a game button, because **a game button is not a rectangle
with a nicer border** — it is an assembly.

Read `Example_Art/Upgrades.png` and list what is actually on screen:

| element | what it really is |
|---|---|
| "UPGRADES" header | ribbon with **folded ends that extend past the frame**, overhanging the top edge |
| outer panel | wood plaque + **metal corner brackets** + separately recessed inner well |
| skill icons | octagonal **gold bezel** + inner icon + optional glow, in tier columns |
| locked skills | the same node in a **stone-grey bezel** — a state, not a modulate |
| costs | dark pill with star + number, **pinned to the node's corner**, overlapping it |
| connectors | **lines between nodes**, coloured by branch and by unlocked state |
| column footers | **shield-shaped** tab with a gem, welded to the column base |
| tooltip | floating black panel, yellow title, no tail, drawn above everything |
| RESET / DONE | chunky bevelled plaques; UNDO is a **distinct disabled sculpt**, not 50% alpha |

A Godot `StyleBox` can express exactly one of those (the plaque). Every other item needs
**layers, non-rectangular silhouettes, elements that overhang their parent, and attached
sub-elements**. That is why each round of theming work has produced "better colours, still
looks like an app": the widget vocabulary is missing, not the palette.

The same vocabulary repeats across the rest of the reference set — `rpgui*.png`,
`skilltree*.png`, `citybuilder1-5.png`, `racing1-4.png`, `gameui1-9.png`, `settings1.png`.

**Decision: build a widget library. Stop deepening the theme generator.**

---

## 2. What the library is

A set of `[GlobalClass]` Godot `Control`s under `ecs/ui/kit/`, each a real game element with
its own anatomy, states and skin hooks. They are **composed from five primitives**:

1. **`KitLayer`** — one drawn layer: a 9-patch texture *or* a procedural shape, with its own
   offset, modulate role, and z-order. A widget is an ordered list of these.
2. **`KitShape`** — the silhouette: `Rect · Round · Chamfer · Clip · Notch · Speed · Ribbon ·
   Shield · Octagon · Ellipse · Arch · Pill · Arrow · Chevron · Parallelogram · Pentagon`.
   The first ten are already proven — `tools/genre_shapes/` generates and verifies them per genre
   and all 740 nine-patches pass. The last six come from the golden-kit sheet (§7), which uses
   them for nav, status chips and domed headers.
3. **`KitMaterial`** — the named layer stack a genre defines ONCE and every widget inherits:
   `Base → Bevel → Gloss → Rim → Sparkle`. **Each layer is either a procedural shape or a
   9-patch texture slot from the genre's art set** — the same layer names resolve to
   `kit/<genre>/<widget>_<layer>.png` when art exists and fall back to procedural when it does
   not. A material that can only draw procedurally cannot express wood grain, brushed metal,
   parchment or carbon weave, which is most of what separates the reference kits from each other. Confirmed by the golden-kit reference sheet
   (§7), where a single gold material is applied unchanged across a dozen silhouettes. Without
   this each widget would describe its own layers and they would drift apart.
4. **`KitGeometry`** — the genre's PROPORTIONS. Corner fraction, height-to-font ratio, padding,
   rim weight, bevel depth, gloss angle, stud/ornament density, attachment overhang. A genre is
   recognisable by its **build**, not only its palette: an RPG plaque is squat, thick-rimmed and
   heavily bevelled; a racing chip is tall, thin-rimmed and raked. Without this every genre is
   the same button in a different colour — see §4.1.
5. **`KitAttach`** — a sub-element pinned to an anchor point of its host, **allowed to overhang**
   (`TopLeft … BottomRight`, `Above`, `Below`). This is the piece Godot has no answer for and
   the reason banners and cost badges currently look wrong.

Skinning stays exactly as it is now and is already correct:
**genre → silhouette · theme → colour identity · palette → tint**, with every colour resolved
through `UiSurface` roles and every size through `UiSurface.FontSize`. The library consumes
that; it does not replace it.

---

## 3. The catalogue

Grouped by what they are for. **Bold** = not expressible as a themed Godot control today.

### 3.1 Frames and panels
- **`KitPanel`** — frame + recessed well + corner brackets; well optional (flush variant).
- **`KitBanner`** — **ribbon / plaque / shield / ellipse**, overhangs its host, folded ends.
- **`KitWindow`** — modal shell: banner + body + action bar + close stud.
- **`KitTooltip`** — floating panel, role-coloured title, drawn on a top layer.

### 3.2 Buttons
- **`KitButton`** — layered plaque: base + bevel + face + **rim light** + optional stud corners.
  Five *sculpted* states (normal/hover/pressed/disabled/focus) — pressed re-draws sunken,
  disabled re-draws de-sculpted. Not alpha changes.
- **`KitIconButton`** — square/round bezel + icon + optional **corner cost badge**.
- **`KitTab`** — welded top-rounded tab; selected merges into the panel below it.
- **`KitToggle`** — sliding knob in a track, with an on/off sculpt.

### 3.3 Readouts
- **`KitBadge`** — circular icon frame overhanging a capsule plate (exists as
  `ResourceBadgeComponent`; folds into the kit).
- **`KitMeter`** — **outer frame + recessed inner track + fill + end caps**, not a bare groove.
  The reference sheet frames every bar; an unframed track is what makes a meter read as a
  progress bar rather than a gauge. Optional icon cap and tick marks.
- **`KitStatusChip`** — small pentagon/round chip carrying a single semantic glyph
  (✓ success, ✗ danger). Used inline, not as a button.
- **`KitCounter`** — pill with icon + value, for currency.
- **`KitStatRow`** — label · dotted leader · value, the RPG sheet row.

### 3.4 Containers with motion
- **`KitCollapsible`** — header + **slide/clip reveal**, opens up or down depending on anchor.
- **`KitDrawer`** — edge-docked panel that slides in from a screen side.
- **`KitCarousel`** — paged strip with nubs.
- **`KitAccordion`** — stacked collapsibles, one open at a time.

### 3.5 Structured views
- **`KitSlotGrid`** — inventory: slot sculpt (empty/filled/locked/highlight), drag, stack count.
- **`KitTree`** — **skill/upgrade tree**: nodes + tiers + **connector lines** coloured by branch
  and unlock state + locked sculpt + cost badges. Directly from `Upgrades.png` / `skilltree*.png`.
- **`KitCardHand`** — fanned card layout with hover lift.
- **`KitList`** — banded rows with hover/selected sculpts.

### 3.6 Ornaments
Decorative `KitAttach`es with no interaction, which promote a plain plate into a reward or
achievement element: **crown, wings, star cluster, heart, laurel, trophy, ribbon tail**. Pinned
to a host anchor and allowed to overhang. Absent from the first draft of this catalogue; the
golden-kit sheet uses them constantly.

### 3.7 Overlays
- **`KitToast`**, **`KitDialog`**, **`KitPrompt`** (input glyph + label), **`KitRadial`** (radial menu).

---

## 4.1 The failure this section exists to prevent

The phase-A proof rendered one `KitButton` under rpg/fantasy, shooter/cyberpunk, racing/neon,
survival/wilderness and puzzle/candy. **All five came out as the same brown chamfered plate.**

Two causes, both structural rather than cosmetic:

- **Geometry barely varied.** Corner fraction, height ratio, padding and rim weight were
  constants on `KitControl`, so every genre inherited one build and differed only by a bevel
  multiplier. Chamfer vs clip vs notch is invisible at button size when everything else matches.
- **Texture was not involved at all.** `KitControl` drew procedurally only. The 740 verified
  nine-patches in `tools/genre_shapes/` were never consulted, so no genre could carry a material.

**Acceptance test for the whole kit — the greyscale rule.** Render the same widget across all
ten genres and desaturate. If the genres are not still tellable apart, the kit is skinning by
colour and has failed. Colour must be the *last* thing that distinguishes a genre, not the only
one. This is checkable and belongs in `tools/genre_shapes/` beside the nine-patch verifier.

### 4.2a MEASURED from the reference art — supersedes my invented table

The table in 4.2b was guessed. Measuring `Example_Art/rpgui.png` (a real RPG asset sheet)
changes the STRUCTURE, not just the numbers:

**A game control is a FRAME around an INNER PLATE — two nested shapes, not one plate with a
bevel.** The PLAY button is a ~10px wood frame containing a separate green plate with its own
bevel and its own rim. Every button, bar and plaque on that sheet is built the same way. My
`DrawMaterial` draws a single plate, which is the real reason each genre reads as generic
regardless of corner radius or bevel depth.

Measured from `rpgui.png`:

| element | measurement |
|---|---|
| PLAY button | ~380x85, frame ~10px (12% of height), inner plate inset ~10px, corner ~6px (0.07 of height), **4 corner studs** |
| title bar ("Knight - Level 8") | frame + deeply recessed dark well, gold rim ~3px, **close button attached at the right end**, **pouch icon overhanging the left** |
| health bars | capsule + **separate metal end caps** as attachments, not part of the bar |
| blue plaque | ~250x60, chamfered corners, gold rim ~3px, stud per corner |
| icon buttons | square, corner ~0.18, coloured inner plate, thin light rim |
| banners | cloth body + **rod above** / shield bottom / folded ribbon ends — all attachments |

**Consequences for the kit:**

1. `KitControl` needs `Frame` and `Plate` as separate layers with independent shape, inset and
   rim — not one `Base` with a bevel. This is the biggest missing piece.
2. Frame thickness is a fraction of HEIGHT (~0.12 for rpg), so it scales with the widget.
3. End caps, rods, close buttons and overhanging icons are `KitAttach`es — the primitive is
   right, but nothing uses it yet beyond the badge.
4. Corner radius on real RPG art is SMALL (~0.07), not the 0.16 I guessed. The genre reads as
   RPG through frame thickness, studs and material — not through its corner radius.

**Method for the remaining nine genres:** measure the same six rows from that genre's sheet
(`citybuilder1-5`, `racing1-4`, `skilltree*`, `Upgrades`, `gameui1-9`, `settings1`) before
writing any numbers. No invented values.

### 4.2b Per-genre build (INVENTED — superseded by 4.2a, kept only to show what was assumed)

Every column is a `KitGeometry`/`KitMaterial` field. These are the numbers that must differ, and
they are what the greyscale test checks.

| genre | silhouette | corner | height×font | rim | bevel | gloss | surface texture |
|---|---|---|---|---|---|---|---|
| rpg | chamfer + studs | 0.16 | 2.9 | 3.0 | 1.2 | 0.55 | wood grain |
| survival | notch, chipped | 0.12 | 2.7 | 3.0 | 1.1 | 0.20 | rough hide / rust |
| strategy | square + brackets | 0.04 | 2.4 | 2.5 | 0.8 | 0.25 | brushed steel |
| shooter | clipped corners | 0.10 | 2.3 | 1.5 | 0.6 | 0.35 | matte composite |
| racing | raked / speed | 0.08 | 2.2 | 1.5 | 0.7 | 0.85 | carbon weave + lacquer |
| citybuilder | square, crisp | 0.06 | 2.5 | 2.0 | 0.7 | 0.30 | flat matte |
| cardgame | round, card-like | 0.22 | 2.8 | 2.0 | 0.9 | 0.70 | linen / felt |
| platformer | pill, chunky | 0.45 | 3.1 | 3.5 | 1.3 | 0.80 | glossy plastic |
| puzzle | squircle | 0.30 | 3.0 | 2.5 | 1.2 | 0.90 | candy gloss |
| topdown | round, plain | 0.18 | 2.6 | 2.0 | 1.0 | 0.40 | soft matte |

Height×font is the button height as a multiple of the theme font, so a genre stays proportioned
at any type size. Texture column names the material each genre's 9-patch layers must carry —
`gen_all_genres.py` currently emits flat greyscale sculpts, which is why nothing reads as a
material yet.

---

## 4. Architecture

```
KitControl  (abstract Control)
├─ Geometry    : KitGeometry  corners, ratios, padding, rim, bevel — set per GENRE
├─ Material    : KitMaterial  Base → Bevel → Gloss → Rim → Sparkle, each procedural OR 9-patch
├─ Layers      : KitLayer[]   ordered, each shape or 9-patch, role-coloured
├─ Attachments : KitAttach[]  overhanging sub-elements + ornaments
├─ States      : Normal Hover Pressed Disabled Focus Locked Selected
├─ Skin        : genre → shape · theme → colour · palette → tint   (via UiSurface)
└─ Metrics     : every size derived from UiSurface.FontSize
```

**Rules the library must hold to** — each one is a defect already paid for this session:

1. **No colour literals.** Every colour is a `UiSurface.Role` or a theme lookup.
2. **No pixel font sizes.** Every metric derives from `UiSurface.FontSize(this, scale)`.
3. **No `AddThemeStyleboxOverride` on a kit widget.** That is what made the weather cards
   ignore the skin entirely.
4. **Nothing reparents a scene tree** other components hold NodePaths into.
5. **Ornament respects 9-patch margins**, verified by `tools/genre_shapes/verify_ninepatch.py`.
6. **One global source** for genre/theme/palette (`SkinCatalog.SetActiveSkin`) — no per-scene
   copies to drift.
7. **No metric constants on `KitControl`.** Every proportion comes from `KitGeometry`, or genres
   collapse into one build. This is the rule §4.1 exists to enforce.

---

## 5. Phases

| phase | deliverable | done when |
|---|---|---|
| **A — Foundation** | `KitControl`, `KitLayer`, `KitShape`, **`KitGeometry`**, `KitMaterial`, `KitAttach`, `KitState`; layered draw, texture-or-procedural layers, attachment anchoring | one button across 10 genres is **tellable apart in greyscale** (§4.1). Colour-only difference = phase A not done |
| **B — Core widgets** | `KitPanel`, `KitBanner`, `KitButton`, `KitIconButton`, `KitTab`, `KitBadge`, `KitMeter` | settings + one HUD rebuilt on the kit, no generic `Button`/`PanelContainer` left |
| **C — Motion** | `KitCollapsible`, `KitDrawer`, `KitAccordion`, `KitCarousel` | weather forecast, build toolbar and quest log all use kit motion |
| **D — Structured** | `KitSlotGrid`, `KitTree`, `KitList`, `KitCardHand` | rpg inventory + an upgrade tree matching `Upgrades.png` |
| **E — Art** | per-genre **material textures** per layer per widget (`kit/<genre>/<widget>_<layer>.png`), extending `gen_all_genres.py` | nine-patch verifier **and** the greyscale test pass for the full kit across 10 genres |
| **F — Migration** | port the 10 genre HUDs and 25 screens onto the kit | zero direct `Button`/`PanelContainer` in game scenes |
| **G — Retire** | delete superseded paths in the theme generator | theme generator only styles editor/inspector fallbacks |

Phases A–B are the ones that prove or kill the approach. Nothing after B should start until a
rebuilt screen sits next to `Upgrades.png` and holds up.

---

## 6. Open questions for you

1. **Art source.** Kit layers need per-genre art. Generate procedurally (current
   `gen_all_genres.py`, fully controllable) or buy/download a CC0 kit and slice it? Generated art
   will never look hand-painted like `Upgrades.png`.
2. **Scope of migration.** Port all 10 genres, or prove on RPG + city-builder first?
3. **`beep_ui` GDScript addon.** It already ships 84 themed widgets. Retire it, or is the kit
   the C# replacement for it?

---

## 7. Reference material

Reference sheet, supplied by the owner:
[Golden interface game buttons, ui, gui elements](https://www.pinterest.com/pin/1000643610955128361/)
— a stock vector kit. Its value is structural: it demonstrates **one material stack applied
across a dozen silhouettes**, which is the model in §2. **Licensing: stock vector, style
reference only** — not shippable art unless separately licensed, same rule already applied to
`gameui2/3/7`.

Local: `Example_Art/` — `Upgrades.png` (richest single reference), `skilltree*.png`,
`rpgui*.png`, `gameui1-9.png`, `citybuilder1-5.png`, `racing1-4.png`, `settings1.png`.
Licensing note from earlier audit: `gameui2/3/7` are watermarked comps — **style reference only,
never shipped art**.

Online:
- [Game UI Database](https://www.gameuidatabase.com/) — 1,300+ games, 55,000+ UI screenshots;
  the [Skill Tree set](https://www.gameuidatabase.com/index.php?scrn=64) matches §3.5 directly.
- [9-slice scaling explained](https://generalistprogrammer.com/tutorials/nine-slice-scaling-explained)
  and [GameMaker's 9-slice guide](https://gamemaker.io/en/blog/slick-interfaces-with-9-slice) —
  the stretchable-centre rule the verifier already enforces.
- [ui-patterns.com game mechanics](https://ui-patterns.com/patterns/game-mechanics/list) — naming
  for the interaction patterns behind §3.4/§3.5.
