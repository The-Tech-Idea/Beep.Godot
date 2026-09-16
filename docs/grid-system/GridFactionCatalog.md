# GridFactionCatalog

Who the factions are, once, for the whole map (FEAT-10). Two `[Tool][GlobalClass]` `Resource`s, so a
map ships its players as a `.tres` beside its terrain: `GridFactionCatalog` is the ordered list, and
`GridFactionDefinition` is one entry — id, display name, colour, `Playable`, `Team`, `LockedStart`.
Neither is a node and neither holds runtime state; the assignment of factions to the map's starts
lives on [`GridStartAreaComponent`](GridStartAreaComponent.md), which is the only thing that writes.

**The index is the position plus one, and that is the whole point.** A faction's index is its place
in `Factions` + 1, which makes it a byte with 0 free to mean *nobody*. The class comment names the
three consumers that number is shared with: the per-cell owner a territory layer writes (FEAT-02's
`Owner` byte), the plane a fog layer keeps per faction (FEAT-03's plane index), and the colour the
minimap tint and the start overlay draw with. One number, so a faction cannot be player 2 on the map
and player 3 on the minimap. `ColourOf(0)` is transparent rather than the first faction's colour,
which is what lets a caller tint by owner and draw nothing where there is no owner.

Do not confuse that encoding with the one the cells carry. `GridCellDataComponent.GetStartArea`
also stores `k + 1` — but its `k` is a **start index**, not a faction index, and the two are only
equal by accident. The minimap converts explicitly (`reserved - 1` back to a start, then
`ColourOfStart`), and nothing reads a cell's area id as a catalog index.

**The actors layer is untouched.** `PlayerContextComponent.FactionId` stays a `string` (default
`"faction_1"`) and `ActorRegistryComponent` still owns hostility — `_hostilities` is keyed by the
two players' `FactionId` strings through `RelationKey`, and `AreHostile` still answers from it. The
catalog does not replace that model; it answers the three questions that model cannot: which index a
faction is, which faction an index is, and what colour to draw it. Resolution runs one way only,
id → index, through `GridIds.Normalize`, so the string stays the identity and the byte stays the
encoding. `Team` is declared for a game's own diplomacy and the engine reads it nowhere — not in the
catalog, not in the start component, and not in either view that draws a faction's colour.

**One tint rule.** `GridFactionCatalog.TintToward` is `static` and every view that shades something
by its owner calls it, so the amount and the blend are one decision rather than one per view. It is
a tint, not a fill: the terrain has to read through it or a minimap of owned land stops being a map.

## Public API — GridFactionCatalog

- `[Export] Godot.Collections.Array<GridFactionDefinition> Factions` — the factions in order.
  Position + 1 is the index every grid layer uses.
- `int Count` — `Factions.Count`.
- `GridFactionDefinition? At(int index)` — the faction at an index, or `null` when the index names
  nobody. Index 0 is "nobody" by construction, so it answers `null` rather than the first faction;
  so does any index past `Count`.
- `int IndexOf(string factionId)` — the index of a faction id, or **0** when the catalog does not
  hold it. Both sides are normalised through `GridIds.Normalize` (trim, lower-invariant, spaces and
  dashes to underscores), so an authored `"Faction 1"` and a saved `"faction_1"` are one id. An id
  that normalises to empty answers 0 without scanning.
- `string IdOf(int index)` — the faction id at an index, normalised, or `""` when the index names
  nobody.
- `Color ColourOf(int index)` — the colour of an index; **transparent** (`0,0,0,0`) for index 0 and
  for anything out of range, so a caller tinting by owner draws nothing where there is no owner
  instead of tinting everything with faction 1's colour.
- `bool PlayableAt(int index)` — whether a player may be assigned this index; `false` when the index
  names nobody.
- `int LockedStartOf(int index)` — the start this index must have, or `-1` when it may take any.
  Out-of-range indices answer `-1`: an index that names nobody constrains nothing.
- `static Color TintToward(Color source, Color faction, float strength)` — the one owner-tint rule.
  Returns `source` unchanged when `faction.A <= 0` (nobody owns it), otherwise
  `source.Lerp(new Color(faction, source.A), clamp(strength, 0, 1))`. The lerp target takes the
  *source's* alpha, so tinting changes hue and never opacity — an unreserved texel and a reserved one
  are equally opaque.

## Public API — GridFactionDefinition

- `[Export] string FactionId` (default `""`) — the faction's id, as `PlayerContextComponent.FactionId`
  spells it. Stored as authored; compared normalised by `GridFactionCatalog.IndexOf`.
- `[Export] string DisplayName` (default `""`) — what a player is shown. The id is for code.
- `[Export] Color Colour` (default `(0.25, 0.44, 0.88)`) — the one colour this faction is drawn in:
  minimap tint, start-area border, start ring. A second colour table is how a map comes to show a
  player as blue in one view and green in another.
- `[Export] bool Playable` (default `true`) — whether a player may be assigned this faction. False
  for scenery or neutral sides; `AutoAssign` skips them.
- `[Export(Range 0,16,1)] int Team` (default 0) — a grouping number for a game's own diplomacy. The
  engine reads it nowhere.
- `[Export(Range -1,23,1)] int LockedStart` (default `-1`) — the start this faction must have, as an
  index in the generator's start order, or `-1` to let the assignment choose. A scenario that means
  "the defenders begin in the valley" says it here rather than relying on the order the catalog
  happens to be in; `Assign` refuses any other start for a locked faction, and `AutoAssign` seats
  locked factions before anyone else.

## Dependencies

- `GridIds.Normalize` (`ecs/grid/GridIds.cs`) for id comparison — the addon's one id normaliser, so
  a faction id is canonicalised exactly like a resource id or a terrain kind.
- Godot's `Color.Lerp` for `TintToward`. Nothing else: no `NodePath`, no node lifecycle, no signals.
- Consumed by `GridStartAreaComponent` (`FactionCatalog` export; `IndexOf`, `IdOf`, `ColourOf`,
  `PlayableAt`, `LockedStartOf`, `Count`), by `GridMinimapComponent` (the static `TintToward` only),
  and — indirectly, through `GridStartAreaComponent.ColourOfStart` — by
  [`TerrainMapOverlayComponent`](../terrain-engine/TerrainMapOverlayComponent.md).
  `GridWorkerSpawnerComponent` reads only `_startArea.FactionCatalog is null` as the "no factions on
  this map" test; it never resolves a faction itself.
- Neither view resolves a catalog of its own. Both take a `GridStartAreaComponent` path and ask it
  for a colour, because the catalog maps a *faction* to a colour and only the assignment knows which
  faction holds start k — reading `ColourOf(k + 1)` would assume catalog order **is** start order,
  which is exactly what `LockedStart` breaks.

## Notes

- `Factions` is a **typed** `Array<GridFactionDefinition>`, not the untyped `Godot.Collections.Array`
  plus `TryRead`/`Enumerate` dual-key reader that `GridBuildDefinition`, `GridCropDefinition` and
  `GridObjectiveDefinition` use. There is therefore no dictionary-authored or duck-typed form of a
  faction: an entry is a real `GridFactionDefinition` or it is nothing. Null entries are tolerated
  rather than rejected — `IndexOf` skips them (`Factions[i] is { } faction`) and `At` can hand one
  back, which is why every accessor is written as `At(index)?.X ?? fallback`.
- The catalog answers by index, and every fallback it returns is a *refusal*, never a guess:
  `null`, `0`, `""`, transparent, `false`, `-1`. None of them names a real faction, so a caller that
  forgets to check cannot silently act as faction 1.
- `TintToward` clamps `strength` to 0–1 itself, so a misauthored tint export cannot invert or
  overshoot the blend.
- Nothing here is saved. The catalog is authored map/session data; the table that maps it onto the
  map's starts is what `GridStartAreaComponent` captures, keyed by faction id rather than by index
  precisely so that editing this list between a save and a load cannot hand a player someone else's
  start.
- `Team`, `DisplayName` and `Playable`-for-scenery are the fields OpenRA's `Players` block carries
  for the same reason: the map declares them and the rules decide what any of it means. AI factions,
  lobby UI and per-player starting stock are deliberately not here.
- **Guards.** `tests/addon_contract_scan.ps1`'s `# FEAT-10:` block requires this file to declare
  `public static Color TintToward(` — "the one owner-tint rule; every view that shades by owner uses
  it" — and, separately, refuses the bare call `ColourOf(` appearing in `GridMinimapComponent.cs` or
  `TerrainMapOverlayComponent.cs` at all. That second pin is written on the *bare* call rather than
  on `FactionCatalog.ColourOf(` because the qualified spelling was defeated by a single null-forgiving
  operator: `_startArea!.FactionCatalog!.ColourOf(` slid straight past it. `ColourOfStart(` does not
  contain `ColourOf(`, so the legitimate call through the start component is not caught. Nothing else
  in the scan reads this file: the index↔id↔colour mapping itself is covered by
  `tests/terrain_faction_assignment_probe.gd`, not by a pin.
- `tests/GridMinimapSmoke.cs` builds a two-faction catalog in C# and calls `TintToward` directly to
  compute what the minimap's texel must equal, so the guard and the code under test share the tint
  rule rather than each carrying a copy of the blend.
