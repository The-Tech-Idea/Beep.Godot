## Dock Cleanup + Component Categories

### Part 1: Dock — 11 tabs → 2 tabs

**Keep:**
1. **"Game"** (renamed from Project, stripped to config only) — game name/version/developer, display settings, audio, language. Removes all folder/script/manager generator buttons.
2. **"Genre"** — file-driven skin catalog, unchanged.

**Remove (9 generator tabs):** Scenes, Characters, Shaders, Tweens, Particles, Projectiles, ECS Components, Validation, Export.

**Files:** Edit `BeepGameBuilderDock.cs` (remove 9 `Add*Tab` methods + `LoadPresets` infra), keep `BeepGameBuilderDock.Genres.cs`. Delete the 4 unused preset JSONs.

### Part 2: Component Categories in Add Node

Create 4 intermediate abstract base classes extending `EntityComponent`:

```
EntityComponent (existing abstract base)
├── UIComponent          ← new abstract [GlobalClass] base
├── GameplayComponent    ← new abstract [GlobalClass] base
├── ControllerComponent  ← new abstract [GlobalClass] base
└── WorldComponent       ← new abstract [GlobalClass] base
```

Then re-parent all 159 components to the appropriate category base instead of `EntityComponent` directly. In Godot's Add Node dialog, `EntityComponent` expands to show 4 category folders, each expanding to its components.

**Classification:**
- **UIComponent** (~80): everything in `ecs/ui/` — buttons, menus, dialogs, themes, effects, HUD, settings, tables, etc.
- **GameplayComponent** (~40): health, attack, movement, inventory, AI, status effects, projectiles, pickups, interaction, etc.
- **ControllerComponent**: PlatformerController, TopDownController, AIController, ShooterController
- **WorldComponent**: Weather, DayNight, Spawners, Checkpoints, Doors, MovingPlatforms, Parallax, WindField, Particles

**Implementation:** This is a bulk find-replace across ~159 files:
- `: EntityComponent` → `: UIComponent` (for ui/ files)
- `: EntityComponent` → `: GameplayComponent` (for gameplay files)
- `: EntityComponent` → `: ControllerComponent` (for controllers)
- `: EntityComponent` → `: WorldComponent` (for world/env files)

The `GetSiblingComponent<T>()` helper on `EntityComponent` still works — it scans all children, so re-parenting doesn't break discovery.

### Acceptance
- Dock: 2 tabs (Game + Genre), 0 errors
- Add Node: `EntityComponent` → 4 category folders → components
- Build clean, same warning baseline
- All components still discoverable via `GetSiblingComponent<T>()`