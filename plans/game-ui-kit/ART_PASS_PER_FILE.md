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

## 29 `ui3.png` — blue-gradient casual equip screen *(all three empty states)*

| aspect | observation |
|---|---|
| frame | rounded rects on a blue **radial gradient**; panels a lighter blue with a subtle 1–2px **light** rim |
| empty states | all three on one screen: **blank** (darker cell), **invite `+`** (equip slot), **locked** (padlock **plus `Lv.11` in words**) — confirms the settled "locked states state their requirement" rule |
| selection | **brighter/white rim** on the chosen slot; active tab = **light filled** rounded rect |
| shadow | soft to none |
| corner | moderate-large, uniform |
| typography | rounded bold sans, white, no outline |
| texture | none — smooth gradients |
| suits | platformer, puzzle, cardgame, rpg-lite |

## 30 `ui5.png` — **the material acceptance test, confirmed and enlarged**

The plan cited this as "one dialog geometry in ~10 materials". Counted here: **twelve**, all sharing
the same layout (header plaque overhanging the top → body → welded button row):

wood planks · parchment scroll with rolled ends · chipped stone · stone wrapped in **ivy** ·
**open book** spread with a spine · **taped card** (tape strips at the corners) · bone/skull metal ·
**chained** metal (chains hanging below) · **signpost** on wooden posts · red **fabric** banner with
folds · vegetable/carrot themed · **grid notebook paper**

| aspect | observation |
|---|---|
| shadow | soft throughout |
| typography | bold rounded caps with thick dark outlines, often 3D-extruded; a separate **comic display** face for `WON!` / `GAME OVER!` / `PLAY Now!` |
| notable | **torn paper strips** as banners and labels; **tape**, **chains**, **vines**, **posts** and **gears** as frame attachments; radial **spin wheel**; ON/OFF pairs (green/red); segmented star ratings; loading bar made of chain links |
| suits | every casual genre — this is the style-pack argument in one image |

## 31 `rpg1.png` — cartoon knight shop

| aspect | observation |
|---|---|
| frame | **wood plank ground** with **parchment panels** inset, torn edges, thin darker outline, soft shadow |
| tabs | grey rounded rects, active = **amber fill**; a yellow **`!` badge straddling the top-right corner** |
| rows | parchment plate + **icon tile on the left** + caps title + small description + **cost plate on the right** + a **segmented progress bar** beneath the cost |
| disabled | the **cost plate desaturates** when unaffordable |
| shadow | soft |
| corner | moderate; parchment edges irregular |
| typography | bold rounded caps for titles, regular for body; dark brown on parchment |
| texture | wood (ground), parchment (panels) |
| suits | rpg, survival, citybuilder |

## 32 `gameui8.png` / `ui9.png` — cosy RPG kit *(same asset family)*

| aspect | observation |
|---|---|
| frame | parchment/tan panel, dark brown border, **gold L-shaped corner ornaments** on all four corners |
| header | plaque **overhanging the top edge**, in a **contrasting hue per screen** (tan / green / purple / blue) |
| meters | **discrete heart pips** (5 red + 2 dark) for HP; continuous MP/XP bars with a **label chip overhanging the left** |
| currency | dark pill + **icon disc overhanging the left** + value + **green `+` welded flush right** |
| locked | quest row greys out and shows **`???` placeholder text** — not just a padlock |
| tooltip | **dark brown** plate with a gold border; **title colour encodes rarity**; a hairline separator above the stat row; `▼` continue indicator bottom-right |
| shadow | soft |
| typography | rounded sans, dark brown on tan |
| notable | **teardrop map pins** in five colours; pennant flags; heraldic hanging banners with pointed bottoms; ornate cartouche with gold scroll flourishes; slot count badge bottom-right |
| suits | rpg, survival |

## 33 `rpgui2.png` — roguelike deckbuilder, hand-drawn parchment

