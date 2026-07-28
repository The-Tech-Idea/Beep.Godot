# Widget catalogue derived from `Example_Art/`

Every entry below was read off the reference pictures, not invented. The **seen in** column is
the evidence; anything not traceable to a picture is not in this list.

Sources examined: `ui1.png` `ui2.png` (Layer Lab casual GUI), `store.png` (farm/casual shop),
`skilltree1.png` (talent tree), `Upgrades.png` (Kingdom-Rush-style upgrades), `rpgui.png`
(painted fantasy asset sheet).

---

## A. Universal — appears in nearly every picture

| widget | anatomy | seen in |
|---|---|---|
| **CurrencyBar** | capsule + icon **overhanging the left cap** + value + optional `+` button at the right cap | ui1, ui2, store, skilltree1, Upgrades |
| **TabStrip** | row of tabs; selected is raised/brighter; optional **corner flash badge** ("NEW!") and notification dot | store (7 tabs), ui1, ui2 |
| **NodeCard** | icon panel + **footer label bar welded underneath** carrying state text (`MAX`, `2/3`, `BUY`, price) | skilltree1, Upgrades, store |
| **ProgressBar** | rounded track + fill + **icon cap on the left** + fraction text centred | ui1, ui2, skilltree1 (EXP), store |
| **PrimaryAction** | large button, often with a **separate cost plaque beside or above it** | ui2 (`Select` + `2,500`), store (`BUY` + price) |
| **PanelBanner** | title plate **overhanging the panel's top edge** | store (`STORE`), Upgrades (`UPGRADES`), rpgui |
| **CloseButton** | X or back-arrow pinned to a panel corner, **straddling the frame** | store, ui2, rpgui |

## B. Structured views

| widget | anatomy | seen in |
|---|---|---|
| **SkillTree** | NodeCards in tiers + **connector lines** + locked variant (desaturated + lock glyph) | skilltree1, Upgrades |
| **ItemGrid** | card = title / art / description / **price+BUY footer**; paged with chevron arrows | store |
| **StatList** | rows of icon + label + value, each in its own dark capsule | ui2 |
| **MissionRow** | icon badge **overhanging the row's left edge** + title + progress + action button | ui1 |
| **PlayerRow** | rank chip + avatar + name + trophy icon + score, in a coloured bar | ui1 |
| **PlayerCard** | avatar frame + name + level + XP bar, top-left cluster | skilltree1, ui1 |

## C. Small parts

| widget | anatomy | seen in |
|---|---|---|
| **RarityChip** | small coloured plate carrying a tier word (`EPIC`) | ui2 |
| **FlashBadge** | starburst/scalloped disc with short text (`NEW!` `HOT` `BEST` `x15`) | ui1, store |
| **CountBubble** | speech bubble with a tail carrying a number | ui1 |
| **NotificationDot** | small filled circle, optionally numbered, on a widget corner | ui1, ui2 |
| **LockOverlay** | desaturated widget + padlock + requirement text (`Lv15`) | ui2, skilltree1 |
| **PagerArrow** | chevron button at a list's left/right edge | store |
| **HintTooltip** | rounded panel **with a tail** | ui1 |
| **Slider** | rounded track + filled portion + round knob | ui1 |

---

## The two style families in this folder

They are not the same kit and must not be averaged into one look.

**Casual/mobile** — `ui1`, `ui2`, `skilltree1`, `store`
- uniform **thick dark outline** (3–4px) on every element
- **flat saturated fill** plus one lighter band across the top; no gradients
- **large corner radius** (~0.25–0.35 of height)
- **hard drop shadow** offset straight down
- icon badges **overhang** the left edge of rows and the caps of bars
- **Reproducible procedurally.** This is the family the kit should target first.

**Painted fantasy** — `rpgui`, `Upgrades`
- frame around a **separate inner plate**, carved bevel, metal rivets
- **small** corner radius (~0.07)
- material is hand-painted wood/metal/parchment
- **Not reproducible procedurally.** Needs sliced 9-patch art cut from the sheets.

**Consequence:** the earlier §4.2a measurements were taken from `rpgui.png` — the painted family
— and applied to a procedural renderer. That is why every attempt looked wrong and got worse
with each parameter change. Procedural work should target the casual family; the painted genres
need their art sliced from `rpgui.png` instead.

---

## Build order (derived from frequency above, not from preference)

1. **CurrencyBar, TabStrip, ProgressBar, PrimaryAction** — in every picture, so they carry the
   most screens per unit of work.
2. **NodeCard + footer bar** — the single most repeated compound element across three pictures.
3. **PanelBanner, CloseButton** — the two attachments that prove overhang.
4. Then the structured views in B, which are compositions of 1–3.

---

## D. Simple / form controls — from `settings1.png`

Games do **not** use the desktop form vocabulary. This picture has no dropdown, no checkbox and
no radio button anywhere, yet it is a full settings screen. What it uses instead:

| widget | anatomy | replaces |
|---|---|---|
| **ArrowSelector** | `◄ value ►` — left/right arrow buttons flanking a value plate. Cycles options in place | dropdown / OptionButton |
| **SegmentedIconGroup** | two or more icon buttons, exactly one lit — keyboard vs gamepad here | radio group / CheckBox |
| **Slider** | textured track + **vertical bar knob**, not a round one | HSlider |
| **LabelRow** | right-aligned label, control on the right, consistent baseline | form row |
| **PlainButton** | flat plaque, no badge, no ornament (`Okay`) | Button |
| **TornPanel** | parchment with an **irregular torn edge**, not a clean rectangle | PanelContainer |
| **CornerClose** | X pinned outside the panel's top-right, overhanging the frame | window close |

