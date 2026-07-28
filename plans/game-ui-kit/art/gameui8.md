# `gameui8.png` — parchment & wood RPG UI kit (transparent)

**1200 × 1200** · asset sheet, alpha background · **parchment + wood, gold trim** family
**Relevance:** **`rpg` and `survival` above all** — this is the richest RPG-specific
vocabulary in the folder, and the only sheet that ships quest rows, an equipment doll, a
dialogue panel and an item tooltip together.

---

## Widget 1 — `Panel` (Inventory / Equipment / Quests / Shop / Character Card)

Scanned H at y=400 on `Inventory`.

```
 alpha│keyline│ wood │ light band │keyline│      plate
      │  2px  │ 6px  │    10px    │  3px  │
       #0E0000 #76441A #EDD1AA     #82592E  #C99F6B
       L=0.03  L=0.28  L=0.80      L=0.35   L=0.60
```

| property | measured | ratio to plate |
|---|---|---|
| total frame | **21px** on a ~300 × 350px panel | — |
| light band | 10px L=0.80 | **1.33 ×** |
| wood | 6px L=0.28 | 0.47 × |
| inner keyline | 3px L=0.35 | 0.58 × |
| plate | `#C99F6B` L=0.60 | 1.00 |

Corners carry **metal brackets** — a separate art element at each of the four corners,
overhanging both edges.

**Title plate colour codes the panel type:** parchment (Inventory, Equipment),
**green** (Quests), **purple** (Shop), **blue** (Character Card). All in the same frame.
That is a genuinely useful idea the kit does not have: **the attachment carries the
category hue, not the panel**.

## Widget 2 — `CurrencyBar` (×3: coin, gem, crystal)

Scanned H at y=60.

| part | measured |
|---|---|
| icon | **45px** (x=542..586), overhanging the plate's left cap |
| plate | `#342412` **L=0.14** S=0.49 — very dark |
| value | white with a dark outline |
| `+` button | **green**, ~48px, welded to the right cap, glyph `#FBF7D6` L=0.91 |

Plate L=0.14 sits alongside `citybuilder1`'s 0.17. **A HUD currency plate is L≈0.15
regardless of family** — five references now agree, because the plate exists to make white
text survive an arbitrary background.

## Widget 3 — `AvatarFrame`

Circular portrait in an ornate gold/brown ring, with a **`Lv. 25` plate overhanging the
bottom rim**. Confirms `ui5`/`rpgui`'s AvatarFrame and adds: the level plate hangs *below*,
not beside.

## Widget 4 — `HeartRow`

Seven hearts, filled red / empty dark brown. **Discrete health** — no bar. The empty heart
keeps its shape and outline; only the fill drops. Sixth distinct "unearned/empty"
rendering in the folder.

## Widget 5 — `StatBar` (health, XP)

Scanned V at x=350.

| property | measured |
|---|---|
| bar height | **32px** (y=87..119) — matches the ~30px rail height seen in five other refs |
| left cap | a **round icon disc overhanging** the bar (water drop, `XP`) |
| fill | blue with a bright top gloss (`#80D1FB` L=0.74) over a darker body |
| value | `120/120` centred, white with a dark outline |

Value **centred over the fill**, not beside the bar. Combined with the outline, this is how
the text stays readable whatever the fill level.

## Widget 6 — `ItemSlot` grid

Recessed brown squares. Filled slots carry item art plus a **count in the bottom-right
corner**; one slot has a **blue rarity tint** on its background. Empty slots are the plain
recess.

## Widget 7 — `CapacityRow`

`(bag) 23/40 (+)` docked at the panel's **bottom edge**, inside the frame. A panel-level
footer, distinct from the welded card footer seen elsewhere.

## Widget 8 — `EquipmentDoll`

Character art centred, flanked by **two columns of equipment slots**, with a bottom row of
four. One bottom slot is **locked with a padlock**. The doll is a layout, not a widget —
worth recording because the kit will need a "slots around a centre" container.

## Widget 9 — `QuestRow`

`(icon) Title / Description  ....  4/10`

| part | observed |
|---|---|
| icon | at the left, inside the row |
| title | bold, dark |
| description | lighter, smaller, beneath |
| progress | `4/10` at the **right edge**, aligned to the description's baseline |
| locked variant | `???` + padlock, whole row **dimmed**, description replaced by a requirement |

The locked quest confirms `citybuilder4`'s pattern: **dim + state the requirement in
words**, rather than hide.

## Widget 10 — `SpeechBubble`

White rounded rect + **tail** + a small **▼ continue indicator at the bottom-right**. The
▼ is the affordance that says "more text follows" — no reference before this had one.

## Widget 11 — `EmoteBubble`

Small white circle with a tail carrying a single icon (heart), floating above an NPC.
Confirms `ui1`'s CountBubble as a general **tailed container**, not just for numbers.

## Widget 12 — `ShopGrid`

