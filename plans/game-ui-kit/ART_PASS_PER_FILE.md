# Example_Art — per-file pattern pass

One row per image. Read **file by file**, recording the things the kit currently gets wrong:
**frame/border construction, layer stack, shadow, corner treatment, typography, texture/material,
and how the genre is signalled.**

Why this document exists: the kit assigns **one register per genre** (`Carved` / `Casual` /
`Technical`, mapped 1:1 from `KitCore.ShapeForGenre`). The first five files already break that —
citybuilder alone appears in **five mutually exclusive registers**. A genre is not a look; a
*theme* is. Recording per file first, then synthesising, so the conclusions are traceable to an
image rather than asserted.

Legend for **Shadow**: `hard` = opaque, offset, no blur · `soft` = large-radius ambient ·
`none` = separation by outline or value alone · `glow` = coloured outer glow.

---

## 01 `Upgrades.png` — tower-defense upgrade tree (Kingdom Rush)

| aspect | observation |
|---|---|
| frame | **double**: outer dark wood border + inner lighter plank panel, with **metal corner brackets (rivetted)** at all four corners |
| banner | gold **ribbon with folded ends overhanging BOTH side edges**, crossing the top border |
| layers (icon btn) | gold outer frame → darker gold inner bevel → **coloured disc** → icon → hard shadow |
| shadow | **hard**, offset down, on every icon button and control |
| corner | square panel; corner *brackets* rather than a radius |
| typography | slab-serif / western display, **ALL CAPS**, dark outline on banner; tooltip body in **italic amber** |
| texture | vertical **wood plank** grain (panel), brushed **metal** (brackets, plaques), **gold** (frames) |
| locked | full **desaturation to grey** + star-cost badge |
| tooltip | pure **black** plate, no frame, cream title + amber italic body |
| other | vertical connector lines between icons = upgrade path; **shield-shaped** plaques hanging below each column |
| suits | citybuilder, strategy, rpg (upgrade trees) |

## 02 `citybuilder1.png` — casual browser citybuilder HUD

| aspect | observation |
|---|---|
| frame | **no panels at all** — HUD floats directly on the world |
| resource pill | dark capsule + **icon disc overhanging the LEFT** + blue `+` welded flush **right** + internal gold fill + `max:` caption **above** |
| layers | outline → body → internal fill → icon cap → welded button |
| shadow | **soft** ambient under each floating element |
| corner | full pill radius on bars; ~20 % on the `+` squares |
| typography | bold rounded sans, **white with a 1–2px dark outline** (must survive any world colour) |
| texture | flat colour + subtle vertical gradient; no material |
| other | circular status buttons with the count **inside**, below the icon; thin light rim |
| suits | citybuilder |

## 03 `citybuilder2.png` — flat translucent mobile (Boom Beach)