| aspect | observation |
|---|---|
| frame | parchment with a **hand-drawn dark outline** — wobbly, variable weight, drawn rather than stroked |
| shadow | **none at all** — the hand-drawn outline does 100 % of the separation |
| cards | **coloured header band by card type** (red attack / green defend) + centred caps title + art + hairline rule + body + a **cost badge straddling the bottom-LEFT corner** |
| tooltip | **dark near-black plate with a thin light outline** — the only dark surface in an otherwise parchment UI, i.e. inverted |
| corner | slightly irregular rounded |
| typography | **serif / old-style**, dark brown, caps titles |
| texture | parchment with visible fibre and stains |
| notable | node graph drawn with **wobbly hand-drawn connector lines**; meter value centred **on** the bar |
| suits | cardgame, rpg |

## 34 `vecteezy_game-buttons-of-wooden-and-gold-...jpg` — wood chrome study *(no text)*

| aspect | observation |
|---|---|
| construction | **frame + insert is the universal model here** — every control is a **light wood frame containing something else**: a coloured pill (button), a dark well (slot), a fill (meter). Not "a plate with a border" |
| wells | dark wood inset with an **inverted bevel** (highlight bottom-right, shadow top-left) so it reads as recessed |
| header | light wood **capsule overhanging the top edge**, itself containing a coloured pill |
| close | **circular wood knob** straddling the top-right corner |
| meters | wood trough + coloured pill fill + **circular wood knob** handle; a long bar with **gold milestone stars pinned along it** |
| corner | large radius, slightly irregular/carved |
| shadow | soft, plus internal bevel shading |
| typography | *(none in this sheet)* |
| texture | **wood grain, light and dark**, with knots, grain following the shape |
| notable | bare solid **wood triangle arrows** with no chrome; stepper = capsule with a `+` welded at one end |
| suits | rpg, survival, platformer, citybuilder |

## 35 `vecteezy_action-game-ui-kit-...jpg` — extruded slab *(a FIFTH shadow kind)*

| aspect | observation |
|---|---|
| depth | **EXTRUDE** — a thick dark **bottom face** under the panel and under every button, so each reads as a solid block seen slightly from above. This is not a drop shadow and not a bevel; it is a **side face**, and the kit has no equivalent |
| corner | **asymmetric by design**: top corners rounded, **bottom corners chamfered at 45°** |
| header | a darker band **inside** the panel's top (not overhanging) — the alternative to the overhanging plaque |
| shadow | extrude **plus** a soft ambient underneath |
| typography | heavy rounded display caps in gold/green with a **hard offset extrude** and a dark outline |
| texture | none, but a **faint large watermark pattern** (skulls, bombs) in the background |
| notable | buttons are green face + gold outline + dark green side face; readouts carry a badge overhanging the left with the value tinted to match the icon |
| suits | platformer, shooter-arcade, puzzle |
| **plan impact** | Phase A's shadow kinds become **None / Hard / Soft / Glow / Extrude** |

## 36 `vecteezy_game-ui-menu-interface-scrolls-...jpg` — painted blue wood + pinned paper

| aspect | observation |
|---|---|
| frame | **blue-painted wood planks** with irregular carved edges and notches, holding a **parchment sheet PINNED inside** with small round pins; the paper's corners are torn and curled, and the wood shows at its edges |
| header | text directly on the parchment with a **hairline rule beneath** — no plaque |
| close | **red circle** straddling the top-right corner |
| shadow | soft |
| corner | irregular carved wood; torn/curled paper |
| typography | bold caps, white with a dark outline on blue; dark brown on parchment |
| texture | **painted wood** (grain visible *through* the paint) and parchment |
| notable | meters = blue-wood trough + coloured fill + **icon medallion overhanging the left**; circular icon buttons with a rim highlight; bare blue triangle arrows; recessed dark input fields |
| suits | platformer, puzzle, survival |

## 37 `skilltree4.png` — merge/idle equipment grid

| aspect | observation |
|---|---|
| rarity | **the tile's FILL COLOUR is the rarity** (gold > magenta > blue), with a thick dark outline and a lighter inner border |
| corner slots | **type icon top-left, `Lv.N` top-right** — corner metadata slots on every tile |
| nav | the **active bottom-nav item is a taller gold tile that overhangs the bar upward**, carrying a crown ornament; a red `!` badge on another |
| shadow | subtle hard shadow under tiles |
| corner | moderate, uniform |
| typography | heavy rounded sans, white + dark outline; **K/M abbreviation** on large numbers *(confirms the settled number-formatting rule)* |
| texture | faint watermark pattern on the dark ground |
| notable | equipped items carry **sparkle particles**; active tab = gold pill vs plain text |
| suits | rpg, cardgame, strategy |

