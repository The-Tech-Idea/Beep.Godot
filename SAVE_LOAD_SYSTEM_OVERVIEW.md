# Universal Save/Load System Overview

## Complete Implementation Status ✅

### Core System Components

#### 1. **Game State Container** (GameStateData.cs)
- ✅ Feature-based architecture with state composition
- ✅ Predefined containers: Movement, Combat, Inventory, Progression, World
- ✅ Custom data dictionary for game-specific state
- ✅ JSON serialization/deserialization

#### 2. **Save/Load Manager** (GameStateManagerComponent.cs)
- ✅ Auto-discovers ISaveable components via tree scan
- ✅ Slot-based saves (0-4) + autosave slot (-1)
- ✅ Auto-save with configurable interval
- ✅ Signals: GameStateSaved, GameStateLoaded, GameStateDeleted, AutosaveTriggered

#### 3. **ISaveable Interface** (IGameStateable.cs)
- ✅ Simple contract: `Save(state)` and `Load(state)` methods
- ✅ Components opt-in by implementing interface
- ✅ SaveableHelper utility for manual discovery

#### 4. **UI System** (Save/Load Menus)
- ✅ SaveGameMenuComponent — Slot selection + save name input
- ✅ LoadGameMenuComponent — Slot browser with metadata display
- ✅ SaveLoadManagerComponent — Integration layer
- ✅ Wired to main menu & pause menu
- ✅ Auto-disables Save button when no game running

#### 5. **Game State Manager** (GameApp.cs)
- ✅ Session management (IsGameRunning, IsPaused)
- ✅ Difficulty levels with score multipliers (0.5x - 2.0x)
- ✅ Statistics tracking (games played, win/loss, best score)
- ✅ Achievement system
- ✅ Checkpoint/quicksave management
- ✅ FPS monitoring
- ✅ Dev mode with cheats
- ✅ Comprehensive signals for all state changes

---

## Example Components with ISaveable

### 1. HealthComponent (Combat State)
**File:** `ecs/HealthComponent.cs`
- Saves: Current health, max health
- Container: `state.Combat`
- Signals: HealthChanged (emitted on load)

### 2. InventoryComponent (Collection State)
**File:** `ecs/InventoryComponent.cs`
- Saves: All items and quantities
- Container: `state.Inventory`
- Signals: InventoryChanged (emitted on load)

### 3. PlayerMovementComponent (Transform State)
**File:** `ecs/PlayerMovementComponent.cs`
- Saves: Position, velocity, rotation, facing direction
- Container: `state.Movement`
- Signals: Moved, FacingChanged (emitted on load)

### 4. ScoreComponent (Custom Data)
**File:** `ecs/ScoreComponent.cs`
- Saves: Player score
- Container: `state.GameData["player_score"]`
- Integrates with GameApp difficulty multipliers

---

## UI Integration

### Main Menu (main_menu.tscn)
- ✅ "New Game" button → Sets IsGameRunning = true
- ✅ "Save Game" button → Shows SaveGameMenuComponent (disabled if no game)
- ✅ "Load Game" button → Shows LoadGameMenuComponent
- ✅ SaveLoadManagerComponent auto-attached

### Pause Menu (pause_menu.tscn)
- ✅ "Save Game" button → Shows SaveGameMenuComponent (only when game running)
- ✅ "Load Game" button → Shows LoadGameMenuComponent
- ✅ SaveLoadManagerComponent auto-attached

### Game Flow (GameFlowComponent.cs)
- ✅ Calls `SetGameRunning(true)` when game starts
- ✅ Calls `SetGameRunning(false)` when game ends
- ✅ Auto-tracks session playtime
- ✅ Records game end outcome (won/lost)

---

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                      GAME COMPONENTS                        │
│   (HealthComponent, InventoryComponent, etc.)              │
│   ├─ Implement ISaveable                                    │
│   └─ Methods: Save(state), Load(state)                     │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓ (Auto-discovery)
┌─────────────────────────────────────────────────────────────┐
│            GameStateManagerComponent                        │
│   ├─ Finds all ISaveable components                        │
│   ├─ Calls Save() on each → populates GameStateData       │
│   ├─ Calls Load() on each ← restores from GameStateData   │
│   └─ Handles file I/O (JSON serialization)                │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────────────┐
│              GameStateData Container                        │
│   ├─ state.Movement (position, velocity, rotation)        │
│   ├─ state.Combat (health, stamina, lives)                │
│   ├─ state.Inventory (items, quantities)                  │
│   ├─ state.Progression (quests, achievements, level)      │
│   ├─ state.World (entities, switches)                     │
│   └─ state.GameData (custom game-specific data)           │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓ (JSON serialization)
┌─────────────────────────────────────────────────────────────┐
│              Disk Storage (JSON Files)                      │
│   user://saves/save_0.json  (slot 0)                       │
│   user://saves/save_1.json  (slot 1)                       │
│   user://saves/autosave.json (autosave)                    │
└─────────────────────────────────────────────────────────────┘
```

---

## Usage Example: Complete Game Flow

```csharp
// 1. NEW GAME
GameApp.Instance.ResetSession();
GameApp.Instance.SetDifficulty(GameApp.Difficulty.Normal);
GameApp.Instance.SetGameRunning(true);
// → Signals: SessionStarted, GameRunningChanged
// → Save button becomes enabled

// 2. GAMEPLAY
GameApp.Instance.AddSessionScore(100);              // Score += 100
PlayerInventory.AddItem("sword", 1);                // Inventory += sword
PlayerHealth.TakeDamage(10);                        // Health -= 10
// → Each component tracks changes locally

