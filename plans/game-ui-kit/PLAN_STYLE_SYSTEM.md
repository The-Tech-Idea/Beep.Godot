# Plan — the kit needs a STYLE SYSTEM, not a per-genre lookup

Derived from the per-file art pass in [`ART_PASS_PER_FILE.md`](ART_PASS_PER_FILE.md).
Every claim below traces to a numbered file there.

---

## The diagnosis

The kit models genre → look as a **1:1 lookup**:

```
KitCore.ShapeForGenre(genre)  -> one KitShape
KitGeometry.ForGenre(genre)   -> one Register (Carved | Casual | Technical)
```

The art says that is the wrong shape of model, and says it five times in one genre:
**citybuilder** appears as cartoon-outlined (02), flat translucent (03), papery minimal (04),
monochrome drawer (05) and carved stone (06). Those are not variations of one look — they
disagree about outline polarity, shadow, corner radius, texture and typography.

So the genre does **not** determine the look. It constrains which looks are *plausible*. The
**theme** picks one. The kit has a theme layer already (`catalogs/skins/<genre>/themes/<theme>/`)
and the style properties below are simply not in it.

### What is missing, each with the file that proves it

| # | Missing axis | Proof |
|---|---|---|
| 1 | **Shadow as a layer.** `hard`, `soft`, `none` and `glow` all appear. `KitLayerKind` has no `Shadow` member at all. | hard 01·06 · soft 02·04·11·13 · none 03·07·09·10 · glow 06 |
| 2 | **Outline polarity is a theme property.** The `Casual` register hardcodes a thick *dark* outline. | thick **light** 12 · thick **dark** 02 · **hairline** 09·10 · **none** 04 · **dashed** 03 |
| 3 | **Frames are constructed, not bordered.** One `FrameMode` enum cannot express these. | corner rivets 11 · metal corner brackets 01·11 · square corner ticks 10 · L-brackets 10 · gold double-line 11 · organic log 13 · top-rounded-only 04 |
| 4 | **Font family, weight and case.** The kit has **no font family anywhere** — every genre shares the theme default. | **serif** 11 · **bold condensed caps** 06·08·10 · **thin letter-spaced caps** 07 · **light** 04·09 · **rounded display** 12·13 |
| 5 | **Corner radius is not one number.** | 0/sharp 07·10 · shear 08 · small 09 · large+wobble 12 · full pill 04 · per-widget mixed 11 |
| 6 | **Meter end caps.** Bars carry ornate caps, per tier. | 11 (three cap styles) · icon-cap-left 02·12 |
| 7 | **Attachments that overhang the host.** | icon cap left 02·12 · medallion top 04 · "NEW!" flag on a tab corner 13 · nav arrows over the frame 13 · awning 06 · banner over both side edges 01 |
| 8 | **Selection has several renderers, sometimes two on one screen.** | accent fill *and* accent border on the same screen 09 · glow 06 · lighter fill + border 05 · full lime border 10 |

---

## The model

Replace the two per-genre lookups with **three composable layers**:

```
genre   ->  which archetypes exist, and what they are FOR   (unchanged; already in genre.json)
theme   ->  a STYLE PACK: outline, shadow, frame, type, corner, material   (new)
palette ->  hue/sat/value transform                          (unchanged)
```

### `KitStyle` — the new theme-level record

```csharp
sealed record KitStyle(
    KitOutline   Outline,      // None | Hairline | ThickDark | ThickLight | Dashed
    KitShadowDef Shadow,       // kind (None|Hard|Soft|Glow) + offset + blur + alpha + colour role
    KitFrameDef  Frame,        // mode + corner ornament (None|Rivets|Brackets|Ticks|DoubleLine)
    KitTypeDef   Type,         // family role + weight + case + tracking + outline
    KitCornerDef Corner,       // radius fraction, per widget CLASS, + shear + wobble
    KitMaterialDef Material);  // grain pattern + amplitude + tiles  (already exists, folds in)
```

Authored in `theme.json` (snake_case, per `docs/FILE_FORMATS.md`), so **adding a style costs no C#** —
the same promise the skin system already makes for colour.

---

## Phases

Each phase ends with a **gate that must be shown to fail first**. The material axis
(`measure_material.py`) and the outline gate (`verify_greyscale.py`) are the pattern to follow.

