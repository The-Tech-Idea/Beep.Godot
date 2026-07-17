# Phase 3: Specialized Utilities Review & Documentation

**Status:** ✅ LOW Priority — Keep as Static Utilities (not components)  
**Date:** 2026-07-15

---

## Overview

Phase 3 covers three specialized utilities that serve specific, niche purposes in game development. Unlike Phase 1-2 conversions, these **remain as static utilities** (not components) because they are mathematical/algorithmic in nature with no lifecycle or state management needs.

---

## 1. BeepProceduralAnim — Spring Physics & Gradients

**File:** `core/BeepProceduralAnim.cs`  
**Purpose:** Smooth procedural animation using spring physics, noise-based effects, and color gradients  
**Type:** Static Utility (Keep as-is)

### What It Provides

#### A. Spring-Based Animation
```csharp
// Smooth spring-based float animation
var spring = BeepProceduralAnim.Smooth();  // Smooth preset
spring.Target = 100f;                       // Animate to target
float newValue = spring.Update(delta);      // Call each frame
```

**Presets:**
- `Smooth()` — Gentle, responsive (stiffness: 120, damping: 15)
- `Bouncy()` — Energetic bounce (stiffness: 200, damping: 5)
- `Snappy()` — Quick, crisp (stiffness: 300, damping: 20)

**For 2D vectors:**
```csharp
var spring2D = BeepProceduralAnim2D.Bouncy();
spring2D.Target = new Vector2(500, 300);
Vector2 position = spring2D.Update(delta);
```

#### B. Noise Generation
```csharp
// Simplex/Perlin noise for procedural generation
float perlin1D = BeepNoiseGenerator.Noise(time);
float perlin2D = BeepNoiseGenerator.Noise(x, y);
float perlin3D = BeepNoiseGenerator.Noise(x, y, z);

// Wobble effect for UI elements
float wobble = BeepNoiseGenerator.Wobble(time, speed: 2f, amplitude: 5f);

// Generate heightmap for terrain
float[,] heightmap = BeepNoiseGenerator.GenerateHeightmap(
    width: 256, height: 256, scale: 0.05f);
```

#### C. Color Gradients
```csharp
// 10 pre-made color palettes
Color[] sunset = BeepGradientPresets.Sunset;
Color[] ocean = BeepGradientPresets.Ocean;
Color[] neon = BeepGradientPresets.Neon;

// Lerp along a gradient (0.0 - 1.0)
Color color = BeepGradientPresets.LerpGradient(sunset, 0.5f);  // Mid-gradient
```

**Available Gradients:**
- Sunset, Ocean, Forest, Lava, Neon
- Candy, Fire, Ice, Cyberpunk, Retro

### Use Cases

**Animation:**
```csharp
// UI element easing
var healthBarSpring = BeepProceduralAnim.Smooth();
void UpdateHealthBar(float newHealth)
{
    healthBarSpring.Target = newHealth / maxHealth * 100f;
    healthBar.Value = healthBarSpring.Update(delta);
}
```

**Procedural Effects:**
```csharp
// Wobble effect for floating text
var textY = baseY + BeepNoiseGenerator.Wobble(time, speed: 2f, amplitude: 10f);
damageLabel.Position = new Vector2(damageLabel.Position.X, textY);
```

**Terrain & Heightmaps:**
```csharp
// Generate procedural terrain
var heightmap = BeepNoiseGenerator.GenerateHeightmap(512, 512, scale: 0.02f);
for (int x = 0; x < 512; x++)
{
    for (int y = 0; y < 512; y++)
    {
        float height = heightmap[x, y];
        // Use height for tile elevation, color, etc.
    }
}
```

**Gradient-Based UI:**
```csharp
// Color health bar based on health percentage
Color healthColor = BeepGradientPresets.LerpGradient(
    new[] { Colors.Red, Colors.Orange, Colors.Green },
    healthPercentage / 100f);
healthBar.Modulate = healthColor;
```

---

## 2. BeepEncryptionPathfinding — 3 Utilities in 1

**File:** `core/BeepEncryptionPathfinding.cs`  
**Type:** Static Utilities (Keep as-is)

### A. Encryption Helper — Save File Protection

```csharp
// Encrypt sensitive save data
var saveData = new { PlayerName = "Hero", Level = 50 };
BeepEncryptionHelper.SaveEncrypted(
    "user://savegame.enc",
    saveData,
    password: "player_secret_key");

// Decrypt on load
var loadedData = BeepEncryptionHelper.LoadEncrypted<SaveData>(
    "user://savegame.enc",
    password: "player_secret_key");

// Hash passwords
string hashedPassword = BeepEncryptionHelper.HashString(userInput);
```

