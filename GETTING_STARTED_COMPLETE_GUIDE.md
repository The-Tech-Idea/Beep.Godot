# Beep.Godot Complete Getting Started Guide

**Version:** 1.0  
**Date:** 2026-07-15  
**Status:** Production-Ready

---

## Overview

This guide integrates all Beep.Godot systems (Save/Load, Weather, GameApp, and 4 converted components) into a **complete, step-by-step workflow** for building a game across all 10 genres.

---

## Part 1: Project Setup

### Step 1: Generate a Game

```bash
# In Godot editor, open the Beep Game Builder dock
Select Genre: "Platformer" (or any of 10 genres)
Game Name: "My Awesome Game"
Click: Generate Game
```

**What You Get Automatically:**
- ✅ Folder structure (scenes/, scripts/, assets/)
- ✅ Game configuration (game_info.tres)
- ✅ Main menu + pause menu (wired to save/load)
- ✅ GameStateManagerComponent (auto-discovery enabled)
- ✅ WeatherSystemComponent (in main game scene)
- ✅ GameApp autoload (session management)
- ✅ Save/Load UI (disabled by default for main menu)

### Step 2: Verify Configuration

Open `game_info.tres` and check:

```
✅ GameName: "My Awesome Game"
✅ Genre: Platformer
✅ EnableWeather: true (adjustable)
✅ DefaultWeather: Clear
✅ EnableGameStateManager: true
✅ AutosaveEnabled: true
✅ MaxSaveSlots: 5
```

**All defaults are optimal.** Customize only if needed.

---

## Part 2: Create Your Player Character

### Step 1: Scene Structure

Create `scenes/main/player.tscn`:

```gdscript
[scene]
root = Node2D "Player"
├─ Sprite2D (your character visual)
├─ CollisionShape2D (capsule for platformer)
├─ Camera2D (follows player)
├─ Health (HealthComponent) ← Saves/loads health
├─ Movement (PlayerMovementComponent) ← Saves/loads position
├─ Inventory (InventoryComponent) ← Saves/loads items
└─ Score (ScoreComponent) ← Saves/loads score
```

### Step 2: Create PlayerController Script

```csharp
public partial class PlayerController : CharacterBody2D
{
    private HealthComponent _health;
    private PlayerMovementComponent _movement;
    private InventoryComponent _inventory;
    private ScoreComponent _score;

    public override void _Ready()
    {
        _health = GetNode<HealthComponent>("Health");
        _movement = GetNode<PlayerMovementComponent>("Movement");
        _inventory = GetNode<InventoryComponent>("Inventory");
        _score = GetNode<ScoreComponent>("Score");

        // Connect to game state changes
        GameApp.Instance.GamePaused += OnGamePaused;
        GameApp.Instance.GameResumed += OnGameResumed;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GameApp.Instance.IsPaused) return;

        // Get input
        var input = Vector2.Zero;
        if (Input.IsActionPressed("move_left")) input.X = -1;
        if (Input.IsActionPressed("move_right")) input.X = 1;
        if (Input.IsActionPressed("jump")) Jump();

        // Move player (uses PlayerMovementComponent)
        _movement.Move(input);

        // Health regeneration
        if (_health.CurrentHealth < _health.MaxHealth)
            _health.Heal(10f * (float)delta);
    }

    private void Jump() { /* ... */ }
    private void OnGamePaused() { /* ... */ }
    private void OnGameResumed() { /* ... */ }
}
```

### Step 3: Wire to Main Scene

Edit `scenes/main/{genre}_main.tscn`:

```gdscript
[node name="Player" type="CharacterBody2D" parent="."]
script = ExtResource("path/to/PlayerController.cs")
# Add child nodes (Sprite, Camera, Health, Movement, Inventory, Score)
```

---

## Part 3: Implement Game Mechanics

### Example 1: Enemy Spawning with Coroutines

```csharp
public partial class EnemySpawner : Node2D
{
    private CoroutineHostComponent _coroutines;
    private PackedScene _enemyScene = GD.Load<PackedScene>("res://scenes/enemy.tscn");

    public override void _Ready()
    {
        _coroutines = GetNode<CoroutineHostComponent>("Coroutines");

        // Spawn waves on a timer
        _coroutines.Delay(2f, () => SpawnWave(1, 3));
        _coroutines.Delay(10f, () => SpawnWave(2, 5));
        _coroutines.Delay(20f, () => SpawnWave(3, 8));
    }

    private void SpawnWave(int waveNum, int count)
    {
        GD.Print($"Wave {waveNum}!");
        for (int i = 0; i < count; i++)
        {
            var enemy = (Node2D)_enemyScene.Instantiate();
            enemy.Position = new Vector2(GD.Randf() * 1000, -100);
            AddChild(enemy);
        }

        GameApp.Instance.AddSessionScore(100 * waveNum);  // Bonus for surviving
    }
}

// Add to scene:
[node name="EnemySpawner" type="Node2D" parent="."]
script = ExtResource("path/to/EnemySpawner.cs")
├─ Coroutines (CoroutineHostComponent)
```

