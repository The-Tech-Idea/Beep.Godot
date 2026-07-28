# Card Game HUD

**Genre id:** `cardgame` · **Main scene:** `cardgame_main.tscn` · **Genre HUD script:** `CardGameHudComponent`

---

## 1. Reference games

| game | what its HUD is known for |
|---|---|
| **Hearthstone** | The **fanned hand** at the bottom with hover-to-zoom, mana crystals `7/10` bottom-right, hero portraits with health/armour at both ends of the board, and a large **End Turn** button on the right edge that changes colour when the turn is forced. |
| **Slay the Spire** | **Energy orb bottom-left** (`3/3`), draw pile count bottom-left, discard bottom-right, **relics as a top row**, potions top-left, and **enemy intent icons floating above each enemy** — the single most-copied roguelike-deckbuilder idea. |
| **MTG Arena** | Multiple zones made explicit: library, graveyard, exile, stack. Priority/phase indicator down the right. Zone counts are always visible. |
| **Balatro** | Score formula shown live (`chips × mult`), hand-type readout, and remaining hands/discards as discrete pips. |
| **Inscryption** | Physical-table framing; costs and scales as diegetic objects rather than numbers. |

**The through-line:** card games are **information games**. The player must be able to count outs — deck size, discard contents, what the opponent can afford. Hidden zone counts break the genre.

---

## 2. Canonical layout

