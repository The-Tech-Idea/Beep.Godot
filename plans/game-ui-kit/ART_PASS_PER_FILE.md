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

## 14 `vecteezy_futuristic-hud-frames-...jpg` — sci-fi border language *(definitive for shooter)*

Eight frames, no text: a pure study of BORDER CONSTRUCTION. The stroke is not a uniform closed
outline — it is a **run list per edge**:

| element | detail |
|---|---|
| weight | **varies along the run** — hairline sections stepping up to thick solid blocks |
| corners | **stepped (staircase, 2–3 steps)** — not a chamfer, not a radius; and **asymmetric**, every one of the eight treats top-left differently from bottom-right |
| breaks | the stroke **stops and restarts**, leaving deliberate gaps |
| accents | **solid filled segments** (a thick bar of cyan) along one edge |
| decoration | **hatched/striped blocks** (diagonal) and **tick-mark runs** (ruler-like) along an edge |
| micro-marks | detached `×××`, `▪▪▪`, tiny triangles and dashes floating outside the stroke |
| interior | very dark translucent teal, a hair lighter than the page |
| shadow | a **dark offset behind the stroke** giving the line depth |

**Consequence for the kit**: shooter's frame is not a corner treatment at all. It needs an
**edge-run generator** — per edge, a list of segments each with weight, fill, gap and ornament.
No StyleBox and no single silhouette can express this.

## 15 `gameui2.png` — cartoon wood kit *(watermarked comp — style reference only)*

| aspect | observation |
|---|---|
| frame | **four separate wood planks overlapping at the corners**, picture-frame style, each with grain, knots and slightly irregular rounded ends; thick dark-brown outline per plank |
| header | a **darker wood plaque overhanging the panel's top edge**, centred |
| shadow | **soft**, under every panel and button |
| corner | rounded and deliberately **irregular** (hand-drawn plank ends) |
| typography | bold rounded caps, white with a thick dark-brown outline + slight shadow |
| texture | **wood grain with knots**, throughout |
| notable | slider = **recessed dark track + amber fill + wood knob carrying a double-arrow glyph**; level node = circular wood button with a **3-star row BELOW** (gold earned / grey not), padlock when locked; item row = plank with an **inset fill bar** |
| suits | platformer, puzzle, rpg-lite, survival |

## 16 `gameui4.png` — flat sticker kit, neutral panel

| aspect | observation |
|---|---|
| frame | white/light-grey rounded rect with a **thick near-black outline of uniform weight**; no bevel |
| header | **darker grey plaque overhanging the top edge**, centred *(3rd sighting of this pattern)* |
| shadow | **hard**, offset down-right, no blur |
| corner | uniform moderate radius |
| typography | bold rounded caps with a thick dark outline, filled green or orange |
| texture | none — flat fills |
| notable | the **panel is neutral (white/grey) and all colour lives in the icons and text** — the "palette goes on ONE element" rule applied at *screen* scale; close `×` straddles the **top-right corner, outside** the panel; meters appear segmented **and** continuous in the same kit; icon cap overhanging the left of a bar |
| suits | puzzle, platformer, cardgame |

## 17 `ui1.png` — casual mobile kit (Brawl-Stars family)

| aspect | observation |
|---|---|
| frame | thick **DARK** outline + **flat saturated fill** + a **discrete lighter top band** (~25 %) — a hard two-tone, *not* a gradient |
| shadow | subtle **hard** shadow, small offset |
| corner | large, uniform |
| typography | heavy rounded sans; near-black on light plates, white on dark with a subtle dark outline |
| texture | none |
| notable | **circular medallion overhanging the LEFT** of a row *(5th sighting)*; **ticket silhouette** — concave semicircular notches cut into opposite edges; **starburst/rosette** badges (`BEST`, `HOT`); **underline** as the tab-selection cue; speech-**tail** bubbles for values; hexagon/octagon currency chips; **glow** on a highlighted reward; rank chip welded at a row's left; disabled = desaturate + lighten |
| suits | platformer, puzzle, cardgame, casual shooter |

## 18 `ui6.png` — diegetic spiral notebook *(hand-drawn reference)*

| aspect | observation |
|---|---|
| frame | **the UI is a physical object**: paper page with torn soft edges, a **metal spiral binding**, paper **tab dividers** (top-rounded only), a pencil and bookmark tucked at the edge |
| slot grid | **hand-drawn pencil lines** — wobbly, variable weight, **overshooting at intersections**. Not a stroked rect |
| shadow | soft under the page and tabs; light contact shadow under each item |
| corner | paper: irregular, soft, torn |
| typography | **handwritten/marker family throughout** — tabs, title, body and counts; graphite brown, title in rust red |
| texture | **paper grain** (fibrous, mottled cream), metal spiral, wood pencil |
| notable | counts written as **annotations** beside the item, not badges; no detail panel at all — the illustration and handwriting *are* the panel |
| suits | survival, rpg, adventure |
| supports | the plan's hand-drawn outline mode, and adds **Handwritten** to the Phase C font roles |