// 3. SAVE GAME
_saveLoadManager.ShowSaveMenu();
// User enters name "My Playthrough" and clicks Save slot 0
// → GameStateManagerComponent.SyncAllSaveables()
//   → HealthComponent.Save(state)     ✓ state.Combat.Health = 90
//   → InventoryComponent.Save(state)  ✓ state.Inventory.Items["sword"] = 1
//   → ScoreComponent.Save(state)      ✓ state.GameData["player_score"] = 100
// → JSON written to user://saves/save_0.json

// 4. CLOSE GAME
// (Game exits, state lost locally)

// 5. REOPEN GAME & LOAD
_saveLoadManager.ShowLoadMenu();
// User clicks "Load" on slot 0
// → GameStateManagerComponent.Load(0)
//   → JSON read from user://saves/save_0.json
// → GameStateManagerComponent.RestoreAllSaveables()
//   → HealthComponent.Load(state)     ✓ Health restored to 90
//   → InventoryComponent.Load(state)  ✓ Inventory restored with sword
//   → ScoreComponent.Load(state)      ✓ Score restored to 100
//   → Signals emitted for UI update
// → Game state fully restored!

// 6. PAUSE & SAVE
GetTree().Paused = true;
GameApp.Instance.SetPaused(true);
// → GamePaused signal emitted
// → Pause menu visible with Save/Load buttons available

// 7. GAME ENDS
PlayerHealth.Dies();  // Health reaches 0
// → GameFlowComponent detects game over
// → GameApp.Instance.SetGameRunning(false)
// → GameStateManagerComponent.RecordGameEnd(false)
// → GamesLostTotal++
// → Save button disabled (no game running)
```

---

## Signal/Event System

### GameApp Signals
```csharp
// Session management
GameRunningChanged(bool running)       // Game started/ended
GamePaused()                           // Game paused
GameResumed()                          // Game resumed

// Score & progression
SessionScoreChanged(int score)         // Score updated
LevelChanged(int level)                // Level changed
LevelCompleted(int level)              // Level finished

// Achievements & checkpoints
AchievementUnlocked(string id)         // Achievement earned
CheckpointReached(int level)           // Checkpoint saved

// Session lifecycle
SessionStarted()                       // Game session started
SessionEnded(bool won)                 // Game session ended

// State changes
DifficultyChanged(int difficulty)      // Difficulty set
DevModeToggled(bool enabled)           // Dev mode toggled
```

### Save/Load Signals
```csharp
// GameStateManagerComponent
GameStateSaved(int slot, string filename)
GameStateLoaded(int slot, string filename)
GameStateDeleted(int slot)
AutosaveTriggered()

// Component signals on Load (examples)
HealthComponent.HealthChanged(float current, float max)
InventoryComponent.InventoryChanged()
ScoreComponent.ScoreChanged(int newScore, int change)
```

---

## Supported Game Types

✅ **All 10 Genres** (built-in templates):
- Platformer
- Top-Down
- Shooter
- RPG
- Survival
- Racing
- City Builder
- Strategy
- Puzzle
- Card Game

Each genre's main game scene automatically includes:
- GameStateManagerComponent (auto-wired)
- GameFlowComponent (signals session start/end)
- Save/Load UI (wired to main menu & pause)

---

## File Reference

### Core System
- `core/GameStateData.cs` — State container
- `ecs/GameStateManagerComponent.cs` — Save/load manager
- `ecs/IGameStateable.cs` — ISaveable interface
- `ecs/GameApp.cs` — Global game state

### UI/Integration
- `ecs/ui/SaveGameMenuComponent.cs` — Save dialog
- `ecs/ui/LoadGameMenuComponent.cs` — Load dialog
- `ecs/ui/SaveLoadManagerComponent.cs` — Wiring layer
- `templates/scenes/save_game_menu.tscn` — Save UI scene
- `templates/scenes/load_game_menu.tscn` — Load UI scene

### Example Components
- `ecs/HealthComponent.cs` — Combat state (ISaveable)
- `ecs/InventoryComponent.cs` — Inventory state (ISaveable)
- `ecs/PlayerMovementComponent.cs` — Movement state (ISaveable)
- `ecs/ScoreComponent.cs` — Score tracking (ISaveable)

### Documentation
- `core/GameAppGuide.cs` — GameApp API reference
- `core/SaveLoadImplementationGuide.md` — Developer guide
- `SAVE_LOAD_SYSTEM_OVERVIEW.md` — This file

---

## Next Steps for Developers

1. **Review Example Components** — See how HealthComponent, InventoryComponent implement ISaveable
2. **Convert Your Components** — Add ISaveable to any component that needs state persistence
3. **Test Save/Load** — Start game → change state → save → close → load → verify
4. **Custom Data** — Use `state.GameData["key"]` for game-specific state
5. **Wire UI Events** — Connect your menus to ShowSaveMenu() / ShowLoadMenu()

---

## Production Checklist ✅

- [x] Save/load backend complete
- [x] UI scenes created
- [x] Main menu wired
- [x] Pause menu wired
- [x] GameApp comprehensive
- [x] Example components provided
- [x] Documentation complete
- [x] Auto-discovery working
- [x] Auto-save support
- [x] Slot management (0-4 + autosave)
- [x] Save metadata tracking
- [x] Game running flag
- [x] All 10 genre templates updated

**Status: PRODUCTION READY** 🎮

---

## Performance Notes

- Auto-discovery via tree scan: **O(n)** where n = scene nodes
- JSON serialization: **Negligible** for typical game state (~1-50KB)
- Save I/O: **Async-friendly** (can wrap in tasks if needed)
- Memory: **Minimal** (state is transient, only active during save/load)

---

Generated: 2026-07-15
System Version: 1.0.0 (Complete)
