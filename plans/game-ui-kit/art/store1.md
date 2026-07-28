# `store1.png` — mobile idle-RPG shop

**555 × 1110** · live menu screen · **soft cream & green, banded cards** family
**Relevance:** **`rpg`**, `cardgame`, universal shop.

---

## Widget 1 — `ShopCard` — a three-band stack, measured

Scanned V at x=60.

```
        y 273 ┌──────────────────┐
              │      (art)    🔍 │
              │       10         │  CONTENT band  L=0.75→0.85
              │  Skill Summon    │
              │     Ticket       │
        y 407 ├──────────────────┤
              │ Daily Limit 10/10│  LIMIT band    L=0.96
        y 429 ├──────────────────┤
              │   ◆ 100          │  PRICE band    L=0.55
        y 473 └──────────────────┘
```

| band | measured | height | share |
|---|---|---|---|
| content | `#B6DE9F`→`#DDEDC5` **L=0.75–0.85** | ~134px | **0.67** |
| limit | `#F9FDEE` **L=0.96** | ~22px | **0.11** |
| price | `#8AB665` **L=0.55 S=0.36** | ~38px | **0.19** |
| card total | | **200px** | 1.00 |

Relative to the content band's mid-lightness (~0.80):

| band | ratio |
|---|---|
| limit | **1.20 ×** (lightest) |
| price | **0.69 ×** (darkest) |

**Three bands of one hue at three lightnesses.** No borders, no dividers — the lightness
step *is* the divider. This is the cleanest banded card in the folder and the ratios
transfer directly.

## This confirms the `store.md` split

`store.md` argued the "welded footer" is really two widgets. This card settles it:

| element | ratio | source |
|---|---|---|
| **status band** | **0.19** | skilltree1 (`MAX`), **store1 (price band)** |
| **action button** | **0.10** | store (`BUY`) |

Two independent references now measure a status band at exactly **0.19**, and the action
button at **0.10**. The distinction is real and the numbers are stable.

## Widget 2 — Unaffordable state

One card's price band is **grey with a grey gem icon** while its siblings are green with a
magenta gem. The rest of the card is unchanged.

**Only the price band desaturates** — not the art, not the name, not the limit. That is
more precise than `rpg1`, which dimmed the whole row. Desaturating just the blocking
element tells the player exactly *what* is wrong.

Worth adopting: `KitState.Unaffordable` should target the **price slot**, not the card.

## Widget 3 — `MagnifierButton`

A small round `🔍` in each card's **top-right corner**, inside the card. Opens details
without buying. A second, non-destructive affordance on a card whose whole body is a buy
target.

## Widget 4 — `CurrencyPill` (×4, top)

Dark translucent capsules, icon overhanging the left cap, value right-aligned. Four
currencies in one bar at 555px wide — ~130px each.

Values use abbreviated notation (`1.57C`, `51,470`) — second reference after `skilltree4`
needing a **number-formatting policy**.

## Widget 5 — `TitlePlate`

`Shop` on a cream rounded plate with **two small dot ornaments, one at each end**,
overhanging the panel's top edge. The dots are the entire decoration — the minimal version
of `rpg2`'s flourishes and `rpgui3`'s end caps.

## Widget 6 — `TabRow` (×2)

Text tabs separated by **small diamond glyphs** rather than gaps or rules. The selected tab
becomes a **cream pill**; the rest are bare text. Some tabs carry a small **orange diamond
badge**.

| row | tabs |
|---|---|
| 1 | Ad · Diamond · **Ruby** · Luna |
| 2 | Package · Costume · Diamond · **Currency** |

**Separator-as-glyph** is new: the diamond is a typographic separator, not a border. It
scales to any tab count and costs nothing.

Selection = the pill materialises — the same mechanism as `skilltree4` and `gameui8`.

## Widget 7 — `IconRail` (bottom)

Dark strip with six category icons, below the tabs. Three levels of navigation on one
screen (icon rail → tab row 2 → tab row 1), each a different visual weight.

## Widget 8 — `CharacterArt`

A mascot in the top-left **overlapping the currency bar**, breaking its bounds. Decorative
overlay crossing a chrome element — same device as `store.png`'s leaves and `gameui9`'s
tools.

## Widget 9 — `CloseHint`

`Tap to Close` with a diamond glyph at the screen's bottom, on the background rather than
in a bar. A plateless, low-emphasis instruction — the **low-emphasis text role** flagged in
`racing3.md` is needed again here.

---

## Cross-widget rules

1. **Banded card: content 0.67 / limit 0.11 / price 0.19**, one hue at three lightnesses
   (1.20 × and 0.69 × the content band). No dividers needed.
2. **Status band = 0.19, action button = 0.10** — confirmed independently.
3. **Unaffordable desaturates only the price slot**, not the card.
4. **Separator-as-glyph** (a diamond between tabs) replaces rules and gaps.
5. **Three navigation levels** can coexist by varying visual weight.
6. **Number formatting is a real requirement** — second reference.

## Actions

- [ ] Add `BandedCard` with ratios **0.67 / 0.11 / 0.19** and lightness steps
      **1.20 × / 1.00 × / 0.69 ×**.
- [ ] `KitState.Unaffordable` targets the **price slot** only.
- [ ] Add **separator glyph** as a tab-strip option.
- [ ] Add the **low-emphasis text role** (second request, after `racing3`).
- [ ] Add a **number-formatting policy** (second request, after `skilltree4`).
