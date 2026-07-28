# Strategy HUD (RTS / 4X)

**Genre id:** `strategy` · **Main scene:** `strategy_main.tscn` · **Genre HUD script:** `StrategyHudComponent`

---

## 1. Reference games

| game | what its HUD is known for |
|---|---|
| **Age of Empires II** | The archetypal RTS frame: resource strip across the top with **population `47/60`**, minimap bottom-left, selection portrait centre, **command grid bottom-right**. Idle-villager button is a genre-defining affordance. |
| **StarCraft II** | Same frame, tightened: **3×5 command card** with fixed hotkey positions (a unit's ability is *always* in the same cell), control-group tabs, production queue with progress rings, and a supply-blocked warning that turns the pop counter red. |
| **Command & Conquer** | Right-hand vertical sidebar instead of a bottom bar — build queue as a column of buttons with build progress drawn on the icon itself. |
| **Civilization VI** | 4X variant: no unit command card, instead a **next-turn button** as the primary control, research/culture progress bars top-left, and a notification stack down the right edge that must be cleared to end a turn. |
| **Total War** | Splits into campaign map (4X) and battle (RTS) HUDs — battle adds a unit card row with morale/fatigue per card. |

**The through-line:** an RTS HUD is a **command interface**. The player's hands live on the minimap and the command card. Resources are glanceable context; the command card is where the game is played.

---

## 2. Canonical layout

```
┌───────────────────────────────────────────────────────────────────────────┐
│ 💰1,240 (+12/t)  🌾880 (+9/t)  🪵640 (+4/t)  🪨120   👥47/60   Turn 34 · Age II│ top
├───────────────────────────────────────────────────────────────────────────┤
│ ┌────────┐                                                                │
│ │⚠ under │  alerts                                                        │
│ │ attack │                                                                │
│ └────────┘                (battlefield viewport)                          │
│                                                                           │
│                                                                           │
├──────────────┬────────────────────────────────┬───────────────────────────┤
│ ┌──────────┐ │  ┌────┐ Knight        ⚔ 12    │  ┌───┬───┬───┬───┐        │
│ │ minimap  │ │  │port│ ❤ 84/100     🛡  4    │  │ A │ M │ S │ P │        │
│ │  ▫ ▫  ▪  │ │  └────┘ Veteran               │  ├───┼───┼───┼───┤        │
│ │     ▪    │ │  [◱][◱][◱] production queue   │  │ H │ B │   │ ⏹ │        │
│ └──────────┘ │                                │  └───┴───┴───┴───┘        │
│              │                                │      command card         │
└──────────────┴────────────────────────────────┴───────────────────────────┘
```

**4X variant:** command card is replaced by a large **End Turn** button bottom-right; research + civic progress bars sit top-left; notifications stack down the right edge.

---

## 3. Element specification

| # | element | region | type | behaviour | priority |
|---|---|---|---|---|---|
| 1 | Resources | top strip | icon + amount + **rate `(+12/t)`** | rate is what enables planning; amount alone is not | **P0** |
| 2 | **Population `47/60`** | top strip | fraction | turns red when capped — the classic "supply blocked" tell | **P0** |
| 3 | Turn / Age / Era | top-right | text | 4X: also the phase indicator | P1 |
| 4 | **Minimap** | bottom-left | `MinimapComponent` | fog of war, unit blips by team colour, **camera viewport box**, click to jump, drag to pan | **P0** |
| 5 | **Selection panel** | bottom-centre | portrait + stats | 1 unit → portrait, HP, attack, armour, veterancy. Multi-select → grid of unit cards | **P0** |
| 6 | **Command card** | bottom-right | fixed 4×3 grid | ability icon + hotkey letter + cooldown; **position is stable per unit type** so muscle memory works | **P0** |
| 7 | Production queue | above selection | icon row + progress | first item shows a progress ring; click to cancel/refund | P1 |
| 8 | Alerts | left edge | toast stack | "under attack", "resource depleted"; click to jump | P1 |
| 9 | Idle worker | near minimap | button + count | AoE's signature quality-of-life affordance | P2 |
| 10 | Control groups | above minimap | numbered tabs `1..9` | shows group composition at a glance | P2 |
| 11 | **Next turn** (4X only) | bottom-right | large button | blocks while notifications are unresolved | **P0** (4X) |
| 12 | Research / civic | top-left (4X) | progress bars | turns remaining | P1 (4X) |

---

## 4. Genre best practices

1. **Command card positions are fixed per unit type.** Never reflow the grid. A StarCraft player presses `A` without looking; a reflowing grid destroys that.
2. **Every ability shows its hotkey on the button.** The keyboard is the primary input; the mouse is the fallback.
3. **Resource rate matters more than resource amount.** `(+12/turn)` is the planning number.
4. **Population cap must shout when hit.** Red text, and ideally an alert — being supply-blocked is the most common silent failure state in RTS.
5. **The minimap needs a camera box.** Without it the player loses spatial orientation. Click-to-jump and drag-to-pan are both expected.
6. **Multi-select shows unit cards, not a merged blob.** The player needs to see composition and pick out the wounded.
7. **Alerts must locate.** Same rule as city builder: clicking an alert moves the camera.
8. **The bottom bar is opaque and reserves screen space** — unlike a shooter HUD, an RTS HUD is furniture, and the viewport is sized to exclude it.

---

## 5. Current state vs target

**Audited `strategy_main.tscn` (2026-07-26):**

| have | status |
|---|---|
| Gold / Food / Wood / Units / Turn labels in a top `PanelContainer` | text only; **no rates, no population cap** |
| `MinimapComponent` | present; no fog, no camera box, no click-to-jump |
| `GenreScreenComponent` → `research.tscn`, `diplomacy.tscn`, `unit_panel.tscn` | full-screen modals on hotkeys |

**Missing:** command card (P0), selection panel (P0), production queue, resource rates, population cap, alerts, idle worker, control groups.

> `unit_panel.tscn` already exists as a screen and contains selection-style content. Like city builder's `build_menu`, it is a **modal that should be docked**. Stage 30.4 reuses its content inside `SelectionPanelComponent`.

---

## 6. Data contract

`StrategyHudComponent` currently binds:

```
gold   food   wood   units   turn
```

Stage 30.4 adds:

```
gold_rate / food_rate / wood_rate / stone     int, per turn or per minute
population        { current, cap }
era               string
selection         { portraits[], hp, attack, armour, veterancy }
commands          list of { icon, hotkey, cooldown, enabled }
production_queue  list of { icon, progress 0..1 }
alerts            list of { severity, text, world_position }
idle_workers      int
```

---

## 7. Components

**Reuse (already built, never placed in a scene):**
`TableComponent` (unit stats) · `ContextMenuComponent` (right-click orders) ·
`ToastNotificationComponent` (alerts) · `BadgeComponent` (idle count, group counts) ·
`ProgressRingComponent` (production progress) · `TooltipComponent` (ability descriptions) ·
`TabGroupComponent` (control groups) · `SafeAreaComponent`

**Build new:**

| component | responsibility |
|---|---|
| `CommandCardComponent` | fixed 4×3 ability grid, hotkey labels, cooldown overlay, stable positions |
| `ProductionQueueComponent` | icon row with progress + cancel |
| `SelectionPanelComponent` | **shared with `citybuilder`** — single portrait or multi-unit card grid |

---

## 8. Pitfalls

- The bottom bar **reserves space**: the game viewport must shrink, not be overlaid, or units under the bar are unclickable.
- The command card and the minimap both take mouse input — everything between them must be `mouse_filter = Ignore`.
- A 4×3 grid at 1280×720 with the theme's ~44px buttons needs ~200×140px; budget for it before sizing the selection panel.
- 4X and RTS want *different* bottom-right controls (next-turn vs command card). Drive this from `genre.json#tuning`, not a second scene.

## 9. Collapsible panels

**Rule: every HUD panel in this genre is collapsible.** A HUD competes with the game for the
same pixels; each panel is useful *some* of the time, and occupying the screen for the rest is
how a HUD becomes clutter. Folding is the standard answer across the genre, and it costs
nothing to add — `CollapsiblePanelComponent` attaches as a child of a panel that already
exists, inserts a header above it, and folds the panel away leaving only that header.

| Panel | Header | Default | Note |
|---|---|---|---|
| `SelectionPanel` | Selection | expanded | only meaningful with a selection |
| `Minimap` | Map | expanded | never fold by default — it is the genre's primary navigation |
| `ResourceBar` | Resources | expanded | small, but foldable for screenshots/cinematics |
| `BuildQueue` | Queue | collapsed | empty most of the time |

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
[node name="Collapse" type="Node" parent="HUD/Root/.../SelectionPanel"]
script = ExtResource("collapsible")
Title = "Selection"
ToggleAction = "ui_end"
```

Verified in the city builder slice: header renders `▾ Build` / `▸ Build`, the panel drops out of
the layout (neighbours reflow up into the space), the header stays clickable while collapsed,
and the component appears in the `saveables` group alongside the genre state component.
