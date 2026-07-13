# Enhancement Suggestions

Ideas for new effects, scenes, and component enhancements — based on the current
component inventory (99 components across UI/Gameplay/Controller/World categories).
All suggestions follow the Godot skill rules: `[Tool] [GlobalClass]`, `[Export]`
for inspector-tunable parameters, `[Signal]` for communication, composable node
children (never inheritance), Godot file API (never `System.IO`).

**Already done (do NOT suggest):**
- Movement: Jump, Dash, Slide, Hover, Fly, Glide, WallJump — all created.
- Weather: WeatherSystem, DayNight, WindField, WeatherHUD, LightningBolt — all created.
- Inventory: InventoryGridComponent (display), InventoryComponent (data) — base exists.
- Dialog: DialogComponent (data), DialogUIComponent (UI) — base exists.
- Effects: Ripple, Pulse, Shake, Slide, Flash, FloatingText, ChromaticAberration, Vignette — all exist.

---

## 1. Inventory Enhancements

`InventoryGridComponent` (UIComponent) exists as a display grid. `InventoryComponent`
(GameplayComponent) exists as a data model. The gap is **interaction**.

### InventoryDragComponent (UIComponent) — NEW
Drag-and-drop items between slots using Godot's native drag API:
```
[Tool][GlobalClass]
public partial class InventoryDragComponent : UIComponent
{
    [Export] public PackedScene? DragGhostScene { get; set; }  // visual ghost
    [Signal] public delegate void ItemMovedEventHandler(int fromSlot, int toSlot);
}
```
- Uses `_GetDragData()`, `_CanDropData()`, `_DropData()` on Control
- Emits `ItemMoved(from, to)` — InventoryComponent handles the data swap
- Place as child of the same Control as InventoryGridComponent

### InventoryStackComponent (UIComponent) — NEW
Stackable items (potions, ammo, currency):
```
[Export] public int MaxStackSize { get; set; } = 99;
[Signal] public delegate void StackSplitEventHandler(int slot, int amount);
```
- Right-click → split stack
- Shift+drag → move partial stack
- Merges compatible stacks on drop

### InventorySortComponent (UIComponent) — NEW
```
[Export] public SortMode Mode { get; set; } = SortMode.ByType;
// ByType, ByRarity, ByName, ByQuantity
[Signal] public delegate void SortRequestedEventHandler();
```

### InventoryTooltipComponent (UIComponent) — ENHANCEMENT
Reuses the existing `TooltipComponent` pattern but reads item data:
- Hover a slot → shows item name, stats, description
- `[Export] public float HoverDelay { get; set; } = 0.3f;`

---

## 2. Combat Enhancements

### DamageTypeComponent (GameplayComponent) — NEW
Typed damage system (physical/fire/ice/poison/holy):
```
[Export] public DamageType Type { get; set; } = DamageType.Physical;
[Export] public float Multiplier { get; set; } = 1.0f;
```
- Attach to AttackComponent or ProjectileComponent
- `HealthComponent` reads the incoming type and checks `ResistanceDictionary`
- Signals: `DamageDealt(target, type, amount)`

### ResistanceComponent (GameplayComponent) — NEW
```
[Export] public Godot.Collections.Dictionary<DamageType, float> Resistances { get; set; }
// value 0 = immune, 0.5 = half, 1 = normal, 2 = double (weak)
```
- Pairs with HealthComponent — modifies incoming damage
- Signal: `ResistanceBroken(DamageType)` when a threshold is exceeded

### HitStopComponent (WorldComponent) — NEW
Brief freeze-frame on heavy hits for impact feel:
```
[Export] public float FreezeDuration { get; set; } = 0.05f;
[Export] public float VelocityThreshold { get; set; } = 300f;
```
- On `HealthComponent.Damaged`, briefly sets `Engine.TimeScale = 0`
- Restores time after `FreezeDuration`
- Signal: `HitStopTriggered()`

### HitSparkComponent (WorldComponent) — NEW
Particle burst on melee/projectile impact:
```
[Export] public PackedScene? SparkScene { get; set; }  // drag-and-drop particle scene
[Export] public Color SparkColor { get; set; }
```
- Listens to sibling `HealthComponent.Damaged`
- Spawns particle burst at the collision point

---

## 3. Gameplay Systems

### CraftingComponent (GameplayComponent) — NEW
Recipe-based item crafting:
```
[Export] public CraftingRecipe[] Recipes { get; set; }  // array of recipe resources
[Signal] public delegate void CraftedEventHandler(string resultItemId);
[Signal] public delegate void CraftFailedEventHandler(string reason);
```
- Reads sibling `InventoryComponent` for available materials
- Deducts materials, emits `Crafted`
- `CraftingRecipe` is a `[GlobalClass] Resource` with `InputItems[]` + `OutputItem`

