# Save/Load Implementation Guide

## Overview

Your game now has a **universal save/load system** that automatically discovers and syncs game state. Developers just need to implement `ISaveable` on their components.

---

## Quick Start: Make Your Component Saveable

### Step 1: Implement ISaveable Interface

```csharp
public partial class MyComponent : GameplayComponent, ISaveable
{
    public void Save(GameBuilder.GameStateData state) { }
    public void Load(GameBuilder.GameStateData state) { }
}
```

### Step 2: Implement Save() Method

Sync component state **TO** the global GameStateData:

```csharp
public void Save(GameBuilder.GameStateData state)
{
    state.Combat.Health = CurrentHealth;
    state.Combat.MaxHealth = MaxHealth;
    
    // Or use custom data for game-specific state:
    state.GameData["my_state_key"] = MyValue;
}
```

### Step 3: Implement Load() Method

Restore component state **FROM** the global GameStateData:

```csharp
public void Load(GameBuilder.GameStateData state)
{
    CurrentHealth = state.Combat.Health;
    MaxHealth = state.Combat.MaxHealth;
    
    // Emit signals to notify UI
    EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
}
```

---

## Real Examples in This Project

### 1. HealthComponent (Combat State)

**File:** `ecs/HealthComponent.cs`

```csharp
public void Save(GameBuilder.GameStateData state)
{
    state.Combat.Health = CurrentHealth;
    state.Combat.MaxHealth = MaxHealth;
}

public void Load(GameBuilder.GameStateData state)
{
    CurrentHealth = state.Combat.Health;
    MaxHealth = state.Combat.MaxHealth;
    EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
}
```

**What gets saved:** Current health, max health (for UI sync)

---

### 2. InventoryComponent (Inventory State)

**File:** `ecs/InventoryComponent.cs`

```csharp
public void Save(GameBuilder.GameStateData state)
{
    state.Inventory.Items.Clear();
    state.Inventory.MaxSlots = MaxSlots;
    
    foreach (var item in Slots)
    {
        if (item != null)
            state.Inventory.Items[item.Id] = item.Quantity;
    }
}

public void Load(GameBuilder.GameStateData state)
{
    Clear();
    MaxSlots = state.Inventory.MaxSlots;
    
    foreach (var (itemId, quantity) in state.Inventory.Items)
        AddItem(itemId, quantity);
}
```

**What gets saved:** All items and quantities

---

### 3. PlayerMovementComponent (Movement State)

**File:** `ecs/PlayerMovementComponent.cs`

```csharp
public void Save(GameBuilder.GameStateData state)
{
    state.Movement.PositionX = _node2D.GlobalPosition.X;
    state.Movement.PositionY = _node2D.GlobalPosition.Y;
    state.Movement.VelocityX = _velocity.X;
    state.Movement.VelocityY = _velocity.Y;
    state.Movement.FacingDirection = _facingDirection;
}

public void Load(GameBuilder.GameStateData state)
{
    _node2D.GlobalPosition = new Vector2(
        state.Movement.PositionX, 
        state.Movement.PositionY
    );
    _velocity = new Vector2(state.Movement.VelocityX, state.Movement.VelocityY);
    _facingDirection = state.Movement.FacingDirection;
}
```

**What gets saved:** Position, velocity, facing direction, rotation

---

### 4. ScoreComponent (Custom Data)

**File:** `ecs/ScoreComponent.cs`

```csharp
public void Save(GameBuilder.GameStateData state)
{
    state.GameData["player_score"] = CurrentScore;
}

public void Load(GameBuilder.GameStateData state)
{
    if (state.GameData.TryGetValue("player_score", out var score))
        CurrentScore = (int)score;
}
```

**What gets saved:** Player's current session score

---

## GameStateData Structure

Your components have access to these pre-defined state containers:

### Predefined Containers

```csharp
// Combat/Health State
state.Combat.Health = 50f;
state.Combat.MaxHealth = 100f;
state.Combat.Lives = 3;
state.Combat.Stamina = 80f;

// Movement State
state.Movement.PositionX = 100f;
state.Movement.PositionY = 200f;
state.Movement.VelocityX = 5f;
state.Movement.VelocityY = -10f;
state.Movement.FacingDirection = -1f; // -1 = left, 1 = right
state.Movement.Rotation = 0f;

// Inventory State
state.Inventory.Items["sword"] = 1;
state.Inventory.Items["potion"] = 5;
state.Inventory.MaxSlots = 20;

// Progression State
state.Progression.CompletedQuests.Add("slay_dragon");
state.Progression.UnlockedAchievements.Add("first_kill");
state.Progression.Level = 5;
state.Progression.Experience = 1250;

// World State
state.World.Entities.Add(new EntityStateData { ... });
state.World.Switches["door_open"] = true;
```

### Custom Data Dictionary

For game-specific state:

```csharp
// Save custom data
state.GameData["boss_health"] = 250;
state.GameData["cutscene_watched"] = true;
state.GameData["collected_coins"] = 42;

// Load custom data
if (state.GameData.TryGetValue("boss_health", out var health))
{
    CurrentHealth = (float)health;
}
```

---

## How It Works

### When Save Button Is Pressed

1. **GameStateManagerComponent** auto-discovers all `ISaveable` components in the scene tree
2. Calls `Save(state)` on each component
3. Components populate the global `GameStateData`
4. JSON is written to disk (`user://saves/save_0.json`, etc.)