## 38 `gameui5.png` — flat torn paper *(no outline, no shadow, no radius)*

| aspect | observation |
|---|---|
| frame | white panels with **irregular torn/ripped edges** — a rough polygon silhouette. **No outline, no radius, no shadow at all** |
| header | a dark plum **torn band overhanging** the panel top |
| buttons | torn, slightly sheared plates in maroon / orange / red, white caps |
| shadow | **none** |
| corner | **torn and angular** — neither rounded nor chamfered; an irregular polygon |
| typography | bold condensed caps, no outline |
| texture | none — pure flat colour |
| notable | proves `Torn` is not tied to parchment: it works as a **flat graphic** register where value contrast and the ragged silhouette do all the separating |
| suits | platformer, puzzle, shooter-arcade |

## 39 `store1.png` — cosy pastel shop

| aspect | observation |
|---|---|
| card | a **four-band card**: art (with a magnifier in the top-right) / name / **limit band** / **price footer welded at the bottom** |
| unaffordable | **only the price footer desaturates** to grey — the rest of the card is untouched |
| header | cream plaque with rounded ends and **two dot rivets**, overhanging the panel top |
| tabs | **two tab levels with different active renderers** — a cream pill on the first row, a dark pill on the second; text tabs separated by **small diamond dots** |
| shadow | very soft to none |
| corner | moderate, uniform, soft |
| typography | rounded soft sans, brown/dark-green on light |
| texture | subtle paper/linen ground; illustrated scene above the panel |
| notable | currency toolbar = one dark pill grouping four icon+value pairs |
| suits | cardgame, rpg, survival (cosy) |

## 40 `ui7.png` — pixel-art diegetic workbench *(the pixel register)*

| aspect | observation |
|---|---|
| frame | the UI **is a wooden workbench** with a saw and hammer laid across it; the crafting panel is a green board inset into it |
| pixel discipline | **1px outlines, no anti-aliasing, limited palette, corners stepped by pixel** (2–3px stair) |
| shadow | **none** |
| typography | **bitmap/pixel font** — cream on green, dark on parchment |
| texture | pixel wood, pixel paper |
| notable | active tab is **raised and lighter with a tooltip label below it**; selected hotbar slot = a **1px white outline offset outward**; HUD bars carry vine/leaf ornaments at their ends |
| suits | topdown, survival |
| confirms | `Stepped` is not merely a corner treatment — **pixel is a whole coherent register** (outline weight, font, AA, shadow policy all follow from it) |

## 41 `gameui6.png` — layered-stroke "biscuit" kit

| aspect | observation |
|---|---|
| frame | **three concentric strokes** — dark brown outer, mid orange, cream face. The layered outline *is* the depth mechanism |
| header | **green ribbon with folded ends** overhanging the panel top and past both sides *(3rd sighting of this banner type)* |
| shadow | **none** — the layered strokes replace it |
| corner | large / capsule |
| typography | bold rounded caps, dark brown on cream |
| texture | none |
| notable | **icon medallion overhanging the RIGHT end** of a meter — the mirror of the left-cap pattern, and its first sighting; toggle = capsule with a **square** knob |
| suits | puzzle, platformer, cardgame |

## 42 `survaivleandrpg2.png` — pixel inventory (Stardew family)

| aspect | observation |
|---|---|
| frame | cream pixel panel with a **layered 2–3px pixel border** (darker band inside a brown outer band) |
| tabs | pixel tabs on the top edge; active is **raised, lighter and joined to the panel** |
| shadow | **none** |
| corner | pixel-stepped, small |
| typography | **pixel font**, small, dark on cream |
| texture | pixel wood/paper |
| notable | **circular wood-framed portrait window**; hotbar selected cell = **amber fill**; top bar carries **input-hint prompts with chords** (`L2 + ✛  SPLIT STACK`) — confirms the InputHint chord requirement |
| suits | topdown, survival, rpg |

## 43 `vecteezy_hud-frames-futuristic-text-box-...jpg` — sci-fi frames **with labels** *(pairs with 14)*

Everything in 14, plus how the family labels itself:

