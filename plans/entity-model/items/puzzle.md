# puzzle — no. Nothing. Not even a spec.

> Read `README.md` first. This doc conforms to its rule and its template.

**Puzzle earns the least of any genre in the set, and this doc is short because that is the
finding, not because the work was skipped.**

---

## 1. Does this genre need items at all?

**No.** The README's index already says *"**no** — board-driven"*, and the code agrees
completely. `ecs/ui/Match3BoardComponent.cs` holds the entire game in one field:

```
private int[,]? _grid;                                    // :27
```

Everything is an `int`. Swap, match-detect, clear, gravity, refill, cascade — all of it reads
and writes that array. There is no entity anywhere in the genre.

### The board is complete

`Match3BoardComponent` is, unusually for this repo, **finished**:

| Behaviour | Where |
|---|---|
| swap with adjacency check | `Swap(int ax, int ay, int bx, int by)` (`:70-77`), rejects non-adjacent via `Mathf.Abs(ax-bx)+Mathf.Abs(ay-by) != 1` (`:73`) |
| match detection, 3+ runs, H and V | `FindMatches()` (`:106-139`) |
| clear | `ClearCells()` (`:141-145`) |
| gravity | `ApplyGravity()` (`:147-165`) |
| refill | `Refill()` (`:167-179`) |
| cascade until stable | `ResolveCascades()` — `while(true) { … if (matches.Count == 0) break; }` (`:79-104`) |
| match-free seeding | `InitGrid()`'s `do { … } while (FormsMatchAt(x, y, g));` (`:58-61`) |
| per-level difficulty ramp | `_Ready()` grows `GridWidth`/`GridHeight` to a cap of 12 and `GemTypeCount` to 8 by `GameApp.CurrentLevel` (`:37-46`) |
| reentrancy guard | `_resolving` (`:29`, checked `:72`) |

It also reads `GameInfo.GridWidth`/`GridHeight` (`:34-35`), which `BeepGenreGenerator`
populates from `catalogs/skins/puzzle/genre.json`'s `grid_width: 8` / `grid_height: 8`
(`core/BeepGenreGenerator.cs:185-186`). Unlike every other genre in this set, puzzle's tuning
keys are **real** — `grid_width`, `grid_height`, `target_score` are all in `KnownTuningKeys`
(`:221-231`).

### And it is headless and inert

Verified by grep over `addons/` excluding the component's own file:

- **`Swap()` has 0 callers.** Nothing takes input. No click, no drag, no keyboard.
- **Nothing subscribes to `CellChanged`** (`:24`, emitted `:190-193`). Nothing renders. There
  is not a gem on screen.
- **Nothing subscribes to `MatchesCleared`** (`:25`) or the board's **`ScoreChanged`** (`:23`,
  emitted `:101`).

That last one is the sharpest. `templates/scenes/puzzle_main.tscn` ships:

- `Match3Board` (`:17-18`), a bare `Node` under `Board`, with **no sibling and no listener**;
- `GameFlow` (`:42-43`) — `ecs/GameFlowComponent.cs`, which has its **own** `ScoreChanged`
  (`:44`), its own `Score`, and fires `LevelComplete` at `if (Score >= TargetScore && TargetScore > 0)`
  (`:81`);
- `Hud` (`:45-49`) — `ecs/ui/HudComponent.cs`, which binds **`flow.ScoreChanged`**
  (`:45`), i.e. `GameFlowComponent`'s, not the board's;
- `ScoreLabel` `text = "0"` (`:37`) and `TargetLabel` `text = "Target: 1000"` (`:40`) — both
  static strings.

So there are **two unconnected `ScoreChanged` signals in one scene**. The board scores into a
private `_score` (`:28`) that reaches nobody; the HUD and `GameFlow` listen to each other.
`GameFlowComponent.TargetScore` is correctly loaded from `GameInfo` (`:63`) — **and can never
be reached**, because nothing ever calls `GameFlow.AddScore`. `target_score: 1000` is wired all
the way from JSON to the win check, and the win check is unreachable.

The genre's problem is a **missing edge**, not a missing class. This is the same species as
README § *What is already known* — *"`Pickup.Collected → Inventory.AddItem`: 0 connections"* —
and no amount of new data fixes it.

---

## 2. The tree

**There is none.** Zero branches. Zero `.tres` files. Nothing to author.

### Is a gem a `GameItem`?

No, and not for a close reason. **A gem is a VIEW, not an entity.** It has no existence outside
`_grid[x, y]`. It is an `int` between 1 and `GemTypeCount` (`Refill`, `:176`), and `0` means
empty (`ClearCells`, `:144`; tested `:155`). Against the spine (README § *The spine*):
`MaxStack` — no; `IsStatic` — no; `IsDestructible` — a gem is *cleared*, not damaged;
`Rarity` — no; `WorldScene` — **no**: there is no per-gem node, and the class doc is explicit
that *"gem rendering is left to the game"* (`:9-10`).

**Zero components belong on a gem.** Not `HealthComponent`, not `PickupComponent`, not
`DestructibleComponent`. The README's traits table exists to decide what a world instance is
built from; a gem **has no world instance**. There is nothing for the table to decide.

---

## 3. New framework classes this genre earns

### **None.** This is the answer.

Puzzle earns nothing. Not a `GameItem` subclass, not a sibling spec, not a `Resource` of any
kind.

