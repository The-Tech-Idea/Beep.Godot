# Skin And Template Help

## Skin Catalog Shape

Skin catalog data lives under:

`addons/beep_game_builder_cs/catalogs/skins`

Each genre directory contains:

- `genre.json`: genre metadata and theme list.
- `geometry.json`: genre-level geometry defaults and optional background image.
- `themes/<theme>/theme.json`: color, texture, HUD, kit, and style data.
- `themes/<theme>/<palette>.json`: palette variants.

The scan verified all JSON parsed and all `texture_path` and `background_image` values pointed to existing files.

## Genres And Themes

| Genre | Themes |
| --- | --- |
| `cardgame` | `arcane`, `casino`, `paper`, `royal`, `velvet` |
| `citybuilder` | `blueprint`, `eco`, `future`, `industrial`, `urban` |
| `platformer` | `cartoon`, `modern`, `nature`, `pixel8bit`, `retro80s` |
| `puzzle` | `candy`, `cartoon`, `japan`, `modern`, `sea` |
| `racing` | `arcade`, `carbon`, `motorsport`, `neon`, `street` |
| `rpg` | `arcane`, `darkfantasy`, `fantasy`, `parchment`, `royal` |
| `shooter` | `cyberpunk`, `military`, `scifi`, `space`, `toxic` |
| `strategy` | `blueprint`, `command`, `military`, `royal`, `scifi` |
| `survival` | `apocalypse`, `desert`, `frozen`, `industrial`, `wilderness` |
| `topdown` | `classic`, `fantasy`, `japan`, `military`, `nature` |

## Runtime Consumers

- `SkinCatalog` loads and caches catalog data.
- `ThemePresetComponent` applies skin values to UI trees.
- `KitStyleJson` reads kit-specific style keys from theme JSON.
- `GameInfo` stores current genre, theme, palette, and geometry selections.
- MCP commands expose catalog discovery, skin application, and texture baking.

## Scene Templates

Template scenes live under:

- `addons/beep_game_builder_cs/templates/scenes`
- `addons/beep_game_builder_cs/templates/particles`

The scan verified template external resources exist.

### Scene Categories

| Category | Templates |
| --- | --- |
| Main genre scenes | `cardgame_main`, `citybuilder_main`, `platformer_main`, `puzzle_main`, `racing_main`, `rpg_main`, `shooter_main`, `strategy_main`, `survival_main`, `topdown_main` |
| Shared UI screens | `main_menu`, `settings_menu`, `game_over`, `save_game_menu`, `load_game_menu`, `hud`, `level_summary`, `theme_gallery`, `kit_browser`, `kit_gallery` |
| Entity templates | `player_template`, `enemy_template`, `pickup_template`, `projectile_template`, `dialog_template`, `robot_npc_template` |
| Genre UI screens | RPG, racing, shooter, puzzle, strategy, survival, cardgame, and citybuilder sub-screens |
| Starter levels | Two level templates for platformer, racing, RPG, shooter, survival, and topdown |
| Particles | blood, coin, dust, explosion, fire, heal, sparks, magic, muzzle, rain, smoke, sparkle |

## Adding A New Skin Theme

1. Add a new theme directory under `catalogs/skins/<genre>/themes/<theme>`.
2. Add `theme.json`.
3. Add palette files if the theme supports palette variants.
4. Ensure all `texture_path` references exist.
5. Add or update texture assets under `textures/<genre>/<theme>` or shared HUD texture folders.
6. Run the reference validation used in the review.
7. Test through `beep.list_themes`, `beep.list_palettes`, `beep.apply_skin`, and a visual scene render.

## Adding A New Scene Template

1. Put the `.tscn` under the correct `templates/scenes` subfolder.
2. Use `res://addons/beep_game_builder_cs/...` paths for addon-owned scripts/assets.
3. Use PascalCase property names for C# `[Export]` assignments in scenes.
4. Verify external resources resolve.
5. Open the template in Godot after a successful C# build.
6. Add it to generator or MCP discovery if it should be selectable.
