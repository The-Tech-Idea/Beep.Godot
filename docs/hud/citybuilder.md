# City Builder HUD

**Genre id:** `citybuilder` · **Main scene:** `citybuilder_main.tscn` · **Genre HUD script:** `CityBuilderHudComponent`

---

## 1. Reference games

| game | what its HUD is known for |
|---|---|
| **Cities: Skylines** | The definitive modern layout. Thin top info-strip; a wide **bottom build toolbar** with category tabs that expand into item palettes; **RCI demand bars** always visible on the right; info-view overlay buttons beside the minimap; a "chirper" event feed. |
| **SimCity 4** | Originated the **RCI meter** (residential / commercial / industrial demand as diverging bars) and the advisor system. Budget is a *monthly delta*, not a balance. |
| **Anno 1800** | Population split into **social tiers** (farmers → workers → artisans), each with its own satisfaction and needs. Production-chain tooltips on every building. |
| **Frostpunk** | Ring-shaped time control at bottom-centre; **Hope and Discontent** as the two meters that actually end the game; heat as a spatial overlay rather than a number. |
| **Tropico 6** | Political-faction approval meters; separate treasury vs. Swiss-account balances. |

**The through-line:** a city builder HUD is not a status readout. It is a **toolset**. The player spends the whole session in the build toolbar and the info-view overlays. Numbers exist to tell them *which tool to reach for next*.

---

## 2. Canonical layout

```
┌───────────────────────────────────────────────────────────────────────────┐
│ 💰 52,400  ▲+1,020   👥 250   ⚡120/150   🙂72%    Yr1 · Spring  ‖ ▶ ▶▶ ▶▶▶│  top bar
├───────────────────────────────────────────────────────────────────────────┤
│                                                                      ┌───┐│
│                                                                      │ R ││  RCI
│                                                                      │ C ││  demand
│                        (city viewport)                               │ I ││
│                                                                      └───┘│
│  ┌──────────────────────┐                                                 │
│  │ ⚠ Fire — District 3  │  alerts feed                          ┌────────┐│
│  │ ⚠ No power — Zone B  │                                       │minimap ││
│  └──────────────────────┘                                       │        ││
│                                                                 └────────┘│
│                                                                 [🚦][☁][⚡]│  info views
├───────────────────────────────────────────────────────────────────────────┤
│ [Zones][Roads][Services][Utilities]                                       │  build
│  🏠 House 1,200   🏭 Factory 6,500   🌳 Park 800   ...                     │  toolbar
└───────────────────────────────────────────────────────────────────────────┘
```

When a building is selected, the build toolbar is replaced by a **selection panel** (name, upkeep, output, upgrade, demolish).

---

## 3. Element specification

| # | element | region | type | behaviour | priority |
|---|---|---|---|---|---|
| 1 | Treasury + **monthly delta** | top-left | text + coloured delta | delta green/red; the delta is what the player watches, not the balance | **P0** |
| 2 | Population | top bar | icon + number | tick up/down animated; trend arrow | P1 |
| 3 | Power / Water | top bar | `used / capacity` + bar | bar turns amber >85%, red at cap | P1 |
| 4 | Happiness | top bar | percent + face icon | face changes at thresholds | P2 |
| 5 | Date | top-right | `Yr 1 · Spring` | advances with sim speed | P2 |
| 6 | **Speed controls** | top-right | 4 toggle buttons `‖ ▶ ▶▶ ▶▶▶` | pause is the most-pressed control in the genre | **P0** |
| 7 | **Build toolbar** | bottom, full width | category tabs → item palette | each item: icon, name, cost; disabled + greyed if unaffordable | **P0** |
| 8 | **RCI demand** | right edge | 3 diverging bars | tells the player which zone to paint next; the single most useful number on screen | **P0** |
| 9 | Minimap | bottom-right | `MinimapComponent` | click to jump camera | P1 |
| 10 | **Info-view overlays** | beside minimap | toggle buttons | traffic / pollution / land value / power / water — recolour the world | **P0** |
| 11 | Alerts feed | left, above toolbar | toast stack | fire, no power, no water, complaints; click to jump to location | P1 |
| 12 | Selection panel | replaces toolbar | panel | upkeep, output, upgrade, demolish | P1 |
| 13 | Milestone progress | top bar | small bar | next unlock threshold | P3 |