| element | detail |
|---|---|
| label tab | a small **solid cyan plate with dark caps text** attached to the frame's top edge — centred, left, or with a **sheared right end**. This is the sci-fi header plaque, and it is a *tab*, not a rounded plaque |
| vertical label | a tab on the **left edge with text rotated 90°** |
| corner badge | a **diamond/rhombus** plate carrying `01` / `i` / `!`, attached at a corner or floating beside the frame |
| title band | a solid cyan band across the top whose **right end is cut at 45°** |
| runs | **dashed runs** (`─ ─ ─`) along an edge as a section marker |
| inline slot | a small square **thumbnail inset** within the frame |
| corners | notched, stepped and long-45° cuts **mixed within one frame** |
| typography | **condensed technical caps, letter-spaced**, cyan or dark-on-cyan, small |
| shadow | none; the stroke carries a subtle dark offset |

## 44 `vecteezy_game-ui-kit-with-menus-...jpg` — bright candy casual

| aspect | observation |
|---|---|
| frame | **thick saturated coloured outer band** (blue / red / teal / orange, one per screen) wrapping a cream or dark inner panel — the band *is* the frame, and its hue identifies the screen |
| header | **lime ribbon with a yellow inner outline and folded ends**, overhanging the top and past both sides |
| close | **red circle** straddling the top-right corner |
| shadow | soft |
| corner | large radius |
| typography | heavy rounded caps, white with a **thick dark outline** and a slight extrude |
| texture | mostly flat + gloss, but the pink button set carries a **mottled/spotted fill** |
| notable | rows weld a **price pill at the right**; shop cards weld one at the **bottom** and carry a green header band; nav arrows are **cyan triangles overhanging the panel's side edges**; two tabs attached above a list |
| suits | puzzle, platformer, cardgame |

---

## Final coverage

**59 unique images** (`gameui9.png` is byte-identical to `ui7.png`; verified by hash).

**45 documented here, covering 46 of the 60 files.**

### Not yet read — 14 files

`gameui1` · `gameui3` · `rpg2` · `rpg3` · `rpgui3` · `skilltree` · `skilltree3` · `ui2` ·
`survaivleandrpg1` · `vecteezy_square-wooden-frames-*` · `vecteezy_wooden-buttons-cartoon-*` ·
`vecteezy_wooden-game-buttons-cartoon-*` · `uitexturs` · `uiwood`

Two of those (`uitexturs`, `uiwood`) were read earlier in the session and their findings are already
in the tracker — `uitexturs` is the nine-material sheet the whole grain axis was measured from, and
`uiwood` is a single-material wood family. The other twelve belong to families already documented
several times over (cartoon wood, parchment, stone, casual pastel), so they are expected to confirm
rather than extend — **but that is a prediction, not a result, and the pass is not complete until
they are read.**

## 45 `gameui1.png` — paper scrapbook survival kit

| aspect | observation |
|---|---|
| frame | **layered paper sheets, stacked slightly offset**, torn and curled edges; fastened with **wax seals** and small metal **brads/clips** |
| slots | **HEXAGONAL cells in a honeycomb** — first hex-grid sighting in the folder |
| banners | narrow vertical strips **hanging from a wooden bar with rounded ends**, header plate on top, level chip at the bottom |
| shadow | soft; the paper stack casts onto itself |
| corner | torn / curled paper |
| typography | **condensed serif / typewriter**, dark brown on paper, caps titles |
| texture | paper in several tones, wood, **wax**, **twine** |
| notable | **luggage tags with a twine loop**; **washi-tape strips** as labels; loading bars with a **sheared pointed right end**; a zones map as a **dashed path linking pentagon nodes**; scroll with a rolled bottom and a red title band overhanging the top |
| suits | survival, rpg, adventure |

## 46 `rpg3.png` — chunky cartoon RPG

| aspect | observation |
|---|---|
| frame | tan/olive panels with a **thick dark brown outline**; no shadow — the outline does it |
| slots | rounded squares, **rarity by fill colour**; empty slots tan with an inner shadow |
| item card | **blue banner header overhanging the card top**, carrying an icon at the left and `Lv.14` right-aligned |
| notable | **comparison indicators** — stat chips turn **green with an up-arrow** to show the delta against what is equipped. Nothing else in the folder does this |
| typography | bold, white with a **thick dark outline** |
| suits | rpg, cardgame |

