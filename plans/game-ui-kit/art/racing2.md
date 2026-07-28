# `racing2.png` — mobile arcade racing HUD

**1000 × 563** · live gameplay screen · **angular slash / italic** family
**Relevance:** **`racing`**. Second racing reference, and a different sub-style from
`racing1`.

---

## Widget 1 — `StatPlate` (`PROGRESS 37%`, `POSITION 5/12`)

Scanned H at y=25 and y=45 across the left edge.

| property | measured |
|---|---|
| plate over sky | `#51728C` **L=0.43 S=0.27** |
| sky behind it | `#78A2C5` L=0.62 S=0.40 |
| **darkening factor** | 0.43 / 0.62 = **0.69 ×** → translucent black at roughly **30 % alpha** |
| left edge x | **762 at y=25 and 762–763 at y=45** |
| label | white caps, left-aligned |
| value | right-aligned; `PROGRESS` value white, `POSITION` value **red** |

**Measured correction to my own first read:** the plates *look* slanted, but the left edge
sits at the same x at two rows 20px apart. The plates are **axis-aligned rectangles**. The
angular character of this HUD comes from the banner and the magenta slashes (widget 2),
not from the stat plates.

Worth recording because it is exactly the mistake the kit made before: seeing a style and
implementing it on the wrong element. The slant belongs to the **decoration**, not the
container.

**A lighter alpha than the other families.** `citybuilder2` measured plates at ~45–55 %
alpha; this one is ~30 %. A racing HUD must not hide the road.

## Widget 2 — `EventBanner` (`FINISH`)

| part | observed |
|---|---|
| plate | wide dark translucent bar, **slanted ends** |
| accent | **two magenta parallelogram slashes on each side** of the word, outside the plate |
| text | white **italic** caps, large |
| position | horizontally centred, in the upper third |

The slashes are the identity of this skin: separate shapes, not a border, set at the same
angle as the italic text. `KitAttach` can place them; `KitShape` needs a plain
parallelogram.

Note the banner is **not** the same construction as the stat plates — it is slanted, they
are not. One skin, two plate treatments, split by role: *event announcements* are angular,
*persistent readouts* are rectangular.

## Widget 3 — `PauseButton`

Two white bars (`‖`) at the top-left, **no plate, no circle**. The smallest possible
control. Matches `racing1`'s plateless treatment.

## Widget 4 — `Minimap`

| part | observed |
|---|---|
| shape | **circle**, translucent dark |
| route | a cyan line tracing the track |
| player | a small arrow/marker on the route |
| compass | a small `N` at the rim |
| position | bottom-left |

Both racing references put the map **bottom-left** and the speedometer **bottom-right**.
That is a genre layout convention worth encoding as the `racing` skin's default anchors.

## Widget 5 — `BoostButton`

Rounded-square **yellow** plate with a battery/bolt icon, sitting above the minimap. The
only opaque, saturated element on the screen — because it is the only thing the player
taps.

**Interactive = opaque and saturated; informational = translucent and desaturated.** That
is a clean rule and it holds across both racing references.

## Widget 6 — `Speedometer`

| part | observed |
|---|---|
| shape | circular translucent dark disc |
| scale | tick numbers `0`–`9` around the rim |
| needle | thin **red**, pivoting from centre |
| unit | `km/h` in small caps |
| value | large `75` numeral, offset to the lower-right of centre |

Same arc-plus-digits pairing as `racing1`'s speedometer. The disc is translucent enough
that a scanline across it does not separate cleanly from the road — recorded as an
observation rather than a measurement, because I could not isolate its edge.

---

## Comparison with `racing1`

| | `racing1` | `racing2` |
|---|---|---|
| plates | **none at all** | translucent, ~30 % alpha |
| accent | gold | **magenta** |
| type | upright caps | **italic** caps |
| decoration | none | parallelogram slashes |
| speedo | semicircular arc, bottom-right | full circular dial, bottom-right |
| map | none visible | circular, bottom-left |
| leaderboard | 4-row list, player row filled | none — just position `5/12` |

Two racing HUDs, two different plate policies, but **identical anchor layout** and the
same arc-plus-digits gauge. So for `racing`:

- **layout is genre-level** (map bottom-left, gauge bottom-right, position top corner)
- **plate policy and accent are theme-level**

That split is precisely the `genre → theme → palette` cascade this project already has,
and it is the first time the folder has produced direct evidence for **which** properties
belong at which level.

---

## Cross-widget rules

1. **Interactive = opaque + saturated. Informational = translucent + desaturated.**
2. **Racing plate alpha is lighter (~30 %)** than builder/RPG plates (~50 %) — the road
   must stay visible.
3. **Slant belongs to decoration, not containers.** Measured: the stat plates are
   axis-aligned even in an angular skin.
4. **Event banners and persistent readouts may use different plate shapes** inside one
   skin, split by role.
5. **Racing layout anchors are genre-level**: map bottom-left, gauge bottom-right,
   position/lap top corners.

## Actions

- [ ] `racing` skin: plate alpha **0.30**, not 0.50.
- [ ] Add `KitShape.Parallelogram` slashes as a decorative attachment for banners.
- [ ] Encode racing's **default anchors** at the genre level in `SkinCatalog`.
- [ ] Add the rule **interactive = opaque/saturated** to the kit's state model.
- [ ] `EventBanner`, `BoostButton`, `Minimap` (circular, with compass) → catalogue.