### Phase A — `KitLayerKind.Shadow` *(smallest, unblocks the most)*
- Add `Shadow` to the layer enum and `KitShadowDef` to the style.
- Four kinds from the art: `Hard` (opaque, offset, no blur — 01·06), `Soft` (large radius, low
  alpha — 02·04·11·13), `None` (03·07·09·10), `Glow` (coloured outer, selection — 06).
- Shadow draws **first**, under the whole stack, offset by the style's vector.
- **Gate**: `measure_shadow.py` — a colour-invariant test that each style's rendered shadow
  matches its declared kind (offset > 0 and edge gradient ≈ 0 for hard; gradient > 0 for soft;
  no dark pixels outside the silhouette for none). Must fail on today's build, which has none.

### Phase B — outline polarity + corner per widget class
- `KitOutline` on the style; remove the hardcoded dark outline from `KitStacks.Casual`.
- `KitCornerDef` keyed by widget class (button / panel / slot / bar / chip), because 11 uses a
  different corner per class in one theme.
- Add `shear` and `wobble` as corner modifiers (08, 12).
- **Gate**: extend `verify_greyscale.py` with an **outline-polarity** column — rim:body > 1 for
  light, < 1 for dark, ≈ 1 for none. It currently measures magnitude only, so a light and a dark
  rim of equal contrast are indistinguishable to it today.

### Phase C — typography: family, weight, case, tracking
- `KitTypeDef` with a **font family role** (`Serif`, `Condensed`, `Rounded`, `Sans`, `Pixel`),
  resolved to a real `FontFile`.
- **Fonts must be CC0 and ship** — Kenney's UI packs include fonts under CC0 1.0, same source and
  licence as the grain patterns. Example_Art stays measurement-only.
- Case (`AsAuthored` | `Upper`), tracking, and text-outline width per style — 07 is
  letter-spaced thin caps, 06 is outlined bold caps, 04 is light letter-spaced small caps.
- Wire into `UiSurface.TextRole` (already exists) so role × style = final size, weight and family.
- **Gate**: `measure_type.py` — stroke weight and glyph aspect are measurable in greyscale; a
  serif and a condensed sans at the same point size must separate. Also assert every declared
  family resolves, since a missing font silently falls back to the default and looks identical
  to having no family at all.

### Phase D — constructed frames
- `KitFrameDef.CornerOrnament`: `None | Rivets | Brackets | Ticks | LBrackets | DoubleLine`.
- Drawn as a layer, so it composes with any silhouette.
- `Organic` frame (13) is deferred: it is an illustrated 9-patch, not a procedural outline, and
  belongs with baked art rather than here. **Say so rather than half-implementing it.**
- **Gate**: `poly_probe` already covers silhouette validity; extend it to assert each ornament
  draws inside its host's rect (the count-badge clipping bug in Stage 38f was exactly this).

### Phase E — attachments and meter end caps
- `KitAttach` exists; add the placements the art uses: `CapLeft`, `CapRight`, `MedallionTop`,
  `CornerFlag`, `EdgeArrow`, `Awning`.
- Meter end caps per tier (11) as an attachment pair on `KitMeter`.
- **Gate**: render each attachment at three host sizes and assert it stays anchored (the
  three-size discipline that caught the slot bugs).

### Phase F — selection renderers
- `KitSelectDef` as a **set**, not one value: `Fill | Border | Glow | Lift | Underline`.
- 09 proves two coexist on one screen, keyed by widget class.
- **Gate**: a screen with a selected item in two widget classes must render two distinct cues.

### Phase G — per-genre style packs
Only after A–F exist. Ship **two or three themes per genre**, drawn from the pass:

| genre | themes to author | from |
|---|---|---|
| citybuilder | carved-stone · flat-translucent · papery | 06 · 03 · 04 |
| strategy | carved-stone · monochrome-drawer | 06 · 05 |
| racing | hairline-ticks · sheared-mobile · typography-only | 10 · 08 · 07 |
| shooter | asymmetric-scifi · hairline-dark | sci-fi sheets · 09 |
| rpg | ornate-fantasy (serif, rivets, caps, soft shadow) | 11 |
| survival | wood-parchment (torn cards, log frame) | 13 |
| puzzle / platformer / cardgame | glossy-arcade (light outline, wobble) · papery | 12 · 04 |
| topdown | pixel-stepped | *(pass pending)* |