## 47 `skilltree.png` — dark idle skill tree

| aspect | observation |
|---|---|
| tree | node = rounded square whose **border colour encodes its BRANCH** (amber / green / blue), with a level badge bottom-right; **the connector lines take the same branch colour**, so the tree reads as three coloured paths |
| routing | connectors are **orthogonal, L-shaped** — never diagonal |
| unowned | **dimmed by alpha** *(matches 20)* |
| info card | portrait tile left, title + `Lv.` right-aligned, a coloured effect line, description, a `Next Upgrade` section in a **second accent**, and a **cost chip sitting above** its Upgrade button |
| shadow | none |
| typography | plain sans; amber and cyan accents carry the meaning |
| suits | rpg, strategy, cardgame |

## 48 `vecteezy_square-wooden-frames-...jpg` — **the attachment model, isolated**

Six avatar frames. **Identical geometry** — a square wood block, all four corners chamfered at 45°,
a recessed inner well with an inverted bevel. They differ by **attachment alone**:

`plain` · **vine with leaves** on two opposite corners · `plain` (grain variant) ·
**rope looped across all four corners** · `plain` · **heavy ivy down both sides**

| aspect | observation |
|---|---|
| shadow | internal bevel only; **no external drop shadow** |
| corner | chamfered 45°, all four |
| texture | wood grain with a warm radial gradient (lighter centre) |
| **why it matters** | a clean, isolated proof of the Phase E model: **one base + an attachment set = a family**. Identity comes from the ornament, not the geometry |

## 49 `ui2.png` — hero detail (same family as 17)

| aspect | observation |
|---|---|
| frame | essentially **frameless** — a blue radial gradient with elements floating on it |
| rarity | a small **labelled chip** (`EPIC`) above the name — rarity as a word, not only a colour |
| body text | **inline coloured keywords** inside a paragraph (`10 damage` in orange) — confirms the per-run text-role requirement |
| locked | rune slots and skills show a **padlock plus `Lv15` / `Lv20` in words**, and desaturate |
| stats | rows of coloured icon tile + small-caps label + large value on a translucent strip |
| buttons | a green **cost pill** with a small label chip **above it** (`Lv9 Upgrade`), and the primary action in a **distinct amber** |
| shadow | soft under the character only |
| typography | heavy rounded sans, white; small-caps grey-blue labels |
| suits | platformer, cardgame, rpg-lite |

## 50 `rpgui3.png` — handheld pixel RPG *(the LabelValuePair reference)*

| aspect | observation |
|---|---|
| **LabelValuePair** | exactly as measured: **two welded plates of OPPOSITE polarity** — a dark plate with a cream caps label welded to a light plate with a dark value, about **2 : 1** in width with a 1–2px joint (`ATTACK 7`, `DEFENSE ---`, `COMBO 3`) |
| meter row | **label chip welded left + bar + value right-aligned**, all one welded row (`LV`, `HP`, `RP`) |
| frame | cream panel, 2px dark brown border, **ornamental scroll motifs in the corners** |
| header | dark brown band with cream caps and small flanking marks; the blue `INVENTORY` band carries a **wave motif at its ends** |
| shadow | **none** |
| corner | square, with ornament rather than radius |
| typography | **pixel/bitmap**, very compact, cream-on-dark and dark-on-cream |
| suits | rpg, topdown, survival |

## 51 `gameui3.png` — pale wood GUI with an explicit STATE SET

| aspect | observation |
|---|---|
| **states** | the sheet labels four button states outright: **`Normal` · `Over` · `Click` · `Disabled`**, and **Disabled is fully desaturated to grey** — the settled saturation-drain rule, shipped as art |
| icons | every icon ships in **four variants** (normal / hover / grey disabled / outline), so icon state is authored, not derived |
| frame | wood-plank panel with a **header plank overhanging the top**, small metal side brackets, and a lighter inner field |
| depth | a **stacked panel** — a back plate offset slightly behind the front one, instead of a drop shadow |
| shadow | soft, plus the stacked-plate offset |
| texture | pale gold wood grain |
| notable | meters appear both **segmented** and continuous in the same kit; narrow scrollbar with a wood knob |
| suits | platformer, puzzle, citybuilder |