### QuestComponent (GameplayComponent) — NEW
Quest tracking with objectives:
```
[Export] public QuestObjective[] Objectives { get; set; }
[Signal] public delegate void ObjectiveCompletedEventHandler(int index);
[Signal] public delegate void QuestCompletedEventHandler();
[Signal] public delegate void QuestFailedEventHandler();
```
- `QuestObjective` Resource: `Description`, `Type` (Kill/Collect/Reach/Talk),
  `TargetId`, `RequiredCount`, `CurrentCount`
- Methods: `ProgressObjective(id, amount)`, `CompleteObjective(index)`

### LevelingComponent (GameplayComponent) — NEW
XP and leveling for RPGs/roguelikes:
```
[Export] public int MaxLevel { get; set; } = 99;
[Export] public float BaseXp { get; set; } = 100f;
[Export] public float XpMultiplier { get; set; } = 1.5f;  // exponential growth
[Signal] public delegate void XpChangedEventHandler(float current, float needed);
[Signal] public delegate void LevelUpEventHandler(int newLevel);
```
- `AddXp(amount)` → checks threshold → emits `LevelUp`
- `StatPoints` awarded per level for spending

### CooldownComponent (GameplayComponent) — NEW
Generic ability cooldown timer:
```
[Export] public float CooldownDuration { get; set; } = 1f;
[Export] public bool StartOnReady { get; set; } = false;
[Signal] public delegate void CooldownReadyEventHandler();
[Signal] public delegate void CooldownProgressEventHandler(float pct);
```
- `Trigger()` starts the cooldown
- `IsReady` property
- Can be stacked for multi-ability entities

---

## 4. UI / HUD Enhancements

### BossHealthBarComponent (UIComponent) — NEW
Segmented boss health bar with phases:
```
[Export] public int PhaseCount { get; set; } = 3;
[Export] public Color[] PhaseColors { get; set; }
[Export] public bool ShowAtTop { get; set; } = true;
[Signal] public delegate void PhaseChangedEventHandler(int phase);
```
- Listens to sibling `HealthComponent`
- Each phase = different color segment
- Slide-in/slide-out animation on boss appear/death

### ComboCounterComponent (UIComponent) — NEW
Fighting/combo game display:
```
[Export] public float ResetTime { get; set; } = 2f;
[Export] public int FontSize { get; set; } = 48;
[Signal] public delegate void ComboChangedEventHandler(int count);
```
- `Increment()` adds to combo, resets timer
- Font grows + shakes with each increment
- Auto-resets to 0 after `ResetTime`

