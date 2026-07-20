# Beep Game Builder — templates folder

This folder holds the scene templates that the addon ships out-of-the-box.
They are loaded at runtime by `BeepGenreScene` (and a few `GameFlowComponent`
navigation rules read from `genre.json#nav_wiring`).

## How it works

1. User opens a Godot scene.
2. User adds a `BeepGenreScene` component (Add Node → Beep → GenreScene).
3. User sets `GenreId = "platformer"` (or topdown / shooter / puzzle).
4. User runs the scene. `BeepGenreScene._Ready` then:
   - resolves `addons/beep_game_builder_cs/catalogs/skins/<genreId>/genre.json`,
   - copies the genre's default_theme + tuning into `GameApp.Info`,
   - loads `templates/scenes/<genreId>/<genreId>_main.tscn` and adds it as a child,
   - drives a sibling `ThemePresetComponent` from the resolved theme/palette/geometry.

No buttons to click, no generators to run. The whole flow is data-driven.

## Layout

```
templates/scenes/
├── main_menu.tscn              ← Shared UI scenes (already wired). main_menu also
│                                 doubles as the pause overlay (no separate pause menu).
├── settings_menu.tscn          Each is a Control tree with [GlobalClass]
│                                 C# components only — no inline scripts.
├── hud.tscn
├── game_over.tscn
├── player_template.tscn        ← Generic player/NPC/enemy shells
├── robot_npc_template.tscn        that the genre templates instance
├── enemy_template.tscn            underneath.
├── pickup_template.tscn
├── dialog_template.tscn
├── projectile_template.tscn
├── platformer/                  ← Genre-specific scenes
│   ├── platformer_main.tscn       Loaded by BeepGenreScene when GenreId="platformer"
│   ├── level_select.tscn
│   └── level_results.tscn
├── topdown/
│   └── topdown_main.tscn
├── shooter/
│   └── shooter_main.tscn
└── puzzle/
    ├── puzzle_main.tscn
    ├── level_map.tscn
    ├── pre_level.tscn
    ├── level_complete.tscn
    └── level_failed.tscn
```

## To add a new genre

1. Add `catalogs/skins/<your_genre>/{genre.json, geometry.json, themes/<theme>/theme.json}`.
2. Add `<your_genre>_main.tscn` here under `templates/scenes/<your_genre>/`.
3. Done. `BeepGenreScene` with `GenreId = "<your_genre>"` will pick it up at runtime.

No C# changes required.