## 19 `rpgui1.png` — ornate gold filigree *(a SECOND rpg register)*

| aspect | observation |
|---|---|
| frame | **gold filigree hairline** with **ornamental corner flourishes** (scrollwork); plates use a **gold double line whose corners and mid-edges flare into points** — a cartouche, not a rectangle |
| banners | ribbon with **draped folded ends** + a **medallion emblem overhanging the top centre** |
| meters | gold-capped bar; a **faceted hexagonal GEM bar** with bevelled ends; a **segmented block bar** with a circular emblem overhanging the left, label above and value below |
| shadow | soft, plus a **warm glow** behind the highlighted emblem |
| corner | ornamental flourishes; cartouche flares |
| typography | gold serif/blackletter display for titles; light sans for small labels |
| texture | subtle **damask** pattern in the page ground; **metallic gold gradient with a specular line** |
| notable | thin gold **ring frames with a satellite badge and a connector node** (skill graph); **scroll with rolled ends** and a ribbon tag overhanging the top-left |
| suits | rpg *(premium)* |
| proves | rpg needs **two themes** — this and the chunky wood/stone of 11 share no construction |

## 20 `skilltree1.png` — mobile talent tree

| aspect | observation |
|---|---|
| frame | rounded-square with a **thick coloured frame** + a **welded footer bar** carrying the state text; footer colour differs from the frame |
| state | **the FRAME encodes state** — gold = maxed, blue = in progress; unreachable nodes fade by **ALPHA**, not desaturation |
| shadow | soft under cards |
| corner | moderate, uniform |
| typography | heavy rounded sans, white with a subtle dark edge |
| texture | **halftone dot** pattern in the dark ground |
| notable | **glowing connector lines** between nodes; bottom banner is a **sheared magenta plate with circuit-line decoration** — a "cyber" ornament language layered over a casual base |
| suits | rpg, strategy, cardgame |

## 21 `survaivleandrpg.png` — cosy storybook journal

| aspect | observation |
|---|---|
| frame | an aged **parchment sheet whose whole silhouette is torn/ragged** — not a rect; **leafy vine sprigs** grow at the corners |
| tabs | parchment tabs sitting **on** the sheet's top edge, thin hand-drawn brown outline, active tab **colour-coded** (green / lilac) |
| section header | centred small-caps serif **flanked by decorative leaf flourishes on both sides** |
| rows | separated by **thin dotted rules**; selected row = green tinted plate with a rounded outline |
| shadow | **soft**, large, warm, under the whole sheet |
| corner | torn/irregular (sheet), moderate rounded (inner plates) |
| typography | **serif throughout** (storybook old-style), dark brown; bold serif titles, small-caps headers |
| texture | **aged paper** — mottled, stained, fibrous |
| notable | **tag pill** at a card's bottom-right; MP cost right-aligned in an accent colour; muted palette (cream, sage, dusty violet, rose) |
| suits | survival, rpg *(cosy)* |

## 22 `vecteezy_stone-game-interface-...jpg` — jungle stone

| aspect | observation |
|---|---|
| frame | **masonry** — the border is built from **individual stone blocks laid around the edge**, not a continuous stroke; every plate has its own **chipped, cracked, non-parallel** silhouette |
| attachments | **foliage** (leaves, ferns, vines) overhanging corners and edges of nearly every element; vines drape downward |
| shadow | soft ambient |
| corner | irregular, chipped, worn round |
| typography | **ENGRAVED** — grey caps with a light bottom edge and dark top edge (debossed), **no outline**. A distinct treatment from outlined or plain text |
| texture | **stone** (speckled, cracked, mottled), flat cartoon **foliage**, vine/rope |
| notable | icons are **carved into** the button rather than drawn on it; meter = a **carved channel/trough** with a bright fill |
| suits | survival, citybuilder, rpg (ruins/jungle) |

## 23 `settings1.png` — parchment settings dialog *(directly relevant to our settings screen)*

| aspect | observation |
|---|---|
| frame | **two layers**: a thick dark-brown rectangular border with a **torn parchment insert** whose ragged edge lets the frame show through irregularly |
| title | **outside and above** the panel entirely, white serif with a shadow |
| close | small `×` box **outside the top-right corner** |
| rows | right-aligned **label** : left-aligned **control**, on a consistent label column |
| arrow selector | tan value plate flanked by **bare solid triangles** (◀ ▶) **outside** the plate — no button chrome at all |
| slider | **torn-edged brown track** with a dark vertical knob; no fill/remainder distinction |
| toggle pair | two adjacent rounded rects, **selected = lighter**, unselected = darker |
| shadow | soft under the dialog |
| typography | **serif throughout** (slab/old-style), dark brown on parchment |
| texture | parchment; wood/leather brown for controls |
| suits | rpg, survival |

## 24 `ui8.png` — full screen of 02's game *(collapsible-panel reference)*

