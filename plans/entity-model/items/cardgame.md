# cardgame — GameItem tree

> Conforms to `README.md`. The rule: **a class earns existence only by adding a FIELD or
> BEHAVIOUR its parent cannot express.**

---

## 1. Does this genre need items at all?

**No.** It needs a **card**, and a card is not a `GameItem`.

This genre is the index's clearest "no items; needs a **card**" row (`README.md:112`), and the
evidence is that it has switched off the world the item model presupposes:

- **Every world system is disabled.** `enable_weather`, `enable_day_night`, `enable_seasons`,
  `enable_temperature`, `enable_forecast` — **all `false`**
  (`catalogs/skins/cardgame/genre.json:31-37`). No other genre in the repo turns off all five.
- **There is no world.** `cardgame_main.tscn` is 58 lines: a `Background` `ColorRect`, a
  placeholder `Label` reading *"Card Game gameplay placeholder"* (`:24`), a `HUD`, a
  `GameFlowComponent` (`:47`), and two `GenreScreenComponent`s (`:50-58`). **No
  `LevelContainer`, no `LevelLoaderComponent`** — compare `shooter_main.tscn:21-27` and
  `platformer_main.tscn:20-26`, which both have them.
- **There are no levels.** `templates/scenes/levels/` holds `platformer, racing, rpg, shooter,
  survival, topdown`. **`levels/cardgame/` does not exist** (verified: the directory is absent).