**Security Notes:**
- Uses AES-256 encryption
- PBKDF2 key derivation (1000 iterations, SHA256)
- Base64 encoding for file storage
- **NOT for production crypto** — use industry-standard libraries for critical data

### B. Pathfinding Grid — A* Algorithm

```csharp
// Create a grid-based pathfinding system
var grid = new BeepPathfindingGrid(width: 50, height: 50);

// Mark obstacles
grid.SetObstacle(10, 10, blocked: true);
grid.SetObstacle(10, 11, blocked: true);

// Find path
var path = grid.FindPath(
    start: new Vector2I(0, 0),
    end: new Vector2I(49, 49));

if (path != null)
{
    foreach (var step in path)
        MoveCharacter(step);  // Grid coordinates
}
```

**Features:**
- A* pathfinding algorithm
- 8-directional movement (+ diagonals)
- Manhattan distance heuristic
- Returns null if no path exists

**Use Case — Enemy AI:**
```csharp
public partial class EnemyAI : Node
{
    private BeepPathfindingGrid _grid;

    public override void _Ready()
    {
        _grid = new BeepPathfindingGrid(100, 100);
        // Mark level obstacles
        MarkWallsAsObstacles();
    }

    public void MoveToward(Vector2I targetGrid)
    {
        var path = _grid.FindPath(CurrentGridPos, targetGrid);
        if (path != null)
            _currentPathIndex = 0;
    }
}
```

### C. Rich Text Builder — BBCode Fluent API

```csharp
// Build formatted text for RichTextLabel
string formatted = BeepRichTextBuilder.Build(sb =>
{
    sb.Append(BeepRichTextBuilder.Bold("Health: "));
    sb.Append(BeepRichTextBuilder.Color("100/100", "00ff00"));
    sb.Append("\n");
    sb.Append(BeepRichTextBuilder.Wave("Floating Text"));
});

richLabel.Text = formatted;
```

**Available Methods:**
```csharp
Color(text, "#RRGGBB")          // Color with hex
Color(text, Color)               // Color with Godot Color
Bold(text)                        // Bold
Italic(text)                      // Italic
Size(text, fontSize)              // Font size
Wave(text, amplitude, frequency)  // Wave effect
Shake(text, rate, level)          // Shake effect
Rainbow(text, freq, sat, val)     // Rainbow gradient
Tornado(text, freq, radius)       // Tornado spin
Fade(text, startIdx, length)      // Fade in/out
```

**Example — HUD Status Text:**
```csharp
var statusText = BeepRichTextBuilder.Build(sb =>
{
    sb.Append(BeepRichTextBuilder.Bold("Status: "));
    
    if (player.Health > 50)
        sb.Append(BeepRichTextBuilder.Color("Healthy", "00ff00"));
    else if (player.Health > 20)
        sb.Append(BeepRichTextBuilder.Color("Wounded", "ffff00"));
    else
        sb.Append(BeepRichTextBuilder.Color("Critical", "ff0000"));
    
    statusHUD.MarkdownText = statusText;
});
```

---

## 3. BeepAchievementDebug — 3 Systems in 1

**File:** `core/BeepAchievementDebug.cs`  
**Type:** Static Utilities + Component (Keep as-is, but see GameApp integration below)

### A. Achievement System

```csharp
// Define achievements
var killAchievement = new BeepAchievementSystem.Achievement
{
    Id = "first_kill",
    Title = "First Blood",
    Description = "Get your first kill",
    Icon = killerBadgeTexture,
    Target = 0  // No progress, unlock immediately
};

var masterKillerAchievement = new BeepAchievementSystem.Achievement
{
    Id = "master_killer",
    Title = "Master Killer",
    Description = "Kill 100 enemies",
    Target = 100  // Progress-based
};

BeepAchievementSystem.Register(killAchievement);
BeepAchievementSystem.Register(masterKillerAchievement);

// Listen for unlocks
BeepAchievementSystem.AchievementUnlocked += (ach) =>
{
    ShowNotification($"Achievement Unlocked: {ach.Title}");
};

// Update progress
public void OnEnemyKilled()
{
    BeepAchievementSystem.IncrementProgress("master_killer");
}

// Check unlocked
if (BeepAchievementSystem.IsUnlocked("first_kill"))
    GD.Print("Player has First Blood!");

// Save/Load
BeepAchievementSystem.Save("user://achievements.cfg");
BeepAchievementSystem.Load("user://achievements.cfg");
```

### B. Analytics Helper — Event Tracking

