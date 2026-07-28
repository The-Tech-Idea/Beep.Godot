# `rpgui1.png` — premium dark-fantasy UI kit (Owl Studio)

**1129 × 1600** · asset sheet · **gold hairline on near-black, ornament-heavy** family
(Genshin/AAA-mobile register)
**Relevance:** **`rpg`**, `strategy`, `cardgame`. The highest-production-value sheet in the
folder and the **thinnest frame** by an order of magnitude.

---

## The headline: a 3px gold hairline is the entire frame

Scanned H at y=365 on a field plate.

| part | measured |
|---|---|
| background | `#302C32` L=0.18 |
| **frame** | **3px** gold hairline `#D3C698` L=0.71 S=0.40 |
| plate fill | `#211D30` **L=0.15 S=0.25** |
| plate width | ~180px |
| **frame : width** | **0.017** |

Compare the carved families:

| family | frame ratio |
|---|---|
| `citybuilder5` carved stone | **0.10** |
| `gameui2` wood | **0.08** |
| `rpgui1` gold hairline | **0.017** |

Nearly an order of magnitude apart. The `frame = 3.5px + 0.07 × h` formula does **not**
describe this family at all — a 50px-tall plate here would get 7px by the formula and
actually has 3.

**Revised understanding:** there are two frame *regimes*, not one formula.

| regime | rule | families |
|---|---|---|
| **structural** — the frame is a carved object | `3.5px + 0.07 × height`, min 3.5px | citybuilder5, gameui2, gameui6, gameui8 |
| **hairline** — the frame is a drawn line | **constant 1–3px regardless of size** | rpgui1, racing4, gameui7's inner keyline |

The kit needs `FrameMode: Structural | Hairline`, not one formula with tuned constants.

**Also: the plate is darker than the background** (0.15 vs 0.18), exactly as `racing4`
measured. Premium dark UIs recede rather than raise.

## Widget 1 — `FieldPlate` (×5 variants)

Rectangles with **notched / cut corners** and a gold hairline, in several fills: brown,
dark navy, warm brown. These are the sheet's slot/field/value containers.

The **notched corner** is the identity of this family — every rectangle has its corners cut
or stepped, never rounded and never square.

## Widget 2 — `RibbonBanner` (purple, green)

Wide ribbon with gold trim and **swallowtail ends**, plus a gold ornament centred on its
top edge. Two colourways on the sheet — the hue is the only difference, geometry identical.
Confirms the neutral-geometry / palette-on-colour model yet again.

## Widget 3 — `GoldBanner`

Cream/gold banner with a **circular gem ornament overhanging its top-centre** and a
diamond-and-wings ornament **hanging below its bottom edge**. Attachments on both edges of
one host — the folder's most decorated title plate.

## Widget 4 — `DividerOrnament`

A gold rule with a central diamond and small wings. A divider that is a *shape*, not a
line. Third family to replace a border/rule with ornament (rpg1, rpg2, here).

## Widget 5 — `CornerBracketPanel`

A large frame with a light grey fill and **small square gold corner marks**. Same corner
bracket device as `racing4` — one in a dark technical skin, one in a gold fantasy skin.
The device is skin-independent; only the mark's art changes.

## Widget 6 — `CircularFrame` set (×5)

| variant | anatomy |
|---|---|
| plain | gold ring + dark well |
| with satellite | a **small circle attached to the ring's lower-right**, overlapping it |
| large double-ring | two concentric rings with a wider gap |
| **linked pair** | two frames joined by an **arrow between them** |

The linked pair is a **progression/upgrade path primitive** — the skill-tree connector
drawn as part of the widget rather than as a separate line. `SkillTree` in the catalogue
has connectors; this shows them as a first-class two-node widget.

## Widget 7 — `ProgressBar` set

Scanned V at x=250 on the gold bar.

| variant | measured / observed |
|---|---|
| gold bar | fill **9px**, gradient `#F4D3A5` L=0.80 → `#BCA14D` L=0.52 = **0.65 ×** top-to-bottom |
| bar height | ~14px total |
| **blue gem bar** | pointed ends, bright cyan-blue, reads as a faceted crystal |
| empty track | pointed ends with gold notched edges |
| **segmented pips** | four small gold chunks — seventh segmented-progress reference |

The gold fill's **vertical gradient at 0.65 ×** is what makes a flat bar read as metal. One
gradient, no bevel, no highlight line.

## Widget 8 — `NamedStatBar`

A circular emblem at the left **overhanging the bar**, `Name` label above the bar, a
**segmented blue fill**, and a value (`88`) below. Three text positions around one bar —
label above, value below, emblem left.

Contrast `gameui8`, which centred the value **on** the fill. Both are valid; the choice
depends on whether the bar is thick enough to hold text.

## Widget 9 — `EmblemSet`

Ten gold pictorial emblems (hammer, eye, village, rune, sword+shield, bull, deer, flag,
person, spiral) — faction/category marks. Flat gold, no plate, no container.

## Widget 10 — `GlyphSet`

Small gold UI glyphs: lock, target, `«`, `»`, `!`, scroll, triangles, diamonds, circles,
arrows, magnifier, `?`, note, hand cursor. Note the **`«` `»` double chevrons** — the pager
idiom of this family.

## Widget 11 — `ItemIcon` set

Painted objects (pouch, scroll, mortar, spellbook). Fully rendered art, unlike the flat
gold glyphs — the sheet **separates painted content icons from flat UI glyphs**, and only
the glyphs are recolourable.

That separation is worth adopting: a kit's icon system should distinguish
**tintable UI glyphs** from **fixed painted assets**.

## Widget 12 — `DialogueScroll` (×2, bottom)

Long horizontal parchment with **rolled ends**, dark wood end-caps, and a **purple ribbon
tab overhanging the top-left**. Confirms `gameui1`'s ScrollPanel with a much higher finish.

## Widget 13 — `CardSlotPanel`

Cream panel with gold corner marks holding **two dark rounded rectangles** — card
slots/backs. The only light panel on the sheet.

## Widget 14 — `RadialCluster`

Overlapping circular frames around a **glowing orange core** — a constellation/skill
cluster. Glow is used as a *state* (active node) rather than as decoration.

## Widget 15 — `CornerFiligree`

Ornate gold corners on the sheet itself. Recorded because it is the family's framing
device at screen scale: the *screen* gets corners, not every panel.

---

## Cross-widget rules

1. **Two frame regimes: structural (ratio) and hairline (constant 1–3px).** The kit needs a
   mode flag, not one formula.
2. **Premium dark plates are darker than their background** — second measurement
   (racing4).
3. **Notched corners** are this family's silhouette; never rounded, never square.
4. **Ornament replaces rules and borders** — third family to do so.
5. **Metal reads from one vertical gradient at ~0.65 ×**, no bevel.
6. **Corner brackets are skin-independent** — the same device in gold fantasy and dark
   tech.
7. **Separate tintable UI glyphs from fixed painted icons.**
8. **Glow marks an active node** in a cluster.

## Actions

- [ ] Add `FrameMode: Structural | Hairline` to `KitGeometry`; hairline = constant 1–3px.
- [ ] Add `KitShape.Notched` (cut/stepped corners) — the identity of premium fantasy UI.
- [ ] Metal material = **one vertical gradient at 0.65 ×**; add as a `KitMaterial` preset.
- [ ] Split the icon system into **tintable glyphs** and **painted assets**.
- [ ] `LinkedNodePair` (two circular frames + connector arrow) → catalogue as the skill-tree
      primitive.
- [ ] `NamedStatBar`, `DialogueScroll`, `RadialCluster`, `FieldPlate` → catalogue.