---

## Honest scope

- **13 of 60 files read in depth.** The remaining 47 are listed in `ART_PASS_PER_FILE.md`, by
  genre and priority. The model above already survives contact with 13 files across 6 genres;
  the rest will add ornaments and confirm assignments, not change the model. **If a later file
  does contradict it, the model changes — that is why the per-file notes exist.**
- **Phases A–C are the ones that answer the complaint.** Shadow, outline polarity and typography
  are the axes that make two themes of the same genre read differently. Silhouette (already done)
  was necessary and is not sufficient.
- `Organic` frames and illustrated 9-patches are **out of scope** — they need authored art, and
  the addon ships none. The right answer there is the baked-texture path plus documentation.

---

## Revision after files 14–28 — the frame model was too small

Phase D originally said "corner ornaments as a layer". **File 14 (the sci-fi frame sheet) makes that
insufficient**, and it is the single most important structural finding of the pass.

A sci-fi frame is not a border with decorated corners. It is a **run list per edge**: the stroke
changes weight along its length, breaks and restarts, turns into solid blocks, carries hatch and
tick runs, steps at the corners, and is **deliberately asymmetric between corners**. No StyleBox,
no single silhouette and no corner-ornament enum can express it.

### `KitEdgeRun` — replaces the corner-ornament enum in Phase D

```csharp
sealed record KitEdgeSeg(float Start, float Length,   // fractions of the edge
                         float Weight,                 // multiples of the base stroke
                         KitSegFill Fill,              // Solid | Hatch | Ticks | Gap
                         float Inset);
sealed record KitEdgeRun(KitEdgeSeg[] Top, Right, Bottom, Left,
                         KitCornerStep[] Corners);     // per corner, so asymmetry is expressible
```

Authored in `theme.json` like everything else. A plain rectangle is the degenerate case: one solid
segment per edge, no corner steps — so **every existing theme keeps working unchanged**.

### Frame construction families, all four seen

| family | construction | files |
|---|---|---|
| **Edge-run** | segments per edge, stepped asymmetric corners | 14 |
| **Masonry** | border built from individual blocks | 22 |
| **Plank** | four overlapping planks, picture-frame | 15 · 28 |
| **Double-border** | outer band + inner panel with a visible gap | 26 |
| **Frame + torn insert** | rigid frame, ragged insert revealing it | 23 |

### Phase E grows: attachment SETS, not single attachments

File 25 tells victory from restart from settings **by ornament alone** — crown, helm, gear. So the
attachment table is keyed by **screen archetype**, not just placement:

`Crown | Helm | Gear | Shields | CrossedWeapons | Drape | Awning | Foliage | Vines | Tiki`

Plus the near-universal one the kit half-has: a **header plaque overhanging the top edge**, whose
shape is itself a style property (bar 15·25·28 · ellipse 27 · contrasting-hue plate 26).

### Phase C grows: two more text treatments

- **Engraved / debossed** — light edge below, dark above, no outline (22). Neither "plain" nor
  "outlined"; it is how carved-material themes render text.
- **Handwritten** as a font role (18), and **3D extrude** as a display treatment (26).

### Phase B grows: the plate is two-tone, not a gradient

17 and 27 both put a **discrete lighter band across the top ~25 %** — a hard boundary in 17, a
**curved** one in 27. The kit's `Gloss` layer draws a soft linear band, which reproduces neither.
`KitGloss { Linear | HardBand | CurvedGlass }`.

### One correction to the phase order

**Phase A (Shadow) stays first** — it is still the cheapest and the most broadly wrong. But
**Phase D is now the biggest**, not the smallest, and shooter cannot be made to look like shooter
without it. Re-sequence: **A → B → C → D → E → F → G**, with D given its own gate
(`verify_edge_runs.py`: assert a declared run renders the declared number of segments, gaps and
corner steps, and that a plain rectangle still renders as one unbroken stroke per edge).

---

## Second revision — after 46 of 60 files

Three more changes to the model, all forced by images rather than reasoned into existence.

### Phase A gains a fifth shadow kind: `Extrude`