---

## 4. Genre best practices

1. **Budget is a delta, not a balance.** `52,400` alone is meaningless; `▲ +1,020 / month` is the number that drives decisions. Show both, emphasise the delta.
2. **The toolbar is the game.** It must be reachable in one click, always, and must never be covered by an overlay. Categories stay visible when a palette is open.
3. **Unaffordable items are greyed, not hidden.** Hiding them destroys the player's sense of progression.
4. **RCI is always on screen.** It is the feedback loop for the core verb (zoning). Hiding it behind a panel breaks the loop.
5. **Alerts must be clickable and must locate.** A warning the player cannot act on is noise. Clicking an alert moves the camera.
6. **Info-views recolour the world, not the HUD.** The HUD button is a toggle; the data lives in the viewport.
7. **Pause is sacred.** The player pauses to plan. Bind it to space *and* the button, and show the current speed state clearly.
8. **Never block the viewport centre.** All chrome hugs the edges — the city is the content.

---

## 5. Current state vs target

**Audited `citybuilder_main.tscn` (2026-07-26):**

| have | status |
|---|---|
| Population / Budget / Power / Happiness / Date labels in a top `PanelContainer` | text only, no bars, no delta |
| `MinimapComponent` bottom-right | present, no click-to-jump, no info-views |
| `GenreScreenComponent` x3 (build / economy / districts) | opens `build_menu.tscn` as a **full-screen overlay** on a keypress |
| `Districts.cs`, `Economy.cs` | **17-line Close-only mockups** — every figure on them (`+2,400`, `Happiness 78%`) is a scene literal |
| `CityBuilderHudComponent` | all five stats bind via `Placeholder(...)` — **no real data source for any of them** |

**Missing:** build toolbar (P0), RCI demand (P0), speed controls (P0), info-views (P0), alerts feed, selection panel, budget delta.

> **Key restructure:** `build_menu.tscn` already contains the category list and item grid, but it is a *full-screen modal opened by a hotkey*. In this genre that content belongs **docked at the bottom, always visible**. Stage 30.3 should reuse its content and re-host it in `BuildToolbarComponent`, keeping the modal as an optional "expanded" view.

---

## 6. Data contract

`CityBuilderHudComponent` already binds these via `SetStat(key, value)`:

```
population   budget   power   happiness   date
```

Stage 30.3 adds:

```
budget_delta      int, signed, per month
water             "used/capacity"
demand_r/c/i      float -1..+1  (diverging bar)
speed             0..3          (paused / 1x / 2x / 3x)
milestone         float 0..1
alerts            list of { severity, text, world_position }
selection         { name, upkeep, output, upgrade_cost }
```

---

## 7. Components

**Reuse (already built, never placed in a scene):**
`TabGroupComponent` (toolbar categories) · `ToastNotificationComponent` (alerts) ·
`TooltipComponent` (building costs/needs) · `ContextMenuComponent` (right-click demolish) ·
`TableComponent` (selection stats) · `SafeAreaComponent` · `BadgeComponent` (alert counts)

**Build new:**

| component | responsibility |
|---|---|
| `BuildToolbarComponent` | docked bottom bar, category tabs → item palette, affordability greying |
| `DemandMeterComponent` | RCI diverging bars |
| `GameSpeedComponent` | pause/1x/2x/3x, owns the sim tick rate |
| `InfoViewComponent` | overlay toggles, recolours the world layer |
| `SelectionPanelComponent` | **shared with `strategy`** |

---

## 8. Pitfalls

- The build toolbar **accepts mouse input**; everything else in the HUD must be `mouse_filter = Ignore` or the toolbar will fight the viewport for clicks.
- The bottom toolbar and the minimap both want the bottom-right corner — dock the minimap **above** the toolbar, not beside it, or they overlap at 1280×720.
- `GenreScreenComponent` pauses the tree while its screen is open (`PauseWhileOpen = true`). A docked toolbar must **not** pause — only the expanded modal should.
- RCI bars diverge from a centre line (negative = oversupply). A plain `ProgressBar` cannot express this; `DemandMeterComponent` needs a custom `_Draw`.

## 9. Collapsible panels

