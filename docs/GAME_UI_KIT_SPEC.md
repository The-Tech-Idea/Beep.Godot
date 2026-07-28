# Game UI kit — anatomy spec

Derived from `Example_Art/gameui1..7.png` (2026-07-26), seven complete game UI kits, plus the
five gameplay references in `citybuilder1..5.png`.

> **Licensing:** several of these are watermarked comps (Dreamstime, Game Art Partners, Envato).
> They are **style reference only** — nothing here is shippable art. What we take from them is the
> *anatomy*: which parts an element is made of and how they overlap. The art itself comes from
> the CC0 Kenney packs or is authored.

## The single biggest difference from what we build

Every element in every kit is **made of overlapping parts**. Ours are made of one rectangle with
text inside it. That is the whole gap, and it shows up the same way in eight places:

| Element | Every kit does | We do |
|---|---|---|
| Panel title | a **banner/plaque that overhangs the panel's top edge** — plank, ribbon with folded ends, or ellipse | a Label inside the panel |
| Close / collapse | a **round or square chip floating on the top-right corner, overhanging the border** | (chevron — correct shape, but sits inside the rect) |
| Panel body | **frame + a recessed inner well** with its own border, two-tone | one flat rect |
| Progress bar | capsule track, thick outline, coloured fill, **icon cap on one end** | plain bar |
| Value chip | small capsule, **icon disc + number** | Label pair in a row |
| Icon button | square, rounded, **thick dark outline**, icon centred, shipped as a state set | themed rect |
| Text button | pill / rounded rect, centred caps label, **3+ colourways** | themed rect |
| Toggle | pill with a **coloured square knob** + ON/OFF | CheckBox |

## Rules

### 1. Outline and bevel
A **3–5px outline, distinctly darker than the fill**, on everything. Plus an inner highlight
along the top edge (gameui6, gameui7 are the clearest). A hairline border is what makes UI read
as a document; the heavy outline is what makes it read as an object.

### 2. Title banner overhangs
The header is a separate piece sitting **on top of** the panel, crossing its top border, and is
often a different width from the panel. gameui7 uses an ellipse, gameui2 a wooden plank, gameui6
a ribbon with folded ends, gameui1 a tape/label strip. None of them put the title inside the box.

### 3. Corner chips float and overhang
The X button in gameui4/5/7 hangs off the panel's top-right corner, half on and half off the
border. **This is the pattern for our collapse toggle** — it is currently drawn *inside* the
panel rect and should overhang the corner instead.

### 4. Frame plus inner well
Panel = outer frame with its own fill, containing a recessed content area with a second border
and a darker fill. Lists, grids and text all live in the well, never directly on the frame.

### 5. Bars carry an icon cap
gameui6 puts a heart on the health bar's right end and a bolt on the energy bar's. gameui4 puts
the icon on the left. Either way the bar identifies itself without a label — which is what our
`ResourceBadgeComponent` already does with its overhanging icon disc.

### 6. Buttons ship as state sets
gameui3 labels them literally: **Normal / Over / Click / Disabled**, plus a greyed variant.
Every kit ships 100+ square icon buttons in one visual family. State is shown by fill and bevel
shift, not by a hue change.

### 7. Colourways, not one theme
gameui4/5/6 each ship every button in 3+ colours (green/orange/red, etc.) so a screen can colour
by *meaning* — confirm green, cancel red, neutral grey. That maps onto our existing semantic
colours (`semantic_success` / `danger` / `warning` / `info`), which are currently unused by the
HUD components.

## Corrections to what exists

- [ ] `CollapsiblePanelComponent` — the floating chevron is the right idea but is positioned
      *inside* the panel rect. Move it to **overhang the top-right corner** (half outside), and
      style it as a distinct chip rather than a themed Button.
- [ ] `BeepDialogLayout` / all modal screens — titles are inline Labels. They need an
      **overhanging banner**.
- [ ] Panels have no inner well. Add the recessed content area as part of the panel styling.
- [x] `ResourceBadgeComponent` already matches the value-chip anatomy (icon disc overhanging a
      capsule, heavy outline, drop shadow, optional fill).
- [ ] `MeterBarComponent` — add the icon cap.
- [ ] Semantic colourways are defined in every theme but unused; wire them to button intent.
- [ ] Icon-button family: square, rounded, thick outline, one visual family, four states.

## Which reference for which register

| Register | Reference | Genres |
|---|---|---|
| Wood / adventure | gameui2, gameui3, gameui1 | rpg, survival, topdown, cardgame |
| Clean cartoon vector | gameui4, gameui5 | puzzle, platformer, casual |
| Chunky candy | gameui6, gameui7 | puzzle, platformer, cardgame |
| Parchment / crafting | gameui1 | rpg, survival, citybuilder(industrial) |

The sci-fi glass currently applied to citybuilder came from the shooter/strategy register and is
the wrong family for it — see `docs/hud/citybuilder.md` §10.
