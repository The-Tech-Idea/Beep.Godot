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