### Example 2: UI with Data Binding

```csharp
public partial class HUD : Control
{
    private DataBinderHostComponent _binder;
    private Player _player;

    public override void _Ready()
    {
        _binder = GetNode<DataBinderHostComponent>("DataBinder");
        _player = GetTree().Root.GetNode<Player>("Player");

        if (_binder != null && _player != null)
        {
            // Bind player stats to UI
            _binder.BindProgress(_player, nameof(_player.Health),
                GetNode<ProgressBar>("HealthBar"));
            
            _binder.BindLabel(_player, nameof(_player.Level),
                GetNode<Label>("LevelLabel"), "Level: {0}");
            
            _binder.BindLabel(_player, nameof(_player.Score),
                GetNode<Label>("ScoreLabel"), "Score: {0}");

            // Color health bar based on health percentage
            _binder.Bind(_player, nameof(_player.HealthPercentage),
                GetNode<ProgressBar>("HealthBar"), "Color",
                BindingMode.OneWay, percent =>
                {
                    float pct = (float)percent / 100f;
                    if (pct > 0.5f) return Colors.Green;
                    if (pct > 0.2f) return Colors.Orange;
                    return Colors.Red;
                });
        }
    }
}
```

### Example 3: Enemy AI with State Machine

```csharp
public partial class EnemyAI : Node2D
{
    private StateMachineComponent _fsm;
    private Player _player;
    private AnimatedSprite2D _sprite;
    private const float DetectRange = 300f;
    private const float AttackRange = 50f;

    public override void _Ready()
    {
        _fsm = GetNode<StateMachineComponent>("StateMachine");
        _player = GetTree().Root.GetNode<Player>("Player");
        _sprite = GetNode<AnimatedSprite2D>("Sprite");

        // Define states
        _fsm.AddState("idle", onEnter: () =>
        {
            _sprite.Play("idle");
        });

        _fsm.AddState("patrol", onUpdate: () =>
        {
            _sprite.Play("walk");
            Position = Position.MoveToward(new Vector2(500, GlobalPosition.Y), 100 * (float)GetPhysicsProcess());
        });

        _fsm.AddState("chase", onUpdate: () =>
        {
            _sprite.Play("run");
            Position = Position.MoveToward(_player.GlobalPosition, 200 * (float)GetPhysicsProcess());
        });

        _fsm.AddState("attack", onEnter: () =>
        {
            _sprite.Play("attack");
        }, onUpdate: () =>
        {
            if (_fsm.CurrentStateTime > 1f)
                _fsm.ChangeState("chase");
        });

        // Define transitions
        _fsm.AddTransition("idle", "patrol", trigger: "start");
        _fsm.AddTransition("patrol", "chase", trigger: "see_player");
        _fsm.AddTransition("chase", "attack", trigger: "in_range");
        _fsm.AddTransition("attack", "chase", trigger: "missed");

        _fsm.Start("idle");
        _fsm.Trigger("start");
    }

    public override void _Process(double delta)
    {
        if (_player == null) return;

        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

        if (dist < AttackRange && _fsm.CurrentState != "attack")
            _fsm.Trigger("in_range");
        else if (dist > AttackRange && _fsm.CurrentState == "attack")
            _fsm.Trigger("missed");
        else if (dist < DetectRange && _fsm.CurrentState == "patrol")
            _fsm.Trigger("see_player");
    }
}
```

---

## Part 4: Save/Load Integration

### Step 1: Implement ISaveable on Your Components

```csharp
public partial class CustomComponent : GameplayComponent, ISaveable
{
    private int _customValue;

    public void Save(GameBuilder.GameStateData state)
    {
        // Save to custom data dictionary
        state.GameData["my_custom_value"] = _customValue;
    }

    public void Load(GameBuilder.GameStateData state)
    {
        // Restore from saved state
        if (state.GameData.TryGetValue("my_custom_value", out var val))
            _customValue = (int)val;
    }
}
```

### Step 2: Auto-Discovery Works Automatically

When player clicks "Save Game":
1. GameStateManagerComponent scans the tree
2. Finds all ISaveable components automatically
3. Calls `Save()` on each component
4. Writes JSON to `user://saves/save_0.json`