Item tiles each with a **coin icon + price beneath the tile**, and a wide **total plate**
across the panel's bottom. Ninth picture with a price welded under an item.

## Widget 13 — `CharacterCard`

Portrait → **★★★☆☆ rating row** (earned gold, unearned grey outline) → `Lv. 25` plate →
stat rows of `icon · dotted leader · value`.

The **dotted leader** between label and value is new — a typographic device for aligning
values in a narrow column.

## Widget 14 — `DialoguePanel`

| part | observed |
|---|---|
| portrait | large character art **overhanging the panel's left edge and top** |
| name plate | purple plate overhanging the **top-left**, over the portrait's edge |
| body | parchment, 3 lines |
| continue | ▼ at the bottom-right |

The portrait breaks two edges at once. This is the most aggressive overhang in the folder
and it is what makes the panel read as a character speaking rather than a text box.

## Widget 15 — `SkillIcon`

Circular icons with an ornate rim; some carry a **count badge** (`10`) at the corner.

## Widget 16 — `PortraitEmote` set

Five face variants of the same character: happy, neutral, angry, sad, surprised. Emotion is
a **swappable sub-asset** of the avatar, not a separate widget.

## Widget 17 — `StatusIcon` set

Sparkle, water drop, `!`, `?`, music note — floating world indicators with no plate.

## Widget 18 — `MapPin` set

Teardrop pins in five colours with glyphs (`!`, star, dot, star, skull). Same silhouette as
`citybuilder1`'s WorldPin, here as a **coloured set** — hue is the category.

## Widget 19 — `ElementOrb` set

Circular elemental icons (wind, ice, fire, shield, dark) with an ornate rim and a coloured
core.

## Widget 20 — `MapFlag` set

Small pennants in five colours plus a house glyph — a second, lighter-weight map marker
family alongside widget 18.

## Widget 21 — `CategoryIcon`

Backpack / Crafting / Skills / Pets — a large icon with a **label beneath and outside**,
no plate. A navigation affordance that is pure icon + text.

## Widget 22 — `TabBar`

`All | Equipment | Items | Materials | Quest` on a parchment strip. The selected tab is a
**gold pill**; the rest are flat text on the strip. Below it, a large empty content panel.

Selected = **a filled pill appears**, rather than the tab changing colour. Yet another
selection mechanism (sixth in the folder).

## Widget 23 — `ItemTooltip`

| row | content |
|---|---|
| 1 | item name in **blue** (rarity hue) + `Common` right-aligned |
| 2 | type — `Weapon` |
| 3 | item icon + stat rows — `ATK +25` |
| 4 | flavour description, two lines |

Dark panel, gold border. **Rarity is carried by the name's colour and repeated as a word**
— belt and braces, and greyscale-safe.

## Widget 24 — `Banner` / `Cartouche`

Vertical heraldic pennants (blue, red) and an ornate **cartouche label frame with laurel** —
decorative title holders.

## Widget 25 — `Minimap`

Circular map in an ornate frame with:
- a **title plate overhanging the top**
- an **`N` compass mark** inside
- a **search button at the bottom-left**, straddling the rim
- **`+` / `−` zoom buttons at the bottom-right**, straddling the rim

Four attachments on one circular host, at four different anchors.

## Widget 26 — `IconButton` (gift, mail, pause, menu)

Dark brown plates with a gold rim. The mail button carries a **red notification dot** at
its top-right. Confirms NotificationDot for the seventh time.

---

## Cross-widget rules

1. **HUD currency plate is L ≈ 0.15** — now agreed across five unrelated families.
2. **The attachment carries the category hue**, not the panel (green Quests, purple Shop,
   blue Character Card, all in one frame).
3. **Values are centred over their fill with an outline**, so they survive any fill level.
4. **A dotted leader** aligns label→value in narrow columns.
5. **A portrait may break two panel edges at once.**
6. **Circular hosts need four anchor points** — the minimap uses top, bottom-left,
   bottom-right and inside.
7. **Rarity is doubled**: a hue on the name plus the word. Greyscale-safe.
8. **▼ means "more"** — a continue affordance on any text container.
9. **Selection mechanism #6**: a filled pill appears behind the active tab.

## Actions

- [ ] `KitAttach` needs a **category hue** independent of the host.
- [ ] Progress widgets: value **centred over the fill** + text outline, as a variant.
- [ ] Add a **dotted leader** run between label and value.
- [ ] Circular hosts must support **≥4 anchors** with straddle.
- [ ] Add `KitState.Empty` (heart outline, grey star, dark silhouette) as a per-skin
      renderer — this is now the **sixth** distinct rendering of "empty/unearned".
- [ ] `QuestRow`, `ItemTooltip`, `DialoguePanel`, `Minimap`, `EquipmentDoll`,
      `CharacterCard`, `CapacityRow`, `SpeechBubble` (with ▼) → catalogue, **priority for
      the `rpg` project**.