### When Load Button Is Pressed

1. **GameStateManagerComponent** reads JSON from disk
2. Deserializes to `GameStateData`
3. Calls `Load(state)` on each `ISaveable` component
4. Components restore their state from the global data

### What Gets Discovered Automatically

```csharp
// These are auto-discovered by GameStateManagerComponent:
var saveables = SaveableHelper.FindAllSaveables(GetTree().Root);

// For each saveable component found:
foreach (var saveable in saveables)
{
    saveable.Save(currentState);      // On save
    saveable.Load(loadedState);       // On load
}
```

---

## Common Patterns

### Pattern 1: Simple Value Save

```csharp
// Combat component saving health
public void Save(GameBuilder.GameStateData state)
{
    state.Combat.Health = CurrentHealth;
}

public void Load(GameBuilder.GameStateData state)
{
    SetHealth(state.Combat.Health);
}
```

### Pattern 2: Collection Save

```csharp
// Inventory saving multiple items
public void Save(GameBuilder.GameStateData state)
{
    state.Inventory.Items.Clear();
    foreach (var item in _items)
        state.Inventory.Items[item.Key] = item.Value;
}

public void Load(GameBuilder.GameStateData state)
{
    _items.Clear();
    foreach (var (id, qty) in state.Inventory.Items)
        _items[id] = qty;
}
```

### Pattern 3: Custom Game Data

```csharp
// Boss component saving custom state
public void Save(GameBuilder.GameStateData state)
{
    state.GameData["boss_phase"] = CurrentPhase;
    state.GameData["boss_minions_spawned"] = MinionsSpawned;
}

public void Load(GameBuilder.GameStateData state)
{
    if (state.GameData.TryGetValue("boss_phase", out var phase))
        CurrentPhase = (int)phase;
    if (state.GameData.TryGetValue("boss_minions_spawned", out var minions))
        MinionsSpawned = (int)minions;
}
```

### Pattern 4: Position/Transform Save

```csharp
// Movement component saving world position
public void Save(GameBuilder.GameStateData state)
{
    state.Movement.PositionX = _node2D.GlobalPosition.X;
    state.Movement.PositionY = _node2D.GlobalPosition.Y;
    state.Movement.Rotation = _node2D.Rotation;
}

public void Load(GameBuilder.GameStateData state)
{
    _node2D.GlobalPosition = new Vector2(
        state.Movement.PositionX,
        state.Movement.PositionY
    );
    _node2D.Rotation = state.Movement.Rotation;
}
```

---

## Testing Your Implementation

### Test Save/Load

1. Start game
2. Change component state (take damage, move, get item)
3. Press "Save Game"
4. Close game
5. Open game → "Load Game"
6. Verify state is restored

### Check What Saved

```bash
# Look at the save file in:
user://saves/save_0.json

# You'll see the structure:
{
  "metadata": { "save_name": "Save Game", "timestamp": 1234567890, ... },
  "movement": { "position_x": 100, "position_y": 200, ... },
  "combat": { "health": 75, "max_health": 100, ... },
  "inventory": { "items": { "sword": 1, "potion": 5 }, ... },
  "game_data": { "boss_phase": 2, "custom_key": "value" }
}
```

---

## Quick Reference

| Component | What to Save | State Container | Example |
|-----------|-------------|-----------------|---------|
| HealthComponent | Health, MaxHealth | `state.Combat` | Line 84-90 |
| InventoryComponent | Items, MaxSlots | `state.Inventory` | Line 311-330 |
| PlayerMovement | Position, Velocity | `state.Movement` | Line 74-80 |
| ScoreComponent | Score | `state.GameData` | Line 56-60 |
| CustomBoss | Boss phase, minions | `state.GameData` | Shown above |

---

## FAQ

**Q: How do I save component-specific state not in predefined containers?**  
A: Use `state.GameData["key"]` for anything custom. It's a free-form dictionary.

**Q: What if my component doesn't exist on load?**  
A: GameStateManagerComponent only calls `Load()` on components that exist. Extra saved state is ignored.

**Q: Do I need to handle null/missing data?**  
A: Yes. Use `TryGetValue()` to safely check before accessing custom data.

**Q: Can I save/load without the UI?**  
A: Yes. Call `GameStateManagerComponent.Save(slot)` and `Load(slot)` directly from code.

**Q: How do I autosave?**  
A: Set `AutosaveEnabled = true` and `AutosaveIntervalSeconds = 300` in GameInfo or GameStateManagerComponent.

---

## Files Reference

- **HealthComponent.cs** — Combat state example
- **InventoryComponent.cs** — Collection save example
- **PlayerMovementComponent.cs** — Transform/position example
- **ScoreComponent.cs** — Custom data example
- **GameStateData.cs** — State containers definition
- **GameStateManagerComponent.cs** — Auto-discovery engine
- **SaveGameMenuComponent.cs** — Save UI
- **LoadGameMenuComponent.cs** — Load UI

---

## Next Steps

1. **Audit your components** — Which ones need state persistence?
2. **Implement ISaveable** — Add Save/Load methods
3. **Test** — Save, close, load, verify
4. **Wire to UI** — Connect "Save" buttons to ShowSaveMenu()

That's it! Your components are now fully saveable. 🎮