| aspect | observation |
|---|---|
| frame | none; **flat translucent** plates |
| layers | single translucent plate. **No bevel, no gloss, no shadow** |
| shadow | **none** — translucency does the layering |
| corner | ~10 % uniform |
| typography | plain bold sans, white, **no outline, no shadow** — the exact opposite of file 02 in the same genre |
| texture | none |
| notable | **dashed white border** on world-space labels (`Full!`); fill-as-plate (the meter *is* the readout's background); left rail of uniform blue squares |
| suits | citybuilder |

## 04 `citybuilder3.png` — papery minimal (Before We Leave)

| aspect | observation |
|---|---|
| frame | white/cream plates, **no outline whatsoever** |
| layers | plate + soft shadow. That is all |
| shadow | **soft**, large radius, low opacity — the *only* separator |
| corner | **full pill** for bars, moderate for cards; info card is **top-rounded only** |
| typography | **light** weight sans, **letter-spaced small caps** for captions (`LIFESTYLE`, `COST:`), centred body |
| texture | none; muted earth palette (sand, olive, dusty teal, terracotta), low saturation |
| notable | **pill-as-toolbar** (several readouts grouped in one pill); **circular medallion overhanging the TOP** of a card, centred; tab strip sitting **above** its tray |
| suits | citybuilder, puzzle (calm/minimal) |

## 05 `citybuilder4.png` — dark side-drawer (violet)

| aspect | observation |
|---|---|
| frame | 1px **low-contrast light outline** on cards |
| layers | **monochrome value steps** in one hue: drawer < card < selected. No bevel, no gloss |
| shadow | minimal to none — **lightness alone** layers it |
| corner | moderate, uniform |
| typography | bold rounded sans, white titles, muted secondary numbers, **cost numbers turn warning-coloured when unaffordable** |
| texture | none |
| notable | **full-height side drawer** as the screen archetype (not a centred dialog); **header band inside each card**; world callout = **circle on a stem**; selected rail cell = lighter fill + border |
| suits | citybuilder, strategy, cardgame |

## 06 `citybuilder5.png` — carved stone (Township)

| aspect | observation |
|---|---|
| frame | **4-band carved edge**: bright rim → mid bezel → dark inner shadow → plate face *(the measurement the kit's Carved register is built from)* |
| layers | rim → bezel → shadow → face → icon → caps label → hard shadow |
| shadow | **hard**, offset down, strong, on every plate |
| corner | rounded-square plates; **octagons** on the left rail |
| typography | **bold condensed** sans, ALL CAPS, white with a **strong dark outline** + slight shadow |
| texture | **stone** (speckled, mottled); gold/metal on badges |
| notable | **cyan glow outline = selected**; **star-shaped** level badge overlapping two stacked progress bars; **chiselled/irregular** slab outline on resource bars (not a clean rounded rect); **awning** attachment on the shop panel; **notched flag** promo banner; icon-above-label inside the button |
| suits | citybuilder, strategy |

---

### Already contradicted by files 01–06

- **One register per genre is wrong.** citybuilder appears as: cartoon-outlined (02), flat
  translucent (03), papery minimal (04), monochrome drawer (05), carved stone (06). All five are
  the same *genre*.
- **Typography is not a genre property.** Files 02 and 03 are the same genre and take opposite
  rules: outlined-white-bold vs plain-bold-no-outline. File 04 is light-weight letter-spaced caps.
- **Shadow is a register property the kit does not have at all** — `hard`, `soft`, `none` and
  `glow` all appear, and `KitLayerKind` has no `Shadow` member.
- **Frames are constructed, not just bordered**: corner brackets (01), double frame (01),
  top-rounded-only (04), dashed (03), chiselled-irregular (06).

## 07 `racing1.png` — photoreal sim, typography-only HUD (Forza)

| aspect | observation |
|---|---|
| frame | **none anywhere**. No panels, no borders |
| layers | translucent row / hairline only |
| shadow | **none** — an edge **scrim gradient** lifts text off the world instead |
| corner | **0 radius**, square rows |
| typography | **thin/light**, condensed, **letter-spaced caps** for labels + a **large light numeral** for the value. Typography *is* the widget |
| texture | none |
| notable | leaderboard = ultra-thin rows, player row **accent-filled**; **thin ring** as countdown; **tick-marked arc** as speedo |
| suits | racing |

## 08 `racing2.png` — mobile racing, sheared plates

| aspect | observation |
|---|---|
| frame | dark translucent plates with **SHEARED (skewed) ends** — the border shape is a parallelogram, not a radius |
| layers | plate + optional internal fill |
| shadow | none / very soft |
| corner | **shear**, no radius |
| typography | bold condensed ALL CAPS, oblique feel; **value colour = state** (white 37 %, red 5/12) |
| texture | none |
| notable | banner flanked by **decorative skewed slash pairs** (magenta); circular gauge with **tick ring + needle** |
| suits | racing |

## 09 `racing3.png` — dark technical tuning screen

| aspect | observation |
|---|---|
| frame | very dark translucent + **1px light hairline**, ~6px radius |
| layers | hairline → plate. No bevel, no gloss |
| shadow | **none** — hairlines and value steps |
| corner | small radius |
| typography | **light/regular**, small, white + grey. No caps emphasis, no outline |
| texture | none |
| notable | **radar/pentagon chart** *(the missing primitive, 2nd sighting)*; **two different selected renderers on one screen** — accent **fill** for icon cells, accent **border** for carousel cells; carousel with side chevrons; **single-accent discipline** (amber only) |
| suits | racing, shooter |

## 10 `racing4.png` — hairline + corner ticks (Asphalt)

| aspect | observation |
|---|---|
| frame | dark plate, **1px hairline**, plus **small square corner registration ticks** at all four corners |
| layers | hairline → plate → corner ticks |
| shadow | **none** |
| corner | **0 radius (sharp)**, decorated with ticks / **L-shaped corner brackets** on carousel cells (partial border, not a full one) |
| typography | **bold condensed ALL CAPS** throughout, white |
| texture | none |
| notable | **chevron stack (»»)** as an upgrade-level readout, green when upgraded; sheared white wedge in the top bar for Back; **two accents with distinct roles** (lime = go/owned, magenta = navigation); selected = full lime border |
| suits | racing, shooter |

## 11 `rpgui.png` — full fantasy RPG kit *(richest single file)*

| aspect | observation |
|---|---|
| frame | several, all constructed: wood plank with **corner rivets/screws**; recessed field with a **gold hairline**; **gold DOUBLE-line** border (thick outer + thin inner) on a chamfered/octagonal plaque; ornate **metal corner brackets** on a scroll panel |
| meters | horizontal bars with **ornate metal END CAPS**, a different cap per tier (spiked wheel / star-cross / arrow point). Glossy fill + bright top highlight + segment ticks |
| banners | **hanging cloth on a metal rod with finials** — purple with a **torn ragged bottom**, blue with a **pointed chevron bottom**; long red **ribbon with folded ends**; small pennant on a pole |
| badges | 1st/2nd/3rd **heraldic shields** with wings, laurels and ribbon scrolls |
| slots | rounded-square **rarity colour swatches** (grey/green/blue/purple/orange) with a dark rim + inner gloss |
| shadow | **soft** drop shadow under every element |
| corner | mixed by widget: chamfer/octagon (plaques), rounded (slots), square + rivets (wood bars) |
| typography | **SERIF** (Cinzel/Trajan-like), gold or cream, dark outline; **colour-coded text by resource** — silver / gold / gem each its own colour |
| texture | tileable **brick/stone wall** (dark red-brown with ember sparks) and **stitched leather** (light stitch border) shipped as swatches |
| notable | blood-splatter decal; ember particles over stone; circular dark stone ability icons with a light rim |
| suits | rpg *(definitive)*, survival |

## 12 `vecteezy_galaxy-space-...jpg` — casual space/arcade kit *(purest Casual reference)*

| aspect | observation |
|---|---|
| frame | rounded rect with a **thick LIGHT (white) outer stroke** and a subtly **irregular, hand-drawn wobble** to the edge; slight pillow/barrel distortion on panels |
| layers | light outer stroke → glossy body → **bright top gloss band** → darker bottom |
| shadow | **soft** drop shadow |
| corner | large radius, deliberately **non-uniform** |
| typography | **bold rounded display**, ALL CAPS, white with a dark outline, often slightly extruded/3D. Playful |
| texture | none — glossy gradients only |
| notable | **colour = function** on actions (green confirm/BUY/ON, red cancel/OFF, purple help, amber menu), all saturated; **segmented meters with `+`/`−` end buttons**; thin meters with a **circular icon cap overhanging the LEFT**; level node = rock platform + **star row above** (gold earned / dark unearned) + % below |
| suits | puzzle, platformer, cardgame, arcade shooter |
| **contradicts the kit** | the `Casual` register hardcodes a thick **DARK** outline. Here it is thick and **LIGHT**. Outline *polarity* is a theme property, not a register constant |

## 13 `store.png` — farm/island store, wood + parchment

| aspect | observation |
|---|---|
| frame | thick **wood log/branch** border with knots and an **irregular bark silhouette** — the frame is an illustration, not a rect; foliage sprigs at the top corners |
| item card | **parchment with non-parallel, torn edges** *(confirms `Torn`)*; title caps → art → small-caps body → cost row → **welded gold BUY footer** |
| shadow | **soft**, under cards and buttons |
| corner | organic/irregular (frame), torn (cards) |
| typography | bold rounded caps; dark brown on parchment for body; title white with a thick dark-teal outline |
| texture | **wood/bark** (frame), **parchment/paper** (cards), gold (buttons) |
| notable | **"NEW!" flag overhanging a tab's top-left corner**; tab strip in wood, active = colour swap + star; nav arrows in wood plates **overhanging the frame edge** |
| suits | survival, rpg, citybuilder |

---

### Coverage

**13 of 60 read in depth.** Remaining, in priority order for the next pass:

- **shooter / sci-fi**: `hud-frames-futuristic-*`, `futuristic-hud-frames-*`, `gameui2`, `gameui3`, `gameui7`
- **rpg**: `rpg1–3`, `rpgui1–3`, `medieval-royal-knight-*`, `game-ui-menu-interface-scrolls-*`
- **survival**: `survaivleandrpg`, `survaivleandrpg1–2`, `store1`, `game-interface-jungle-*`
- **strategy**: `skilltree`, `skilltree1`, `skilltree3`, `skilltree4`
- **casual / puzzle / platformer**: `ui1–3`, `ui5–9`, `gameui1`, `gameui4–6`, `gameui8–9`,
  `casual-game-ui-menu-popups-*`, `list-of-mobile-games-*`, `action-game-ui-kit-*`
- **materials**: `uitexturs` (9 tiles, already measured), `uiwood`, `stone-game-interface-*`,
  `wooden-*` ×3, `square-wooden-frames-*`, `game-buttons-of-wooden-and-gold-*`
- **settings reference**: `settings1`