When player clicks "Load Game":
1. Reads JSON from disk
2. Calls `Load()` on each ISaveable component
3. Game state fully restored

**Zero manual wiring needed!**

### Step 3: Test Save/Load Flow

```
1. Start game
2. Move player to position (100, 200)
3. Collect items
4. Take damage
5. Click "Save Game" → Enter name "First Try"
6. Close game
7. Reopen game
8. Click "Load Game" → Select "First Try"
9. ✅ Player at (100, 200), items in inventory, health reduced
```

---

## Part 5: Weather System Usage

### Step 1: Enable Weather (Automatic)

Your game has WeatherSystemComponent in the main scene automatically. It's **already wired** with:
- ✅ 10 weather types (Clear, Rain, Storm, Snow, Fog, etc.)
- ✅ Particle effects per weather
- ✅ Ambient tinting
- ✅ Wind system
- ✅ Cloud rendering
- ✅ Lightning strikes

### Step 2: Trigger Weather Changes

```csharp
// In a gameplay controller
public partial class WeatherController : Node
{
    private WeatherSystemComponent _weather;

    public override void _Ready()
    {
        _weather = GetTree().Root.FindChild("Weather", true, false) as WeatherSystemComponent;
    }

    public void ChangeWeather()
    {
        _weather?.SetWeather(WeatherSystemComponent.WeatherType.Rain);
        // Particles start, ambient darkens, wind activates
    }

    public void StartAutoCycle()
    {
        _weather.AutoCycle = true;
        _weather.CycleInterval = 30f;  // Change weather every 30 seconds
    }
}
```

### Step 3: Wind Affects Physics

Add `WindFieldComponent` to your world:

```gdscript
[node name="WindField" type="Area2D" parent="."]
script = ExtResource("path/to/WindFieldComponent.cs")
├─ CollisionShape2D (rectangle covering level)
```

Now:
- ✅ RigidBody2D objects (debris, boxes) blow in the wind
- ✅ CharacterBody2D (player) gets pushed by wind
- ✅ Wind strength tied to weather system

---

## Part 6: Advanced Features

### Difficulty Scaling

```csharp
// Set difficulty before game starts
GameApp.Instance.SetDifficulty(GameApp.Difficulty.Hard);

// Automatically scales:
// - Enemy damage: 1.5x
// - Loot drops: 0.8x
// - Score multiplier: 1.5x
// - All rewards affected by DifficultyMultiplier

// In your score component:
public void AddScore(int amount)
{
    int scaled = (int)(amount * GameApp.Instance.DifficultyMultiplier);
    CurrentScore += scaled;
}
```

### Achievement System

```csharp
// Unlock achievements
GameApp.Instance.UnlockAchievement("first_kill");
GameApp.Instance.UnlockAchievement("level_5_reached");

// Listen for unlocks
GameApp.Instance.AchievementUnlocked += (id) =>
{
    ShowNotification($"Achievement Unlocked: {id}");
};

// Check unlocked
if (GameApp.Instance.HasAchievement("boss_defeated"))
    ShowBossDefeatedCutscene();
```

### Statistics Tracking

```csharp
public override void _Process(double delta)
{
    // Automatically tracked:
    // - GameApp.Instance.SessionPlaytimeSeconds
    // - GameApp.Instance.CurrentSessionScore
    // - GameApp.Instance.CurrentLevel
    
    // On game end:
    GameApp.Instance.RecordGameEnd(playerWon: true);
    
    // Now available:
    // - GamesWonTotal incremented
    // - BestScore updated
    // - WinRate recalculated
    // - Achievements checked
}
```

### Dev Mode for Testing

```csharp
// Enable dev cheats
GameApp.Instance.ToggleDevMode();

// Now available:
GameApp.Instance.InfiniteLives = true;
GameApp.Instance.SkipTutorial = true;

// Or in debug console (backtick key):
> dev_mode on
> infinite_lives
> skip_tutorial
```

---

## Part 7: Complete Example Game Flow