**This invalidates an assumption in the existing theme system**, which styles `OptionButton`,
`CheckBox`, `CheckButton` and `PopupMenu` because Godot provides them. No reference picture in
this folder uses any of them. A game settings screen is arrow selectors and segmented icon
groups; a dropdown reads as an application immediately.

## E. Circular / radial — under-covered in A–C

Round elements are everywhere in the references and the first catalogue draft nearly missed them.

| widget | anatomy | seen in |
|---|---|---|
| **CircleIconButton** | round bezel + icon; the standard secondary action | rpgui (10 medallions), ui1, store, skilltree1 (gear) |
| **AvatarFrame** | round or shield portrait frame + optional level badge **overhanging its rim** | rpgui, skilltree1, ui1, ui2 |
| **RadialMeter** | ring track + swept fill — cooldowns, timers, capacity | rpgui (blue arc), ui2 |
| **StarburstBadge** | scalloped disc carrying short text (`NEW!` `HOT` `x15`) | ui1, store |
| **NotificationDot** | small filled circle, optionally numbered, on a widget corner | ui1, ui2 |
| **CountBubble** | round/rounded bubble **with a tail** carrying a number | ui1 |
| **RoundKnob** | circular slider handle (casual family; the painted family uses a bar) | ui1 |
| **GemSlot** | polygonal socket — pentagon/hexagon — holding a rune or gem | ui2 (runes), Upgrades |

**Shape note:** the casual family uses true circles; the painted family uses **octagons and
pentagons** that read as circles at small size. `KitShape` already has both, so this is a
per-genre choice, not new geometry.

---

## Coverage check

Against this folder the catalogue now covers: compound game widgets (A–C), simple form controls
(D) and circular/radial (E). **Not yet traced to any picture here** — and therefore not in the
kit until a reference is supplied: text input fields, scrollbars/scroll views, tables with
sortable headers, tree/outline views, date or numeric spinners, multi-line text areas.

Those are application patterns. If a screen in this project needs one, it needs a game reference
first, or it will drag the desktop vocabulary back in.

---

## F. From `ui5.png` (large casual GUI sheet) — new families

### F.1 Panel HANGERS — a panel attaches to the screen

The biggest addition. In this sheet a panel is rarely just placed; it is **hung**, and the hanger
is a distinct element crossing the panel's top edge:

| hanger | anatomy |
|---|---|
| **ChainHang** | two chains from the top edge upward, panel swings from them |
| **RopeHang** | rope/cord, often with a knot |
| **NailPin** | nail or screw head at each top corner |
| **TapeCorner** | masking-tape strips stuck across the corners at an angle |
| **ScrollRoll** | parchment with rolled ends top and bottom |
| **VineFrame** | leaves/vines growing around the frame |

These are `KitAttach`es above the host with `Overhang > 0.5`, which is exactly the primitive
already built — nothing new is needed structurally, only the shapes.

### F.2 Widgets not previously catalogued

| widget | anatomy | note |
|---|---|---|
| **OnOffSwitch** | two-segment plate, `ON` lit / `OFF` dim | **this is the game checkbox** — confirms section D: no real checkbox anywhere |
| **StarRating** | 1–3 stars, filled vs empty | on rows, level tiles and completion panels |
| **LevelNodeGrid** | numbered round/square nodes, **locked variant with padlock**, path between them | level select |
| **BookSpread** | two-page open-book panel with side tabs | inventory/journal |
| **SpinWheel** | segmented circular wheel with a pointer | reward spin |
| **RewardSlotRow** | row of empty item boxes filled on claim | level complete |
| **MedalRosette** | circular medal with **ribbon tails below** | awards |
| **SegmentedBar** | progress drawn as discrete chunks, not a continuous fill | |
| **LoadingIndicator** | text + animated dots or a chain motif | |
| **ItemCardWithFooter** | art + name + **`SELECT` footer button** | shop; same footer pattern as NodeCard |

### F.3 Confirmations

- **NodeCard's welded footer bar** appears again on shop cards and level tiles — now seen in five
  separate pictures. It is the most repeated compound element in the whole folder.
- **Panels come in material variants** — wood plank, parchment scroll, stone slab, book page,
  metal frame — carrying the SAME layout. Material is a skin over one panel widget, which is what
  `KitMaterial` is for.
- Buttons appear in a **stacked colour set** (blue/green/red/yellow/orange) for one screen, i.e.
  role-coloured rather than one accent — matches the `UiSurface.Role` model already in place.

---

## Review status

Reviewed in detail: `ui1` `ui2` `ui5` `store` `skilltree1` `Upgrades` `rpgui` `settings1`.
**Not yet reviewed: `ui3` `ui6` `ui7` `ui8`** plus the older `gameui1-9`, `citybuilder1-5`,
`racing1-4`, `rpg1-3`, `rpgui1-3`, `skilltree3/4`, `store1`, `survaivleandrpg*`.

The catalogue is additive — reviewing those can add widgets but should not remove any, since
every entry is already evidenced. Highest value next: `ui6-8` (newest, same casual family),
then `citybuilder*` and `racing*`, which are the two genres with no dedicated reference read yet.