## 52 `skilltree3.png` — tier-coloured talent grid

| aspect | observation |
|---|---|
| tiles | thick dark outline; **tier by fill colour, one tier per row** (grey → green → blue → gold); small level number in the **top-left corner** |
| tooltip | **white rounded rect with a speech tail** pointing at the tile |
| button | the Upgrade plate is **slightly sheared** (angled left/right edges) with a thick lighter outline and the cost beneath the label |
| nav | active bottom-nav item is a **lighter, taller tile carrying an up-arrow** *(matches 37)* |
| notable | inline coloured run again (`Upgraded **20** times`); gold **star chip** as the level badge |
| suits | rpg, cardgame, strategy |

## 53 `survaivleandrpg1.png` — pixel **open book**

| aspect | observation |
|---|---|
| frame | an **open book**: two parchment pages with a central **spine/gutter** (page-curl gradient either side), bound in a **red leather cover** showing as the border |
| tabs | coloured **index tabs protruding from the left edge** of the book, and one from the right — outside the page, book-style |
| empty slots | show a **ghosted/embossed silhouette** of what belongs there — a **fourth empty state** beside blank, invite-`+` and locked |
| stat bars | the value floats **above the fill's right end**, not inside the bar and not at the row end |
| shadow | none (pixel); the gutter gradient does the folding |
| typography | **three families on one screen** — blackletter display title, pixel serif headings, pixel sans body |
| texture | pixel parchment, red leather |
| suits | rpg, survival, topdown |

## 54 `rpg2.png` — gothic wood/parchment shop

| aspect | observation |
|---|---|
| frame | wood planks with **ornate carved corner brackets**; parchment inset panels whose **bottom edges are torn/notched** |
| header | wood plaque with **carved ornate ends** overhanging the top; section headers flanked by **fleuron marks** on both sides |
| rows | parchment plate + gold-framed icon tile + body + a **footer strip with notched ribbon ends** carrying a count chip, a cost pill and the action button |
| shadow | soft |
| typography | **blackletter / gothic throughout** — gold titles, dark brown body |
| texture | dark wood, parchment |
| notable | segmented progress bar beneath the icon on upgrade rows; gold chevron scroll arrows above and below a column |
| suits | rpg, survival |

## 55 `vecteezy_wooden-game-buttons-...jpg` — one material, many silhouettes

No text; a silhouette study in a single wood material with a **recessed inner well** (inverted bevel:
light bottom-right, dark top-left) on every piece.

Silhouettes present: **chamfered rect** (dominant) · rounded rect · **capsule** · hexagon-ish
(chamfered short sides) · **notched rect** (square bites at the mid-left/right edges) ·
**tiered/stacked plate** (a narrower plate sitting on a wider base) · rounded square · **circle** ·
left/right **triangles**.

Attachments: **rope looped across two opposite corners**; **vines with leaves** along an edge.

| aspect | observation |
|---|---|
| shadow | none external — the well's inverted bevel is the whole depth model |
| texture | wood grain with a warm gradient, knots, and **grain direction varying per plate** |
| **why it matters** | the mirror of 48: there, one geometry × many attachments; here, **one material × many silhouettes**. Together they show geometry, material and ornament are three independent axes |

## 56–58 — read earlier this session, findings already in the tracker

- **`uitexturs.png`** — the nine-material tile sheet. The entire grain/material axis was measured
  from it (67× spread on a colour- and scale-invariant metric); see Stage 38.
- **`uiwood.png`** — one wood material carrying a whole family (panel, round icon buttons, square
  icon buttons, rope-lashed bars). Silhouette varies *within* the family while material holds it
  together — the inverse of what the kit does.
- **`vecteezy_wooden-buttons-cartoon-...`** — viewed at 4898×3695 via the Ghostscript EPS render
  (Stage 37). Wood kit: panel, buttons, icon buttons, sliders, stars.

---

# PASS COMPLETE — 59/59 unique images

60 files, of which `gameui9.png` == `ui7.png` (verified by hash). **All 59 unique images have now
been read**; 55 have a numbered entry above and 3 more are recorded in the tracker from earlier in
the session.