### `GemSpec : Resource` — **considered, and NOT earned**

The tempting argument: `GemTypeCount = 5` (`:20`) is a bare int, so gem type `3` is nameless
and textureless. Replace it with `[Export] GemSpec[] GemTypes` and each gem gains a `Texture2D`,
a score multiplier, maybe a special behaviour. That is how a mature match-3 works.

Against, and it wins:

1. **Two of its three fields have no consumer, today or in the plan.** `Texture` — nothing
   renders (0 `CellChanged` subscribers, §1), so the texture would be read by nothing. Score
   multiplier — the board's `ScoreChanged` reaches nothing (§1), so a multiplier would multiply
   a number nobody sees. MASTER_TODO § *What the analysis changed* is explicit that *"a field
   that silently does nothing is this repo's signature defect"*; `GemSpec` would be three of
   them at once.
2. **It would break the level ramp.** `_Ready` does
   `GemTypeCount = Mathf.Min(GemTypeCount + (level - 1) / 2, 8)` (`:45`). An `int` scales; an
   authored array does not — level 6 would need a 7th `.tres` to exist, and the whole ramp
   (`:37-46`), the genre's only real progression, becomes an authoring burden. The `int` is not
   a shortcut. It is the **right representation** for a value the game grows at runtime.
3. **"Special behaviour" is not a spec field.** A bomb gem that clears a row is `FindMatches`
   and `ClearCells` logic — a change to the board's algorithm, not a value on a `.tres`. Putting
   `IsBomb : bool` on a resource that no code branches on is a lie in the inspector.
4. **The spine test fails.** README § *The one rule*: a class earns existence by adding a field
   or behaviour its parent cannot express. `GemSpec` has no parent to out-express — it would be
   a bare `Resource` whose only justification is "the other genres have one". That is the
   corollary's exact target: *"a genre never gets a class just for being that genre."*

**Revisit when, and only when, `CellChanged` has a renderer.** At that point `Texture` has a
consumer and the argument changes — the field would be earned by an actual reader. Not before.
**UNCERTAIN** whether the ramp objection (2) survives that; it may force gems to stay `int`
permanently, with textures in a lookup array on the renderer rather than a spec. That is a
Phase-5 question with real evidence, not a guess to make now.

### Anything else — no.

No `BoardSpec` (the board reads `GameInfo.GridWidth`/`GridHeight` already, `:34-35`, and
`GameInfo` is already a `Resource`, `core/GameInfo.cs:15`). No `LevelSpec` (the ramp is
computed from `GameApp.CurrentLevel`, `:40`; a level is an int). No `PowerUpSpec` (no power-ups
exist; inventing one is inventing a kind to fill a table).

---

## 4. Components this implies

### Serves already

| Component | Role | State |
|---|---|---|
| `ecs/ui/Match3BoardComponent.cs` | **the entire genre model** | complete, and inert — 0 callers on `Swap`, 0 subscribers on any signal (§1) |
| `ecs/GameFlowComponent.cs` | score, lives, `LevelComplete` at `TargetScore` (`:81`) | in `puzzle_main.tscn:42`, correctly fed from `GameInfo` (`:63`), and never told about a match |
| `ecs/ui/HudComponent.cs` | score label | in `puzzle_main.tscn:45`, bound to `flow.ScoreChanged` (`:45`) — the wrong one of the two |
| `ecs/scenes/puzzle/*` | `LevelMap.cs`, `PreLevel.cs`, `LevelComplete.cs`, `LevelFailed.cs` | screens; they navigate |

### Forced new — by the tree

**None — the tree is empty.** The tree forces nothing because there is no tree. That is the
correct output of section 4 for this genre, and padding it would be dishonest.

### What the genre actually needs (not an item-model concern)

Two edges and a renderer, none of which this initiative owns:

1. an input source calling `Swap()` (`:70`);
2. a renderer subscribing to `CellChanged` (`:24`);
3. `board.ScoreChanged → GameFlow.AddScore`, so `TargetScore` becomes reachable.

Recorded here so the finding is not lost. **Not designed here** — they are wiring and
rendering, and `CLAUDE.md` § *Scope* plus the README's template keep this document to data.

### Explicitly must not have

Every gameplay component. No `HealthComponent`, `MovementComponent`, `AttackComponent`,
`PickupComponent`, `InventoryComponent`, `DropTableComponent`. A puzzle scene that grows any of
them has misunderstood the genre. Note the README's traits table cannot derive this one — it
derives composition **from an item's traits**, and there is no item. Puzzle's `MUST NOT HAVE`
is absolute and comes from the genre, not the data (a Phase-5 note).

---

## 5. Content vs framework

**We ship (framework):**

- `Match3BoardComponent` — already shipped, already complete. **Change nothing about its data
  model.**
- Nothing else. **No new classes.**

**The developer authors (content):**

- The gem renderer — subscribe to `CellChanged`, draw whatever they like. The component's own
  doc already delegates this (`:9-10`).
- The input mapping — click, drag, touch — calling `Swap`.
- Level count, target scores, the ramp's feel.
- Art.

Per `CLAUDE.md` § *Scope*: framework only.

---

## The one-line answer

**Puzzle needs no items, no specs, and no new classes. It has a complete board and no wires.**
The correct entity-model deliverable for this genre is a **zero** — and recording that honestly
is worth more than a `GemSpec` nobody reads.