File 35 puts a **thick dark side face** under every panel and button, so each reads as a solid slab
seen slightly from above. That is not a drop shadow (no offset copy), not a bevel (not an inner
edge) and not a glow. Shadow kinds become:

`None · Hard · Soft · Glow · Extrude`

Files 41 and 38 add the other end of the range: **layered concentric strokes** (three of them) and
**nothing at all** used as the depth mechanism, so `None` is a deliberate choice with two distinct
compensations — a heavier outline (41) or pure value contrast against a ragged silhouette (38).

### `Pixel` is a register, not a corner treatment

Files 40 and 42 show that choosing pixel decides **outline weight (1px), anti-aliasing (off),
corner construction (stepped by pixel), font (bitmap) and shadow (none)** together. The kit
currently models this as `KitShape.Stepped` — one silhouette. It has to be a register alongside
Carved / Casual / Technical, or a pixel theme will keep drawing smooth type and soft shadows inside
a stepped outline.

### Attachment placements, complete list from the pass

`CapLeft` (02·12·17·19·28·36) · **`CapRight`** (41 — the mirror, first seen late) ·
`MedallionTop` (04·19) · `CornerFlag` (13) · `CornerBadgeDiamond` (43) · `EdgeArrow` (13·44) ·
`Awning` (06·25) · `Foliage` (22·28) · `Chains` / `Tape` / `Posts` / `Pins` (30·36) ·
`LabelTab` and `VerticalLabelTab` (43) · `Drape` / `CrossedWeapons` / `Crown` / `Helm` / `Gear` (25)

### One more thing the pass settled

**The header plaque overhanging the top edge is the single most repeated construction in the whole
folder** — 15·16·25·26·27·28·32·34·36·41·44, eleven files across every casual and fantasy family,
in four shapes: bar, ellipse, ribbon-with-folded-ends, and sheared tab. The kit draws a banner
*inside* the host at `0.14 × height`. That is the wrong side of the edge, and it is why panels read
as flat compared with the references.

---

## Third revision — pass complete (59/59)

The last 14 files added no new *axis*, but two of them isolate the model cleanly and three add
states the kit does not have.

### The two proof images

| file | what it isolates |
|---|---|
| 48 `square-wooden-frames` | **one geometry × many attachments** — six avatar frames, identical block, identity from vine / rope / nothing |
| 55 `wooden-game-buttons` | **one material × many silhouettes** — chamfer, capsule, hexagon, notched, tiered, circle, triangle, all in the same wood |

Together they settle the model: **geometry, material and ornament are three independent axes.**
The kit currently ties all three to `genre`.

### States the kit is missing

- **A fourth empty state** (53): a **ghosted silhouette** of what belongs in the slot, beside
  blank / invite-`+` / locked-with-requirement. `KitInventorySlot` has the first three.
- **Comparison indicators** (46): stat chips turn green with an up-arrow to show the delta against
  what is equipped. Nothing else in the folder does this, and no kit widget expresses it.
- **An authored four-state set** (51): `Normal · Over · Click · Disabled` shipped as art, with
  **per-state icon variants**. The kit derives states procedurally, which is right — but it should
  *accept* authored per-state art where a theme supplies it.

### Typography, final tally across all 59

`serif` 11·21·33 · `blackletter/gothic` 19·53·54 · `bitmap/pixel` 40·42·50·53 ·
`handwritten` 18 · `bold condensed caps` 06·08·10·38 · `thin letter-spaced caps` 07·43 ·
`light` 04·09 · `rounded display` 12·13·17·26·44 · `typewriter/condensed serif` 45

Nine families. The kit ships **one**. Phase C's font roles become:
`Serif · Blackletter · Pixel · Handwritten · Condensed · Rounded · Sans · Typewriter`

### Confirmed counts worth keeping

- **Header plaque overhanging the top edge**: 15 of 59 files. The single most repeated construction
  in the folder, and the kit draws its banner on the wrong side of the edge.
- **Icon cap overhanging an end**: 7 left (02·12·17·19·28·36·32) + 1 right (41).
- **Welded footer / cost strip**: 13·20·31·39·44·54.
- **Segmented meter**: 12·16·19·28·31·51·54 — the settled "segmented is the default" rule holds at
  seven independent sightings.