```
┌───────────────────────────────────────────────────────────────────────────┐
│ 🏺🏺🏺🏺  relics                                    ┌───────────────────┐  │
│ 🧪🧪     potions                                    │  Opponent      ❤30│  │
│                                                      └───────────────────┘  │
│                    ▣ ▣ ▣   opponent hand (backs)                          │
│  ┌──────────────────────────────────────────────────────────────────┐     │
│  │  😠 ⚔12      😠 🛡8        enemy intents above each enemy        │     │
│  │                      (board)                                      │     │
│  └──────────────────────────────────────────────────────────────────┘     │
│                                                                           │
│ ┌───────┐                                                  ┌────────────┐ │
│ │ Player│          ╭───╮╭───╮╭───╮╭───╮╭───╮               │  END TURN  │ │
│ │  ❤ 68 │         ╱     ╲     ╲     ╲     ╲                └────────────┘ │
│ │  🛡  4│        │ hand fanned, hover to zoom │                           │
│ └───────┘         ╰───╯╰───╯╰───╯╰───╯╰───╯                               │
│  ⚡3/3   🂠 18                                          🗑 7                │
│  energy  draw                                          discard            │
└───────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Element specification

| # | element | region | type | behaviour | priority |
|---|---|---|---|---|---|
| 1 | **Hand** | bottom-centre | fanned/arc card layout | hover raises + zooms; drag to play; **fan angle shrinks as hand grows** | **P0** |
| 2 | **Energy / mana** | bottom-left | orb or crystal row `3/3` | spent crystals dim; refills with animation on turn start | **P0** |
| 3 | **Draw pile count** | bottom-left | icon + number | click to peek (if allowed) | **P0** |
| 4 | **Discard pile count** | bottom-right | icon + number | click to view | **P0** |
| 5 | **End Turn** | right edge | large button | changes colour when no plays remain; **the most-clicked control in the genre** | **P0** |
| 6 | Player portrait | bottom-left | portrait + health + armour | armour shown as a separate shield number, not merged | P1 |
| 7 | Opponent portrait | top-right | portrait + health + armour | mirrors player | P1 |
| 8 | **Enemy intent** | above each enemy | icon + number | "will attack for 12" — makes the game a puzzle rather than a gamble | **P0** (roguelike) |
| 9 | Relics / artifacts | top-left row | icon row | hover tooltip explains each | P1 |
| 10 | Potions / consumables | top-left | 2–3 slots | click to use | P2 |
| 11 | Exhaust / exile count | near discard | small counter | zone completeness | P3 |
| 12 | Turn / phase | top-centre | text | "Turn 4 — Your move" | P2 |
| 13 | Card tooltip | follows cursor | `TooltipComponent` | keyword reminders | P1 |
| 14 | Targeting arrow | drawn | line/bezier | drag from card to target | P1 |

---

## 4. Genre best practices

1. **Zone counts are never hidden.** Deck, discard, exhaust. The player is counting outs — hiding a count converts strategy into guesswork.
2. **Hover-to-zoom is mandatory.** Card text is unreadable at hand size; a raised, scaled preview on hover is the genre's baseline interaction.
3. **The hand fan must adapt.** 3 cards spread wide, 10 cards overlap tightly. A fixed layout either wastes space or clips.
4. **Energy is discrete and countable.** Crystals/pips beat a number — the player counts what they can still cast.
5. **Enemy intent turns a gamble into a puzzle.** If enemies act predictably, show it. This is the biggest single readability win in modern deckbuilders.
6. **End Turn signals when it is safe.** Colour change or pulse when no plays remain prevents the most common misclick.
7. **Everything hoverable has a tooltip.** Relics, statuses, keywords. Card games are dense; tooltips carry the density.
8. **Animate zone transitions.** A card must be *seen* moving hand → discard, or players lose track of state.

---

## 5. Current state vs target

**Audited `cardgame_main.tscn` (2026-07-26):**

| have | status |
|---|---|
| Health / Gold / Energy / Hand / Deck / Discard labels in a `PanelContainer` | **the data model is right — six correct readouts** |
| `EnergyLabel` carried a hardcoded `font_size = 32` | fixed in Stage 25 — now the `BeepDisplay` role |
| `GenreScreenComponent` → `card_battle.tscn`, `collection.tscn`, `deck_builder.tscn` | full-screen screens exist |
| `HandLabel` reads `"Your Hand: 5 cards"` | **there is no hand — only a label saying how many cards there are** |

**Missing:** actual card rendering (P0), hand fan, hover-zoom, energy crystals, End Turn, portraits, intents, relics, targeting.

> This genre has the **largest gap between data and presentation**. `CardGameHudComponent` already tracks every number a card game needs; none of it is drawn as cards. Stage 30.7 is mostly new UI code here, not wiring.

---

## 6. Data contract

`CardGameHudComponent` currently binds:

```
health   gold   energy   hand   deck   discard
```

Stage 30.7 adds:

```
hand         list of { id, name, cost, art, text, playable bool }
energy       { current, max }
piles        { draw, discard, exhaust }
player       { hp, max_hp, armour, effects[] }
opponent     { hp, max_hp, armour, effects[] }
enemies      list of { id, hp, intent { icon, value } }
relics       list of { icon, name, description }
potions      list of { icon, name, usable bool }
turn         { number, is_player_turn }
```

---

## 7. Components

**Reuse (already built, never placed in a scene):**
`FlipCardComponent` (card reveal/flip) · `CarouselComponent` (hand scrolling fallback) ·
`TooltipComponent` (keywords, relics — essential here) · `BadgeComponent` (pile counts) ·
`DragComponent` (drag to play) · `CounterComponent` · `ModalComponent` (card-select prompts) ·
`ToastNotificationComponent` · `SafeAreaComponent`

**Build new:**

| component | responsibility |
|---|---|
| `HandLayoutComponent` | arc/fan layout, adaptive spread, hover raise + zoom, drag-out |
| `PileCounterComponent` | draw/discard/exhaust counter with click-to-view |
| `EndTurnButtonComponent` | state-aware colouring, keyboard binding |
| `IntentDisplayComponent` | per-enemy intent icon + value (roguelike deckbuilder) |

---

## 8. Pitfalls

- The hand is **fully mouse-interactive** — hover, drag, click. It cannot be `mouse_filter = Ignore`, so it must not overlap other interactive chrome.
- Fan layout is trigonometry per card (`angle = spread * (i - n/2)`), and z-order must follow hand order or cards clip wrongly.
- Hover-zoom must render **above** every sibling — either a dedicated high `CanvasLayer` or `top_level = true` on the zoom preview.
- `DragComponent` exists but was built for generic UI; verify it supports drag-to-world-target before assuming it covers card play.
- `EnergyLabel` now uses `BeepDisplay`. Do not reintroduce a hardcoded font size.

## 9. Collapsible panels

**Rule: every HUD panel in this genre is collapsible.** A HUD competes with the game for the
same pixels; each panel is useful *some* of the time, and occupying the screen for the rest is
how a HUD becomes clutter. Folding is the standard answer across the genre, and it costs
nothing to add — `CollapsiblePanelComponent` attaches as a child of a panel that already
exists, inserts a header above it, and folds the panel away leaving only that header.

| Panel | Header | Default | Note |
|---|---|---|---|
| `HandPanel` | Hand | expanded | never fold by default — it is the game |
| `LogPanel` | Log | collapsed | reference only |
| `DeckStats` | Deck | collapsed |  |
| `OpponentInfo` | Opponent | expanded |  |

### Rules

- **The header always survives the fold.** It is inserted as a *sibling above* the panel, not
  as a child — a child would be folded away with everything else, leaving no way back.
- **Collapsed means zero space, not just hidden.** The component drops
  `custom_minimum_size.y` to 0 and clears `visible`, so the container reflows and neighbours
  take the freed room. A merely-transparent panel still eats clicks.
- **State persists.** The component is `ISaveable` and joins the `saveables` group, keyed on the
  panel name (`hud.collapsed.<panel>`). A player who folded a panel expects it folded after a
  reload; a HUD that silently reopens everything on load is exactly the annoyance this removes.
- **Never default the genre's core loop to collapsed.** The panels marked *never fold by
  default* above are the ones a player reads continuously — hiding them behind a click makes
  the game worse, not cleaner.
- **Bind a key where the panel is toggled often** via `ToggleAction`; click-only is fine for
  the rest.

### Wiring

```
[node name="Collapse" type="Node" parent="HUD/Root/.../HandPanel"]
script = ExtResource("collapsible")
Title = "Hand"
ToggleAction = "ui_end"
```

Verified in the city builder slice: header renders `▾ Build` / `▸ Build`, the panel drops out of
the layout (neighbours reflow up into the space), the header stays clickable while collapsed,
and the component appears in the `saveables` group alongside the genre state component.