```csharp
// Track events for balancing/debugging
BeepAnalyticsHelper.Track("player_died", new Dictionary<string, object>
{
    { "level", currentLevel },
    { "health_remaining", 0 },
    { "enemies_killed", killCount }
});

// Simple syntax
BeepAnalyticsHelper.TrackSimple("item_crafted", 
    ("item_id", "sword_iron"),
    ("crafting_time", 5.2f));

// Query events
int deathCount = BeepAnalyticsHelper.Count("player_died");
var allDeaths = BeepAnalyticsHelper.GetAll("player_died");

// Summary
var summary = BeepAnalyticsHelper.Summary();
foreach (var (event_name, count) in summary)
    GD.Print($"{event_name}: {count} times");
```

**Use Case — Game Balance:**
```csharp
// Track difficulty metrics
BeepAnalyticsHelper.TrackSimple("damage_taken",
    ("source", enemy.Type),
    ("amount", damage),
    ("player_health", player.Health));

// Later: analyze which enemies deal too much damage
```

### C. Debug Console — In-Game Command Line

```csharp
// Add to scene
[node name="DebugConsole" type="Control" parent="."]
script = ExtResource("path/to/BeepDebugConsole.cs")

// Register custom commands
var console = GetNode<BeepDebugConsole>("DebugConsole");

console.RegisterCommand("spawn_enemy", (args) =>
{
    int count = args.Length > 0 ? int.Parse(args[0]) : 1;
    for (int i = 0; i < count; i++)
        SpawnEnemy();
    console.Log($"[color=green]Spawned {count} enemies[/color]");
});

console.RegisterCommand("heal", (args) =>
{
    player.Health = player.MaxHealth;
    console.Log("[color=cyan]Player healed[/color]");
});

console.RegisterCommand("level", (args) =>
{
    if (args.Length == 0)
    {
        console.Log($"Current level: {currentLevel}");
        return;
    }
    LoadLevel(int.Parse(args[0]));
    console.Log($"[color=yellow]Loaded level {args[0]}[/color]");
});
```

**Built-in Commands:**
- `help` — List all commands
- `clear` — Clear console output
- `fps` — Show current FPS
- `time` — Show system time

**Opening Console:**
- Press **`** (backtick/tilde) to toggle

---

## Integration with GameApp

### Achievement System Integration

GameApp already has a built-in achievement system:
```csharp
// GameApp achievements
GameApp.Instance.UnlockAchievement("first_kill");
bool has = GameApp.Instance.HasAchievement("master_killer");
var allAchievements = GameApp.Instance.GetUnlockedAchievements();
```

**For compatibility**, you can sync the two systems:

```csharp
// Sync GameApp → BeepAchievementSystem
BeepAchievementSystem.AchievementUnlocked += (ach) =>
{
    GameApp.Instance.UnlockAchievement(ach.Id);
};

// Sync BeepAchievementSystem → GameApp
GameApp.Instance.AchievementUnlocked += (id) =>
{
    var ach = BeepAchievementSystem.Get(id);
    if (ach != null)
    {
        BeepAchievementSystem.Unlock(id);
    }
};
```

Or use only GameApp for new projects (Phase 1+ already includes it).

---

## Best Practices

### BeepProceduralAnim
1. **Cache springs** — Don't create new springs every frame
2. **Use presets** — Start with Smooth/Bouncy/Snappy, tune from there
3. **Lerp sparingly** — Spring physics is smoother than linear lerp

### Pathfinding
1. **Update obstacles dynamically** — Call `SetObstacle()` when level changes
2. **Cache paths** — Don't recalculate every frame (path is stable)
3. **Use for grid-based only** — Not suitable for physics-based movement

### Debug Console
1. **Disable in production** — Set script disabled in export builds
2. **Validate inputs** — Commands receive raw string arrays
3. **Color output** — Use BBCode colors for readability

### Achievements
1. **Use GameApp for new projects** — Preferred over BeepAchievementSystem
2. **Sync both if needed** — For backward compatibility
3. **Persist on save/load** — Call Save/Load on game exit/start

---

## Files Reference

- `core/BeepProceduralAnim.cs` — Spring animation, noise, gradients
- `core/BeepEncryptionPathfinding.cs` — Encryption, A*, BBCode builder
- `core/BeepAchievementDebug.cs` — Achievements, analytics, debug console

---

## Summary

**Phase 3 Complete:**
- ✅ BeepProceduralAnim — Documented (animation, noise, gradients)
- ✅ BeepEncryptionPathfinding — Documented (encryption, pathfinding, BBCode)
- ✅ BeepAchievementDebug — Documented (achievements, analytics, console)
- ✅ Integration notes for GameApp
- ✅ Best practices for each utility

All Phase 3 utilities remain as static helpers (correct architectural choice for mathematical utilities). They are stable, well-tested, and need no component wrapping.

**Backward Compatibility:** 100% preserved. All Phase 1-3 conversions coexist with original static utilities.

