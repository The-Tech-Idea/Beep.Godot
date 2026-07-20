# Phase 5 — Polish & docs

The skin JSON validated **100% clean** (50 themes / 350 palettes / 10 genre.json / 10 geometry.json — 0
problems), so nothing to fix there. What's left is three stale doc lines the code has drifted past, plus
the short remaining polish tail from the core-utilities pass.

## Doc fixes

### 1. `docs/FILE_FORMATS.md` — texture slot count & geometry count
- **`:233-247`** — the texture schema lists **13** slots, but `SkinCatalog.cs` parses **14**: the
  `dialog` slot (`SkinCatalog.cs:408` `ParseTextureSlot("dialog", …)`, `ThemeTextureSlots.Dialog:551`) is
  **undocumented.** Add it.
- **`:176,199`** — "geometry … 12 numbers" is wrong; the field table (`:204-211`) and
  `SkinCatalog.cs:246-258` both = **13**. Fix to 13.

### 2. `CLAUDE.md:109` — component counts drifted
- "199 files [GlobalClass]" → actual **203**. Category counts drifted: `UIComponent` ~54→53,
  `GameplayComponent` ~44→41, **`WorldComponent` ~21→18** (the biggest drift), `ControllerComponent` 18
  (exact), `EffectComponent` 4 (exact). Nudge the numbers (they're `~`-prefixed, but WorldComponent is far
  enough off to correct).

## Polish tail (from the core-utilities pass, not yet placed)

- **`WeatherForecast.cs:29-35`** — `WeatherType {Clear, Cloudy, Rainy, Stormy}` diverges from
  `WeatherSystemComponent {Clear, Cloudy, Rain, Snow, Storm}`. `GenerateForecast` stamps
  `"Rainy"`/`"Stormy"`, which won't `Enum.TryParse` against the real enum (`ApplyTuning:194` already
  rejects "Rainy"). Display-only today, but align the names to `Rain`/`Storm` so a future consumer that
  parses the forecast doesn't silently get `Clear`.

## Verify
- `dotnet build` → 0 errors; `validate_scenes.sh` → PASS.
- Docs: re-read the two `FILE_FORMATS.md` sections against `SkinCatalog.cs` — 14 texture slots, 13 geometry
  numbers.

## Round-4 close-out
- Update `plans/README.md` with the round-4 row.
- Update this folder's `MASTER_TODO.md` progress checkboxes as phases land.