### ScreenFlashComponent (UIComponent) — NEW
Full-screen color flash on events:
```
[Export] public Color FlashColor { get; set; } = new(1, 0, 0, 0.5f);
[Export] public float Duration { get; set; } = 0.2f;
[Signal] public delegate void FlashCompleteEventHandler();
```
- `Flash()` creates a ColorRect overlay, tweens alpha up then down
- Different from `FlashComponent` (which flashes a sprite's material)

### BuffBarComponent (UIComponent) — NEW
Active buff/debuff icon display:
```
[Export] public int MaxSlots { get; set; } = 8;
[Export] public Vector2 IconSize { get; set; } = new(32, 32);
```
- Listens to sibling `StatusEffectComponent`
- Shows each active effect as an icon with remaining-duration progress ring
- Tooltip on hover shows effect description

---

## 5. World / Environment Enhancements

### DestructibleComponent (WorldComponent) — NEW
Breakable objects (crates, pots, walls, barriers):
```
[Export] public int HP { get; set; } = 1;
[Export] public PackedScene? DebrisScene { get; set; }  // drag-and-drop debris
[Export] public bool DropsOnDestroy { get; set; } = true;
[Signal] public delegate void DestroyedEventHandler(Vector2 position);
```
- Takes damage from `AttackComponent` / `ProjectileComponent`
- At HP 0: spawns debris, triggers `DropTableComponent`, emits `Destroyed`
- Can be chained (destroy support → ceiling collapses)

### AmbientAudioComponent (WorldComponent) — NEW
Zone-based ambient sound with crossfade:
```
[Export] public AudioStream? AmbientTrack { get; set; }  // drag-and-drop
[Export] public AudioStream? CombatTrack { get; set; }   // drag-and-drop
[Export] public float CrossfadeDuration { get; set; } = 1.5f;
```
- Area2D: enters zone → plays that zone's ambient track
- Combat proximity detection → crossfade to combat track
- Integrates with `WeatherSystemComponent` (storm = thunder ambient)

### MovingPlatformPathComponent (WorldComponent) — ENHANCEMENT
Enhance `MovingPlatformComponent` with Path2D support:
```
[Export] public Path2D? Path { get; set; }  // drag-and-drop a Path2D
[Export] public float Speed { get; set; } = 100f;
[Export] public bool Loop { get; set; } = true;
```
- Currently MovingPlatformComponent uses point-to-point
- This enhancement adds curve/path following via PathFollow2D

---

## 6. Scene Enhancements

### Save Slot UI (main_menu "Load Game" → save slot selection)
- 3 save slot buttons showing: game name, timestamp, playtime, level reached
- Delete button per slot
- Empty slots show "New Game"
- Wired via `MenuComponent` + `NavigationComponent` with action `load_slot_1/2/3`

### Difficulty Selector
- Add to main_menu: Easy/Normal/Hard `OptionButton`
- Writes `DifficultyMultiplier` to `game_info.tres`
- GameFlowComponent reads multiplier → adjusts `TargetScore`, `Lives`, enemy stats

### Keybind Remapping in Settings
- Replace "edit in Project Settings" hint with actual rebinding UI
- Click key → "Press any key..." overlay → assigns new InputMap action
- Reset to defaults button
- Save/load keybinds via `SaveManagerComponent`

### Volume Preview
- Play a test sound when audio slider is released in settings
- `[Export] public AudioStream? TestSound { get; set; }` on SettingsComponent

---

## 7. Polish / Feel

### SquashAndStretchComponent (ControllerComponent) — NEW
Visual juice on jump/land:
```
[Export] public float SquashAmount { get; set; } = 0.15f;
[Export] public float StretchDuration { get; set; } = 0.1f;
```
- Listens to sibling `JumpComponent.Jumped` → stretch Y
- Listens to `JumpComponent` / controller `Landed` → squash Y
- Uses `Scale` tween — works on any `Node2D`

### TrailComponent (UIComponent) — NEW
Motion trail behind fast-moving entities:
```
[Export] public int MaxPoints { get; set; } = 20;
[Export] public Color TrailColor { get; set; } = new(1, 1, 1, 0.5f);
[Export] public float FadeSpeed { get; set; } = 5f;
```
- Uses `Line2D` points sampled per-frame from parent position
- Alpha fades older points
- Good for dashes, projectiles, fast enemies

### FootstepComponent (WorldComponent) — NEW
Procedural footsteps with surface detection:
```
[Export] public AudioStream[]? FootstepSounds { get; set; }  // drag-and-drop array
[Export] public float MinSpeed { get; set; } = 50f;
[Export] public float StepInterval { get; set; } = 0.3f;
[Export] public float PitchVariation { get; set; } = 0.1f;
```
- Plays random footstep sound at `StepInterval` while moving
- Random pitch variation per step
- RayCast2D downward detects surface type for different sound sets

---

## Priority table

| Priority | Component / Enhancement | Category | Effort |
|---|---|---|---|
| HIGH | HitStopComponent | World | Small |
| HIGH | DamageTypeComponent + ResistanceComponent | Gameplay | Medium |
| HIGH | SquashAndStretchComponent | Controller | Small |
| HIGH | InventoryDragComponent | UI | Medium |
| HIGH | CooldownComponent | Gameplay | Small |
| MEDIUM | TrailComponent | UI | Small |
| MEDIUM | HitSparkComponent | World | Small |
| MEDIUM | ScreenFlashComponent | UI | Small |
| MEDIUM | BossHealthBarComponent | UI | Small |
| MEDIUM | ComboCounterComponent | UI | Small |
| MEDIUM | DestructibleComponent | World | Small |
| MEDIUM | BuffBarComponent | UI | Small |
| MEDIUM | FootstepComponent | World | Small |
| MEDIUM | LevelingComponent | Gameplay | Medium |
| MEDIUM | CraftingComponent | Gameplay | Medium |
| LOW | QuestComponent | Gameplay | Large |
| LOW | InventoryStackComponent | UI | Medium |
| LOW | InventorySortComponent | UI | Small |
| LOW | AmbientAudioComponent | World | Medium |
| LOW | MovingPlatformPath (enhancement) | World | Small |
| LOW | Save Slot UI | Scene | Medium |
| LOW | Difficulty Selector | Scene | Small |
| LOW | Keybind Remapping | Scene | Medium |
| LOW | Volume Preview | Scene | Small |
