# Master TODO Tracker — Kenney UI Pack Integration (per-genre textured skins)

> Wire the **Kenney "Game Assets All-in-1 3.6.0 → UI assets"** packs into the skin catalog so
> every genre ships a **textured** look (nine-patch buttons/panels/inputs), instead of the
> procedural `StyleBoxFlat` fallback it renders today. Per-phase detail lives beside this file.
>
> **Goal:** open the dock, pick a genre + theme, click *Apply skin* → the shipped UI shows real
> Kenney nine-patch art, per genre, with the procedural look remaining as the graceful fallback.
>
> **Source pack:** `H:\GameDev\GFX\GameAssets\Kenney Game Assets All-in-1 3.6.0\UI assets`
> (all sub-packs are **CC0** — free for commercial use, attribution appreciated, not required).

---

## The key finding — the system is already built, the art is missing

This is **not** a new theming system; it's filling slots that already exist:

- The skin engine already builds `StyleBoxTexture` (9-patch) per theme from a `theme.json`
  `textures{}` block — schema in `docs/FILE_FORMATS.md:230-279`, builder at
  `ecs/ui/SkinCatalog.cs:484-560` (`TextureSlotDef.BuildStyleBox()`), consumed via
  `ecs/ui/FileThemePreset.cs:34-47`.
- **34 of the 50 themes already declare texture slots** pointing at
  `res://addons/beep_game_builder_cs/textures/<genre>/<theme>/{button_normal,button_hover,button_pressed,panel}.png`
  — but **those PNGs are not shipped** (the addon ships no art, by the CLAUDE.md scope rule), so
  every genre currently renders the procedural `StyleBoxFlat`.
- Missing textures fall back **silently and correctly**: `SkinCatalog.cs:513` guards with
  `!ResourceLoader.Exists(Path) → return null`, and the null routes to the procedural box.

So the work is: **import curated Kenney textures into those `textures/<genre>/<theme>/` folders,
name them to the slots the theme.json already expects, and calibrate the nine-patch margins.**
No C# changes are required for the core outcome.

## Scope — REVISED (user, this session)

**Ship the feature, not the art.** The addon makes every genre theme *texture-ready* — the
`theme.json` slots, sensible nine-patch margins, the graceful procedural fallback, the
`CursorComponent`, and an opt-in import script — but ships **no** UI art. The developer supplies
their own textures (Kenney, itch.io, hand-drawn) by dropping files into the folders the themes
already point at. Until they do, every theme renders the procedural `StyleBoxFlat` (no error).

---

## The packs (all CC0, `H:\...\UI assets\`)

| Pack | Files | Aesthetic | Feeds genres |
|---|---|---|---|
| **UI Pack** | 875 | clean modern flat; per-color (Blue/Green/Grey/Red/Yellow), rectangle/round/square in flat·gloss·gradient·border·line + depth variants | puzzle, platformer(modern/cartoon), citybuilder, defaults |
| **UI Pack - Sci-fi** | 822 | sci-fi panels, colored bars, glass/metal | shooter, strategy(command/scifi), racing(neon/carbon/arcade) |
| **UI Pack - Adventure** | 260 | wood/parchment (button_brown/grey/red, banners, panels) | rpg, topdown, survival(wilderness/desert), citybuilder(industrial) |
| **Fantasy UI Borders** | 282 | ornate 9-patch panel borders | rpg(royal/darkfantasy), cardgame(royal/velvet/arcane), strategy(royal) |
| **UI Pack - Pixel Adventure** | 514 | pixel-art tiles/tilesheets | platformer(pixel8bit), retro pixel themes |
| **UI Pixel Pack** | sheet | pixel spritesheet | platformer(retro80s) |
| **UI Adventure Pack** | 90 | arrows + progress bars | supplements progress_bg/fill everywhere |
| **Cursor Pack / Cursor Pixel Pack** | — | mouse cursors | optional per-genre cursor (Phase 5, opt-in) |
| **Mobile Controls** | — | touch dpad/buttons | optional mobile export (out of scope, noted) |

---

## Progress

- [x] **Phase 0 — Audit** — scanned the pack, confirmed the theme system already declares textured
      themes with a graceful fallback (this doc).
- [x] **Phase 1 — (superseded)** — no art shipped. The import script `import_kenney.py` remains as an
      **opt-in helper** the developer runs locally against their own pack; it copies into *their*
      project, nothing ships with the addon.
- [x] **[Phase 2 — Margins](phase-2-ninepatch-calibration.md)** — sensible **default** nine-patch
      margins are baked into every theme.json (uipack 20/28, adventure 18, sci-fi 14, fantasy 28 tiled,
      pixel 6 tiled) so a dev's art slots in with a good starting look; they tune from there.
- [x] **[Phase 3 — Mapping](phase-3-genre-theme-mapping.md)** — the 50-theme style map is documented
      (and encoded in the helper script) as guidance for what art suits each theme.
- [x] **[Phase 4 — Wire theme.json](phase-4-wire-and-states.md)** — all **50/50 themes** carry a ready
      `textures{}` block (button normal/hover/pressed/disabled + panel). Build 0 errors; all 50 parse;
      every slot falls back to procedural until the dev supplies a file.
- [x] **[Phase 5 — CursorComponent](phase-5-verify-credits.md)** — `CursorComponent` shipped (opt-in,
      resets cursors on exit; dev assigns their own cursor texture). README documents the BYO-art flow.

**Feature complete — the addon is texture-ready; art is the developer's.** No editor pass needed
from us (nothing to import); the dev's own art triggers the import/`.uid` on their side.

## Verification gates (every phase)

1. `dotnet build` → 0 errors (this initiative is mostly JSON + assets; C# changes only if Phase 4
   extends the slot set).
2. `cd addons/beep_game_builder_cs/templates/scenes && ./validate_scenes.sh` → PASS.
3. **Editor pass (the real one):** open a scene → dock → pick genre+theme → *Apply to all
   ThemePresetComponents* → buttons/panels show Kenney art; corners are crisp (margins right);
   pixel themes are sharp (nearest filter), not blurred.
4. **Fallback check:** temporarily rename one theme's `textures/` folder → that theme renders the
   procedural `StyleBoxFlat` with no error in Output → C#. (The whole safety net is `SkinCatalog.cs:513`.)

## Decisions — all resolved (user, this session)

| # | Decision | Resolution |
|---|---|---|
| A | Ship UI textures in the addon | **NO (changed mind)** — ship the *feature*, not the art. Developer brings their own. |
| B | Cover all 50 themes or only the 34 already-slotted | **ALL 50** — every theme carries a ready `textures{}` block. |
| C | Individual PNGs vs atlas | **Individual PNGs** (the slot layout the dev fills). |
| D | Pixel themes textured or procedural | Slots wired; pixel art is BYO. Procedural fallback stands. |
| E | Per-genre mouse cursor | **YES** — `CursorComponent` ships (generic; dev assigns their own cursor texture). |

## Cross-references

- **enhancement-review** (`plans/enhancement-review/`) — the theming code touched here
  (`ThemePresetComponent`, `FileThemePreset`) was reviewed there; its per-node-override rule and
  idempotency guarantees still hold — textures slot in through the same `SkinOr()` path.
- `docs/FILE_FORMATS.md:230-279` — the `textures{}` / `TextureSlotDef` schema (the contract this
  initiative writes against).
- `docs/SKIN_SYSTEM.md` — the add-a-theme cookbook; this initiative is "add textures to a theme."
