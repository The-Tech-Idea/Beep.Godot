# Examples — the addon, demonstrated

Two examples. The first shows **what the addon does**; the second shows **its components in
motion**. Run them windowed — `--headless` uses the dummy renderer and draws nothing.

---

## 1. `style_showcase/` — start here

```
godot --path . examples/style_showcase/showcase.tscn
```

Pick any of the **10 genres** and any of its **5 themes** and watch every widget restyle.

The left panel is ordinary kit widgets — a button, a checkbox, a toggle, a slider, a dropdown, a
meter. **Not one of them names a colour, a corner radius, an outline weight or a font.** The right
panel prints what the selected theme actually resolved to, so you can see the declaration and its
consequence side by side:

| axis | what changes |
|---|---|
| `register` | Carved · Casual · Technical · **Pixel** — decides outline weight, AA, corner construction, font and shadow *together* |
| `shadow` | None · Hard · Soft · Glow · Extrude |
| `gloss_style` | soft sheen · discrete band · curved glass |
| `outline_shade` | `> 1` a bright carved rim, `< 1` a thick dark outline |
| `font` · `upper_case` · `tracking` | 8 families across the catalog; missing faces **warn** |
| `corner` | per widget **class**, in theme units — not per widget size |
| `shear` · `wobble` | silhouette modifiers |
| `grain` | the tiling material mask — 9 materials |
| `edge_run` | constructed frame: weight changes, gaps, blocks, hatch, ticks |
| `select` | selection cues as a **set**, per widget class |

**Every one of those is settable from `catalogs/skins/<genre>/themes/<name>/theme.json` in a `kit`
block, with no C# at all.** That is the whole result of the style-system work: `topdown/classic`
draws stepped corners in a bitmap face with no shadow; `shooter/cyberpunk` shears its plates, glows,
letter-spaces its caps and carries a 12-segment constructed frame; `rpg/fantasy` is carved wood with
spiked silhouettes. Same widgets, same scene, one dropdown.

`shot.tscn` renders five genre/theme pairs to `tmp/showcase/` — because a green log line does not
prove anything was *visible*.

---

## 2. `topdown_arena/` — the components in motion

```
godot --path . examples/topdown_arena/ui/main_menu.tscn   # menu -> game -> pause -> result
godot --path . examples/topdown_arena/smoke.tscn          # prove it still plays
```

**WASD / arrows** to move · **Esc / P** to pause. Collect all 14 coins; five chasers cost health on
contact.

Everything is an **authored scene** you can open, select and drag:

```
arena.tscn          13 ArenaWall blocks, 14 coins, 5 enemies, player, 3 UI layers
entities/           player · enemy · coin        components wired as child nodes
ui/                 main_menu · hud · pause · result      kit widgets + ThemePresetComponent
```

| component | from | doing |
|---|---|---|
| `TopDownController` | `ecs/` | eight-way movement, acceleration, friction |
| `HealthComponent` | `ecs/` | `TakeDamage(GameDamage)`, `Died` signal |
| `AIController` | `ecs/` | `Chase` against the `players` **group** |
| `KitPanel` · `KitMeter` · `KitPushButton` | `ecs/ui/kit/` | every screen |
| `ThemePresetComponent` | `ecs/ui/` | colour and type hierarchy per subtree |

Three things worth copying:

- **Enemies find the player through a group, not a reference.** `AddToGroup("players")` plus
  `AIController.TargetGroup = "players"` is the entire contract.
- **Nodes are resolved by NAME**, never by path from the root. A path hard-codes the layout — that
  is exactly how the addon's own menus broke when a `Margin` container was inserted.
- **Input actions are registered at runtime.** `project.godot` in *this* repo defines none;
  `BeepInputMapGenerator` writes them into a *generated* project. Without that the demo would load,
  render perfectly, and not respond — the most confusing way for an example to fail.

---

## Verifying

```
godot --path . examples/load_all.tscn                 # every example scene instantiates
godot --path . examples/topdown_arena/smoke.tscn      # the game is playable
godot --path . examples/style_showcase/shot.tscn      # the skins render and differ
```

The smoke test drives the game and asserts consequences, because *"it started with no errors"* is
nearly worthless — a scene that renders nothing, or a player frozen by a missing input action,
produces exactly the same clean log:

```
arena:  built  walls=13 enemies=5 coins=14
arena:  ok   moved 104.1px while move_right held (want > 40)
arena:  ok   stopped at x=771, right wall at x=1266 (want blocked)
arena:  ok   nearest enemy 128 -> 27px (closed 101, want > 30)
arena:  ok   coin collected on contact
arena:  PASS
```

> That chase line was wrong twice before it was right. It first sampled where the previous test had
> left the player — with an enemy already 27px away, *inside* `AIController`'s `AttackRange` of 26,
> where it deliberately stops. "closed 0" was correct behaviour and a broken test. The precondition
> is now asserted rather than assumed.