**Rule: every HUD panel in this genre is collapsible.** A HUD competes with the game for the
same pixels; each panel is useful *some* of the time, and occupying the screen for the rest is
how a HUD becomes clutter. Folding is the standard answer across the genre, and it costs
nothing to add — `CollapsiblePanelComponent` attaches as a child of a panel that already
exists, inserts a header above it, and folds the panel away leaving only that header.

| Panel | Header | Default | Note |
|---|---|---|---|
| `BuildBar` | Build | expanded | the session is spent here, but it is also the biggest screen consumer |
| `Minimap` | Map | expanded | wanted while planning, dead weight while zoomed in |
| `DemandMeter` | RCI | expanded | glanceable; folds once the player has internalised demand |
| `Alerts` | — | n/a | **not collapsible**: `Alerts` is a Node-based `ToastNotificationComponent`, not a panel Control. Toasts are transient and self-dismissing — there is no persistent rect to fold. Cap the queue instead. |

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
[node name="Collapse" type="Node" parent="HUD/Root/.../BuildBar"]
script = ExtResource("collapsible")
Title = "Build"
ToggleAction = "ui_end"
```

Verified in the city builder slice: header renders `▾ Build` / `▸ Build`, the panel drops out of
the layout (neighbours reflow up into the space), the header stays clickable while collapsed,
and the component appears in the `saveables` group alongside the genre state component.

## 10. Visual language — from the reference art

Derived from `Example_Art/citybuilder1..5.png` (2026-07-26). These are the shipped-game
references this HUD is measured against, and the current implementation violates most of what
follows. Recorded concretely so the gap is actionable rather than a matter of taste.

### What every reference does

| # | Rule | What we do today |
|---|---|---|
| 1 | Resources are **individual capsule badges** — a circular icon frame overhanging a rounded plate holding the number | one 1280×56 `TopBar` strip with a row of icon+label pairs |
| 2 | **Icon-first**: large framed icon, small number beside it | text-first, 13–16px labels next to 20px icons |
| 3 | **Corner clusters, empty middle** — TL player, TR resources, BL tools, BR map/shop | full-width top bar + full-width bottom bar |
| 4 | **Vertical icon rails** down the side edges (refs 2, 5) | none |
| 5 | **Chunky dark outline + drop shadow**, saturated fill; every element reads as an object | thin hairline, translucent grey glass |
| 6 | Build palette = **icon tile with caption underneath**, or a strip of **circular** icon buttons | `"House ×3 / 1,200"` text in a rectangle |
| 7 | Category tabs are **icons**, not words | text tabs ("Zones", "Roads"…) |
| 8 | Round shapes throughout — icon frames, radar, avatars | rectangles throughout |

### The badge — the defining element

A resource readout is *not* a label. It is:

```
   ( ◉ )──────────────╮      circular icon frame, ~40px, overhangs the plate's left edge
   │      4 750       │      rounded capsule plate, dark fill, 2-3px dark outline, drop shadow
   ╰──────────────────╯      value right-aligned, bold, high contrast
```

Refs 1 and 5 add a fill bar inside the capsule (`max: 8 000`), so the badge doubles as a
capacity meter. Ref 3 keeps the same anatomy at half the weight — smaller circle, thinner
outline, lighter plate — which is the "minimal" variant, **not** a flat grey bar.

### Layout regions (ref 5 is the clearest)

- **TL** player avatar + level ring + XP bar
- **TR** resource badge column, right-aligned, stacked vertically
- **L rail** vertical square icon buttons (stats, quests, mail)
- **R rail** vertical square icon buttons (build, move, settings)
- **BL** radar/minimap in a round frame
- **BC** the action bar — square icon tiles, icon over caption, 2 rows
- **BR** shop / promo

### Consequences for this HUD

- [ ] Replace the `TopBar` strip with a `ResourceBadge` component and place badges TR, not in a bar
- [ ] Build palette items become icon tiles (icon + caption), not text rows
- [ ] Category tabs become icon tabs
- [ ] Chrome: thicker outline, real drop shadow, saturated fill — the current sci-fi glass is
      the wrong register for this genre (it suits shooter/strategy, which is where it came from)
- [ ] Minimap into a round frame, BL or BR
- [ ] Add the L/R icon rails for screen navigation