| aspect | observation |
|---|---|
| collapse handle | a small dark tab **outside the panel, centred on its top (moving) edge**, carrying a high-contrast white chevron — confirms the measured CollapsiblePanel spec |
| cards | portrait card with a dark name band at the top and a **star badge straddling the bottom-left edge** |
| carousel | chevrons **and** skip-to-end (`|◀ ▶|`) controls at both ends of the tray |
| rails | left/right vertical stacks of square icon buttons with **red count badges straddling the corner** |
| notable | **radial burst/glow behind** a promo button; a small flag overhanging its top; the primary action is visually the heaviest plate on screen |
| suits | citybuilder, strategy |

## 25 `vecteezy_medieval-royal-knight-...jpg` — stone + heraldry

| aspect | observation |
|---|---|
| frame | chipped stone slab, **plus a THEMATIC ATTACHMENT SET bolted on per screen**: crossed spears + crimson drape (Level Up), **crown** (Victory), **knight helm + maces** (Restart), **gear + shields** (Settings), striped **awning** (Shop), swords flanking (Menu) |
| header | separate stone bar **overhanging the top edge**, centred |
| shadow | soft |
| corner | chipped, irregular |
| typography | bold rounded caps, white + dark outline for titles; **green for interactive text and numbers** — a consistent accent role |
| texture | stone, **cloth** (crimson + gold trim), metal, wood |
| notable | circular **blue** icon buttons are the only saturated UI colour besides the drape; slider knob is stone carrying `◀▶` glyphs; meter = stone trough with a pill fill |
| suits | rpg, strategy, survival |
| key idea | **the ornament identifies the screen** — victory/defeat/settings are told apart by their attachment set, not by colour |

## 26 `vecteezy_list-of-mobile-games-...jpg` — warm cream/coral casual

| aspect | observation |
|---|---|
| frame | **double border with a visible gap** — an outer coral band and an inner cream panel |
| header | plaque overhanging the top in a **contrasting hue** (violet on coral), not a tint of the frame |
| shadow | soft under panels; **hard offset 3D extrude** on the display type |
| corner | large uniform radius; **circles** for icon buttons |
| typography | rounded bold sans; display type has a hard offset duplicate for a 3D look |
| texture | none; subtle inner glow on the cream panels |
| notable | cost pill **welded at a card's bottom**; vertical slider with **circular arrow buttons at both ends**; star row below unlocked level cells |
| suits | puzzle, cardgame, platformer |

## 27 `gameui7.png` — glossy blue cartoon *(watermarked comp)*

| aspect | observation |
|---|---|
| frame | thick **dark navy outline**, cyan fill, a strong **top gloss with a CURVED lower boundary** (glass, not a linear gradient); inner recessed field in darker blue with its own outline |
| header | an **ELLIPSE plaque overhanging the top edge**, centred, often with an **icon overhanging the plaque's own left end** |
| shadow | soft |
| corner | large radius; true ellipse for the header |
| typography | bold caps, white with a dark outline; values in yellow |
| texture | none; glossy |
| notable | **dog-ear** slots (bottom-right corner folded); difficulty selection by **fill colour** (green/white/orange); rosette ribbon badges |
| suits | puzzle, platformer, cardgame |

## 28 `vecteezy_game-interface-jungle-...jpg` — jungle wood/tiki

| aspect | observation |
|---|---|
| frame | **wood plank frame with an inset sand/stucco panel**; vines and leaves overhang every corner |
| header | wood bar overhanging the top, rounded ends |
| shadow | soft |
| corner | rounded, irregular plank ends |
| typography | bold rounded caps, white with a dark brown outline; brown numerals on sand |
| texture | **wood grain**, **sand/stucco**, foliage |
| notable | menu items are **wood capsules** stacked; **segmented meter with `−`/`+` circular end buttons** *(matches the galaxy kit)*; saturated red/blue circular icon buttons against wood; tiki-mask ornament |
| suits | survival, platformer, citybuilder |

---

## Coverage after this pass

**28 of 60 read in depth.** Remaining 32, still listed by genre in the section above.

### Axes added by files 14–28, beyond the eight already in the plan

| new axis | evidence |
|---|---|
| **Frames are EDGE RUNS, not borders** — per edge, a list of segments with weight, fill, gap, hatch, ticks and micro-marks; corners stepped and **asymmetric** | 14 *(definitive)* |
| **Frame construction families**: masonry (blocks), plank (4 overlapping planks), double-border-with-gap, frame + torn insert | 22 / 15·28 / 26 / 23 |
| **Attachment SETS identify the screen** — crown = victory, helm = restart, gear = settings | 25 |
| **Engraved / debossed text** (light below, dark above) as an alternative to outlined or plain | 22 |
| **Header plaque overhanging the top edge** is near-universal in the cartoon families, and its shape varies: bar, ellipse, contrasting-hue plate | 15·16·25·26·27·28 |
| **Two-tone plate** — flat fill + a discrete lighter top band, *not* a gradient; and gloss with a **curved** boundary | 17 / 27 |
| **State encoded by the FRAME**, and unreachable by **alpha** rather than desaturation | 20 |
| **Ticket / dog-ear / rosette** silhouettes | 17 / 27 / 17·27 |
| **Hand-drawn grid** with overshoot, and **handwritten** type | 18 |