- **The presentational tuning is inert.** `hand_limit: 10`, `card_hover_scale: 1.12`,
  `card_fan_angle: 18` (`genre.json:27-29`) appear **nowhere else in the codebase** except a
  generator comment that names them as the example of decoration: *"Several genres ship blocks
  that look like configuration but are decoration (cardgame's `hand_limit`…)"*
  (`core/BeepGenreGenerator.cs:232-234`). None is in `KnownTuningKeys`
  (`BeepGenreGenerator.cs:221-231`), so `WarnUnknownTuning` actively `PushWarning`s on all three
  at generate time (`:236-243`). The `HandLimitLabel` in the HUD is static text — `"Hand: 0 / 10"`
  (`cardgame_main.tscn:41-42`) — and no `HudComponent` is in the scene to update it.

---

## 2. The tree

**Spine branches used: none.** This genre draws from **no** branch of `GameItem`.

### Why a card is not a `GameItem` — the argument

Take `GameItem`'s fields (`README.md:36-40`) one at a time against a card:

| `GameItem` field | On a card |
|---|---|
| `IsStatic` — "stays put (anvil, chest, rock) vs carried" | **Meaningless.** A card is neither. It is never in a world to stay put in — there is no world (§1). |
| `IsDestructible` | **Always `false`.** A card is not broken; it is discarded, exhausted, or burned as a *rules* effect, which is not `HealthComponent` reaching zero. |
| `MaxDurability` | **Dead** — `README.md:39` says it is "meaningful only when `IsDestructible`". |
| `WorldScene : PackedScene?` — "how it exists as a node, when it does" | **Always `null`.** A card exists as a `Control` in a hand or a grid, not a `Node2D` in a scene. |
| `MaxStack` | **Wrong shape.** A deck's "3× Strike" is a deck-list count, not an inventory stack. |
| `Id, DisplayName, Description, Icon, Rarity` | **Genuinely shared.** |

So inheriting `GameItem` would hand every card **four fields it must permanently leave
false/null** and one that means something else — in exchange for five it wants. That is the
**inverse** of the rule: subclassing must *add* what the parent cannot express
(`README.md:12-13`), not inherit what the child must suppress.

The traits table settles it (`README.md:56-64`): its whole point is that `IsStatic` /
`IsDestructible` say **what the world instance may be built from**. A card has no world
instance. The load-bearing half of `GameItem` is exactly the half a card cannot use.

**And the composition test agrees.** `MASTER_TODO.md:50-52`: a sword is a `Resource` *and* a
node, and "this one can carry components". A card's node is a `Control` in a `GridContainer`
(`deck_builder.tscn:47-69`) — its components would be `UIComponent`s, not
`GameplayComponent`s. Different half of the framework entirely.

### What it is instead: a sibling

```
Resource
  ├── GameItem : Resource        (the spine — README.md:35)
  └── GameCard : Resource        ← SIBLING, not a subclass

GameCard
    Id, DisplayName, Description, Icon, Rarity     ← the 5 it shares with GameItem
    Cost        : int             — mana/energy to play.  No GameItem field means this.
    CardType    : CardType        — Attack / Skill / Power / Curse
    TargetMode  : TargetMode      — Self / SingleEnemy / AllEnemies / None
    RulesText   : string          — the printed text
    EffectId    : string          — what playing it does
    ArtFront, ArtBack : Texture2D — FlipCardComponent needs two faces
```

`Cost` alone passes the rule outright: **no `GameItem` field can express "costs 2 energy to
play"** — that is not a price, not a weight, not a durability.

> **UNCERTAIN — a Phase-1 decision, flagged not made here.** `GameCard` and `GameItem` share
> five fields (`Id, DisplayName, Description, Icon, Rarity`). That is either (a) acceptable
> duplication across two independent roots, or (b) the signal that a common ancestor —
> `GameDefinition : Resource { Id, DisplayName, Description, Icon, Rarity }` — should be hoisted,
> with `GameItem` and `GameCard` both extending it. **Cardgame is the genre that raises the
> question**, and `README.md:114-117` predicts it: "half of these genres … want a **spec** …
> That is the same idea one level over." Racing's vehicle spec and citybuilder's building
> footprint will hit the same five fields. This doc recommends **deferring the hoist until a
> second sibling actually lands** — one sibling is not a hierarchy.

### The `.tres` set a developer would author

```
card_strike.tres      Cost = 1,  CardType = Attack,  TargetMode = SingleEnemy
card_defend.tres      Cost = 1,  CardType = Skill,   TargetMode = Self
card_fireball.tres    Cost = 2,  CardType = Attack,  TargetMode = AllEnemies
card_heal.tres        Cost = 1,  CardType = Skill,   TargetMode = Self
card_rage.tres        Cost = 0,  CardType = Power,   TargetMode = Self
```

Five cards, same fields, different numbers — `.tres`, exactly as `sword_iron` / `sword_steel`
are (`README.md:19`). **There is no `CardStrike` class**, and no `AttackCard : GameCard` —
`CardType` is a field, not a subclass, for the same reason `ItemType`'s string is deleted and
"the class is the type" only when the *fields* differ (`phase-1-item-resources.md:129-131`).

### Does a **deck** earn a class?

`GameDeck : Resource { GameCard[] Cards }` would add a field `GameCard` cannot express — a card
is not a list of cards. It passes the rule **on form**. But **nothing would read it**:
`DeckBuilder.cs` does exactly one thing — wires `StartBattleButton` (`:13`); the six
`CardSlot` nodes are empty `PanelContainer`s (`deck_builder.tscn:53-69`); `hand_limit` is
inert (§1). A resource nothing consumes is this repo's signature defect
(`MASTER_TODO.md:123-125`, and the reason `Range` was kept off `GameWeapon`,
`phase-1-item-resources.md:138`).

**Recommendation: not yet.** A deck is `[Export] GameCard[] Cards` on the future
`DeckComponent`. Promote it to `GameDeck` when a second consumer wants to name and save one.

---

## 3. New framework classes this genre earns

**Exactly one: `GameCard : Resource`.**

- **It is a sibling of `GameItem`, not a subclass** — argued in §2.
- It earns existence on `Cost`, `CardType`, `TargetMode`, `RulesText`, `EffectId` — five fields
  no `GameItem` in the spine carries or should.
- **Not** `GameDeck` (§2 — no consumer).
- **Not** a class per card type. `card_strike.tres` ≠ `StrikeCard` (`README.md:19,23-24`).
- **Not** a `CardgameItem`. "A genre never gets a class just for being that genre"
  (`README.md:26-28`).

This is the genre `README.md:117` had in mind: *"Where a genre says 'no items', the doc says
what it wants **instead**, and does not force it into `GameItem`."*

---

## 4. Components this implies

**Existing, and how little of it works:**

- **`FlipCardComponent` (`ecs/ui/FlipCardComponent.cs`) is the only card-shaped component in
  the addon** — and it is **used in zero scenes**. Grepped `FlipCardComponent` across all
  `.tscn` and `.cs`: the only hit is its own declaration (`:11`). It requires a `Container`
  parent (`:28`) with **≥ 2 `Control` children** (`:30-31`); the shipped card slots are **empty
  `PanelContainer`s** (`deck_builder.tscn:53-69`, `collection.tscn:53-69`), so even if it were
  attached it would resolve `_front`/`_back` to `null` and **silently do nothing** — `Flip()`
  scales the container and swaps two nulls (`:42-50`). Its `ArtFront`/`ArtBack` demand is why
  `GameCard` carries both textures.
- **`CardBattle`'s turn machine is real and has no card layer under it.** `TurnChanged(bool
  playerTurn, int turnNumber)` (`ecs/scenes/cardgame/CardBattle.cs:10`), the hand-off, the
  re-entrancy guards (`:41,54`) all work; the doc comment is candid — *"The turn STATE machine
  is real and complete; the per-card actions are the game's"* (`:29-31`). `HandLabel` is static
  text: `"Your Hand: 5 cards"` (`card_battle.tscn:47`).
- `GameFlowComponent` (`cardgame_main.tscn:47`) — score/flow, genre-neutral.
- `InventoryComponent` — **not in any cardgame scene**, and should not be. A collection
  (`collection.tscn:40`, "Collection (12 / 50 collected)") is an ownership set, not a bag with
  `MaxSlots` (`ecs/InventoryComponent.cs:52`).

**New ones the tree forces** (named, not designed — `CLAUDE.md` § *Scope*):

1. **A `CardComponent : UIComponent`** — binds one `GameCard` `.tres` to one slot `Control`:
   fill the `PanelContainer`, build the two faces `FlipCardComponent` needs (`:30-31`), and turn
   the empty `CardSlot1..6` into something. **This is the gap `GameCard` exists to close**;
   without it the `.tres` files are unreadable data.
2. **A `HandComponent` / `DeckComponent`** — the thing `hand_limit: 10` was always describing
   and nothing ever read (`BeepGenreGenerator.cs:232-234`). It would hook `CardBattle`'s
   `TurnChanged` (`:10`), which is already emitting into nothing.

**Cited, not re-derived:** the item edges do not exist (`README.md:88-90`); `DamageTypeComponent`
is dead (`README.md:86-87`) — a card's damage would be `Physical` like everything else, which
matters if `EffectId` ever resolves to `TakeDamage`.

---

## 5. Content vs framework

**We ship (framework):**
- **`GameCard : Resource`** — `[Tool][GlobalClass]`, filename = class name
  (`CLAUDE.md` § *[GlobalClass] Components*), under `ecs/items/` beside the spine or a sibling
  folder. *(UNCERTAIN: `ecs/items/` is named for items; a sibling root may not belong there.)*
- The **`CardComponent`** that reads it, and the two faces `FlipCardComponent` needs
  (`:30-31`) — otherwise we ship data nothing renders, which is the failure mode
  `MASTER_TODO.md:123-125` names.
- Either **wire `hand_limit`/`card_hover_scale`/`card_fan_angle`** to the new component, or
  **delete them from `genre.json:27-29`**. They currently emit generate-time warnings
  (`BeepGenreGenerator.cs:236-243`) and mislead.

**The developer authors (content):**
- Every `.tres`: `card_strike`, `card_defend`, `card_fireball`, `card_heal`, `card_rage`, their
  art, their `EffectId` resolutions, their deck lists.
- All rules and balance. `Cost = 1` on Strike is a placeholder; so are `"Enemy Health: 50 / 50"`
  (`card_battle.tscn:43`) and `"Collection (12 / 50 collected)"` (`collection.tscn:40`).
- The opponent AI. `CardBattle` says so: *"A game replaces this timer with real opponent AI,
  but the turn hand-off itself works today"* (`:45-46`).