```
┌─────────────────────────────────────┐
│         MAIN MENU                   │
│  - New Game → Set difficulty        │
│  - Load Game → Load saved state     │
│  - Settings → Rebind keys           │
└──────────┬──────────────────────────┘
           │
           ↓
┌─────────────────────────────────────┐
│      GAME INITIALIZATION            │
│  GameApp.SetGameRunning(true)       │
│  GameApp.SetDifficulty(Normal)      │
│  WeatherSystem enabled              │
│  Autosave starts (every 5 min)      │
└──────────┬──────────────────────────┘
           │
           ↓
┌─────────────────────────────────────┐
│         GAMEPLAY LOOP               │
│  _Process:                          │
│   - Player moves                    │
│   - Enemies AI (state machines)     │
│   - UI updates (data binding)       │
│   - Weather effects (wind, etc.)    │
│   - Score += damage dealt           │
│   - Track analytics                 │
└──────────┬──────────────────────────┘
           │
    ┌──────┴───────┐
    ↓              ↓
 PAUSE        ENEMY DEFEATED
    │              │
    ├─ Save        ├─ Unlock achievement
    ├─ Load        ├─ Add to score
    ├─ Settings    ├─ Track kill
    │              │
    └──────┬───────┘
           ↓
┌─────────────────────────────────────┐
│      GAME END (Boss/Level Complete) │
│  GameApp.RecordGameEnd(true)        │
│  Update statistics                  │
│  Check achievements                 │
│  Show results screen                │
└──────────┬──────────────────────────┘
           │
           ↓
┌─────────────────────────────────────┐
│    RETURN TO MAIN MENU              │
│  Save final state                   │
│  Cleanup (clear weather, etc.)      │
└─────────────────────────────────────┘
```

---

## Part 8: Troubleshooting

### Save Files Not Appearing
```
✅ Check: IsGameRunning = true (save disabled in menu)
✅ Check: GameStateManagerComponent is in scene
✅ Check: Components implement ISaveable correctly
✅ Check: user://saves/ folder exists
```

### Weather Not Showing
```
✅ Check: WeatherSystemComponent is in main scene
✅ Check: EnableWeather = true in game_info.tres
✅ Check: CanvasModulate exists for ambient tinting
✅ Check: WeatherParticles node created in _Ready
```

### Keybinds Not Working
```
✅ Check: KeybindManagerComponent in scene
✅ Check: Register keybinds in _Ready (don't forget!)
✅ Check: Callbacks are actually assigned
✅ Check: Not disabled with SetEnabled(false)
```

### Data Binding Not Updating
```
✅ Check: AutoRefresh = true on DataBinderHostComponent
✅ Check: Source object implements proper properties
✅ Check: Binding target node exists and is valid
✅ Check: Poll interval not too long (default 0.1s)
```

---

## Part 9: Performance Tips

### Save/Load Performance
- Keep component count reasonable (<50 per scene)
- ISaveable.Save/Load should be O(1) or O(n) where n is small
- JSON serialization is fast for typical game data
- Use custom data dictionary for game-specific state

### Weather System Performance
- Particle count scales with difficulty (export adjustable)
- Disable fog shader if not visible (export toggle)
- Wind calculations are O(1) per frame
- Cloud rendering uses canvas layers (efficient)

### Data Binding Performance
- Default poll interval (0.1s) is usually fine
- Increase interval if many bindings
- Use OneWay mode (not TwoWay) by default
- Formatters should be lightweight

### Coroutines Performance
- Job list is compact (O(n) where n = active jobs)
- Each job costs ~1μs per frame
- Cancel jobs when done (don't let them pile up)
- Job IDs use dictionary (O(1) cancellation)

---

## Checklist: Ship-Ready

Before releasing your game, verify:

```
CORE SYSTEMS
□ GameApp tracks all statistics correctly
□ Save/Load works on all platforms
□ Autosave doesn't freeze game
□ Weather system integrates with gameplay

COMPONENTS
□ Player controller implements ISaveable
□ Enemies use StateMachineComponent
□ UI uses DataBinderHostComponent
□ Timers use CoroutineHostComponent

FEATURES
□ Difficulty scaling affects gameplay
□ Achievements unlock correctly
□ Dev mode disabled in release builds
□ Debug console disabled in release builds

TESTING
□ Save game, close, load game → state restored
□ Play on Hard difficulty → content scales
□ Change weather → visual effects appear
□ Pause game → everything pauses correctly
```

---

## Next Resources

- 📖 `ecs/StateMachineComponent.md` — AI/animation patterns
- 📖 `ecs/ui/KeybindManagerComponent.md` — Input customization
- 📖 `ecs/ui/CoroutineHostComponent.md` — Timing & effects
- 📖 `ecs/ui/DataBinderHostComponent.md` — UI binding patterns
- 📖 `core/GameAppGuide.cs` — Full API reference
- 📖 `core/SaveLoadImplementationGuide.md` — Advanced save patterns
- 📖 `core/PHASE3_UTILITIES_GUIDE.md` — Math utilities

---

## Support

If you get stuck:
1. Check the relevant guide above
2. Look at code examples in `ecs/` for working implementations
3. Enable debug console (backtick key) to inspect state
4. Check `game_info.tres` configuration

---

**Ready to build amazing games with Beep.Godot!** 🎮

