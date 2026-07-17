# DataBinderHostComponent Implementation Guide

## Overview

`DataBinderHostComponent` is a data-binding engine that keeps C# object properties synchronized with Godot UI controls automatically. Replaces manual property assignments with declarative bindings.

**When to use:**
- Display player stats (health, mana, level) in real-time
- Bind form data to model objects
- Two-way sync (UI → model and model → UI)
- Complex UI updates from changing game state

**Benefits:**
- No manual "Update HUD" methods
- Bidirectional sync possible
- Formatter functions for display logic
- Instance-based (not global)

---

## Quick Start

### 1. Add to Scene

```gdscript
[node name="DataBinder" type="Node" parent="HUD"]
script = ExtResource("path/to/DataBinderHostComponent.cs")
auto_refresh = true
poll_interval = 0.1
```

### 2. Create Bindings

```csharp
var binder = GetNode<DataBinderHostComponent>("DataBinder");

// Bind player health to progress bar
binder.BindProgress(player, nameof(player.Health), healthBar);

// Bind player level to label
binder.BindLabel(player, nameof(player.Level), levelLabel, "Level: {0}");

// Bind sound enabled to checkbox
binder.BindCheckBox(settings, nameof(settings.SoundEnabled), soundCheckbox);

// Generic binding with formatter
binder.Bind(inventory, nameof(inventory.ItemCount), itemLabel, "Text",
    BindingMode.OneWay, count => $"Items: {count}");
```

### 3. Done!

The UI updates automatically as the data changes. No more manual `healthBar.Value = player.Health;`

---

## API Reference

### Convenience Methods

```csharp
// Bind numeric property to progress bar
BindProgress(object source, string sourceProp, ProgressBar bar)
BindTextureProgress(object source, string sourceProp, TextureProgressBar bar)

// Bind to label
BindLabel(object source, string sourceProp, Label label, string format = "{0}")
BindRichLabel(object source, string sourceProp, RichTextLabel label)

// Bind boolean to checkbox/button
BindCheckBox(object source, string sourceProp, CheckBox check, BindingMode mode)

// Bind boolean to visibility
BindVisible(object source, string sourceProp, CanvasItem target)

// Bind color
BindColor(object source, string sourceProp, CanvasItem target)
```

### Generic Binding

```csharp
// Full control: any property to any UI property with formatter
Bind(object source, string sourceProp, Node target, string targetProp,
     BindingMode mode = BindingMode.OneWay,
     Func<object, object> formatter = null)
```

### Control & Query

```csharp
void RefreshAll()              // Force refresh all one-way bindings
void RefreshTwoWay()           // Push UI changes back to source
void RefreshProperty(string sourceProp)  // Refresh a specific property

void Unbind(object source)     // Remove all bindings for an object
void Unbind(object source, string sourceProp)  // Remove specific binding
void Clear()                   // Clear all bindings

int BindingCount { get; }      // Number of active bindings
```

### Signals

```csharp
[Signal] BindingCreated(string sourceProperty)
[Signal] BindingRemoved(string sourceProperty)
[Signal] BindingRefreshed(string sourceProperty, Variant newValue)
```

---

## Binding Modes

### OneWay (Default)

Source → UI only. UI changes are ignored.

```csharp
binder.BindLabel(player, nameof(player.Health), healthLabel);
// UI updates when Health changes
// Changing healthLabel text doesn't affect player.Health
```

### TwoWay

Source ↔ UI. Changes flow both directions.

```csharp
binder.BindCheckBox(settings, nameof(settings.SoundEnabled), soundCheckbox,
    BindingMode.TwoWay);
// Checking the box updates settings.SoundEnabled
// Changing settings.SoundEnabled updates the checkbox
```

### OneWayToSource

UI → Source only. Source changes are ignored.

```csharp
binder.Bind(form, nameof(form.Username), usernameField, "Text",
    BindingMode.OneWayToSource);
// Changes to usernameField update form.Username
// Changing form.Username doesn't update the field
```

---

## Examples

### Example 1: Player HUD

```csharp
public partial class HUD : Control
{
    private DataBinderHostComponent? _binder;
    private Player? _player;

    public override void _Ready()
    {
        _binder = GetNode<DataBinderHostComponent>("DataBinder");
        _player = GetTree().Root.GetNode<Player>("Player");

        if (_binder != null && _player != null)
        {
            // Health and mana bars
            _binder.BindProgress(_player, nameof(_player.Health),
                GetNode<ProgressBar>("HealthBar"));
            _binder.BindProgress(_player, nameof(_player.Mana),
                GetNode<ProgressBar>("ManaBar"));

            // Level and experience
            _binder.BindLabel(_player, nameof(_player.Level),
                GetNode<Label>("LevelLabel"), "Lvl {0}");
            _binder.BindLabel(_player, nameof(_player.Experience),
                GetNode<Label>("ExpLabel"), "{0}/1000");

            // Equipment slots
            _binder.BindLabel(_player, nameof(_player.EquippedWeapon),
                GetNode<Label>("WeaponLabel"), "Weapon: {0}");

            // Effects (hide label when no status effect)
            _binder.BindVisible(_player, nameof(_player.HasStatusEffect),
                GetNode<Label>("StatusLabel"));
        }
    }
}
```

### Example 2: Form with TwoWay Binding

```csharp
public class SettingsForm
{
    public string Username { get; set; } = "Player";
    public bool SoundEnabled { get; set; } = true;
    public int MasterVolume { get; set; } = 80;
}

public partial class SettingsUI : Control
{
    private DataBinderHostComponent? _binder;
    private SettingsForm? _form;

    public override void _Ready()
    {
        _binder = GetNode<DataBinderHostComponent>("DataBinder");
        _form = new SettingsForm();

        if (_binder != null && _form != null)
        {
            // Two-way: changes sync both directions
            _binder.Bind(_form, nameof(_form.Username),
                GetNode<LineEdit>("UsernameInput"), "Text",
                BindingMode.TwoWay);

            _binder.BindCheckBox(_form, nameof(_form.SoundEnabled),
                GetNode<CheckBox>("SoundCheckbox"),
                BindingMode.TwoWay);

            _binder.BindProgress(_form, nameof(_form.MasterVolume),
                GetNode<HSlider>("VolumeSlider"),
                BindingMode.TwoWay);
        }
    }

    public void SaveSettings()
    {
        _binder?.RefreshTwoWay();  // Sync UI changes back to form
        // _form now has latest values from UI
        SaveFormToFile(_form);
    }
}
```

### Example 3: Inventory Display

```csharp
public partial class InventoryUI : Control
{
    private DataBinderHostComponent? _binder;
    private Inventory? _inventory;

    public override void _Ready()
    {
        _binder = GetNode<DataBinderHostComponent>("DataBinder");
        _inventory = GetTree().Root.GetNode<Player>("Player")?.Inventory;

        if (_binder != null && _inventory != null)
        {
            // Display item count
            _binder.BindLabel(_inventory, nameof(_inventory.ItemCount),
                GetNode<Label>("CountLabel"), "Items: {0}");

            // Display weight percentage
            _binder.Bind(_inventory, nameof(_inventory.CurrentWeight),
                GetNode<ProgressBar>("WeightBar"), "Value",
                BindingMode.OneWay, weight =>
                {
                    return Mathf.Min((float)weight / 100f, 1f);  // 0-1 for bar
                });

            // Color the weight bar red if overweight
            _binder.Bind(_inventory, nameof(_inventory.IsOverweight),
                GetNode<ProgressBar>("WeightBar"), "Color",
                BindingMode.OneWay, isOverweight =>
                {
                    return (bool)isOverweight ? Colors.Red : Colors.Green;
                });

            // Display weight label
            _binder.BindLabel(_inventory, nameof(_inventory.CurrentWeight),
                GetNode<Label>("WeightLabel"), "{0}/100 kg");
        }
    }
}
```

### Example 4: Status Display with Formatter

```csharp
public partial class GameStatusUI : Control
{
    public override void _Ready()
    {
        var binder = GetNode<DataBinderHostComponent>("DataBinder");
        var gameApp = GameApp.Instance;

        if (binder != null && gameApp != null)
        {
            // Format game state as readable text
            binder.Bind(gameApp, nameof(gameApp.CurrentLevel),
                GetNode<Label>("LevelLabel"), "Text",
                BindingMode.OneWay, level =>
                {
                    return level switch
                    {
                        1 => "🌍 Forest",
                        2 => "❄️ Tundra",
                        3 => "🏜️ Desert",
                        4 => "🌙 Cavern",
                        _ => "Unknown"
                    };
                });

            // Format difficulty as emoji
            binder.Bind(gameApp, nameof(gameApp.CurrentDifficulty),
                GetNode<Label>("DifficultyLabel"), "Text",
                BindingMode.OneWay, diff =>
                {
                    return diff switch
                    {
                        GameApp.Difficulty.Easy => "😴 Easy",
                        GameApp.Difficulty.Normal => "🎮 Normal",
                        GameApp.Difficulty.Hard => "💪 Hard",
                        GameApp.Difficulty.Nightmare => "👹 Nightmare",
                        _ => "Unknown"
                    };
                });

            // Format FPS
            binder.BindLabel(gameApp, nameof(gameApp.CurrentFPS),
                GetNode<Label>("FpsLabel"), "{0:F0} FPS");
        }
    }
}
```

---

## Advanced Patterns

### Pattern 1: Conditional Visibility

```csharp
// Show label only when player has poison effect
binder.Bind(player, nameof(player.HasPoisonEffect),
    poisonLabel, "Visible");
```

### Pattern 2: Complex Formatting

```csharp
// Format time remaining
binder.BindLabel(timer, nameof(timer.SecondsRemaining), timerLabel,
    seconds =>
    {
        int mins = (int)seconds / 60;
        int secs = (int)seconds % 60;
        return $"{mins}:{secs:D2}";
    });
```

### Pattern 3: Dynamic Color Based on Value

```csharp
// Health bar changes color based on health percentage
binder.Bind(player, nameof(player.HealthPercentage),
    healthBar, "Color",
    BindingMode.OneWay, percent =>
    {
        float pct = (float)percent / 100f;
        if (pct > 0.5f) return Colors.Green;
        if (pct > 0.2f) return Colors.Orange;
        return Colors.Red;
    });
```

### Pattern 4: Multiple Properties Same Target

```csharp
// Display combined damage value from multiple properties
binder.Bind(player, nameof(player.BaseDamage),
    damageLabel, "Text",
    BindingMode.OneWay, _ =>
    {
        float total = player.BaseDamage + player.BonusDamage;
        return $"DMG: {total}";
    });

// Refresh when bonus changes too
binder.RefreshProperty(nameof(player.BonusDamage));
```

---

## Tips & Tricks

### Refresh on Demand

```csharp
// Force UI to sync immediately (instead of waiting for poll)
binder.RefreshAll();
```

### Clean Up Bindings

```csharp
// When an object is removed from the game
binder.Unbind(deadEnemy);

// Or specific binding
binder.Unbind(player, nameof(player.Health));
```

### Monitor Binding Activity

```csharp
binder.BindingCreated += (prop) => GD.Print($"Bound: {prop}");
binder.BindingRemoved += (prop) => GD.Print($"Unbound: {prop}");
```

---

## Save/Load Behavior

DataBinderHostComponent implements `ISaveable`:

- **On Save:** Bindings are **not** persisted (they're UI setup)
- **On Load:** Calls `RefreshAll()` to resync UI with restored data

This is correct: the bound data persists (through other save mechanisms), but the bindings themselves are UI infrastructure.

---

## Migration from BeepDataBinder

If using static `BeepDataBinder`:

```csharp
// Old code (static)
BeepDataBinder.BindLabel(player, "Health", healthLabel, "HP: {0}");

// New code (component)
var binder = GetNode<DataBinderHostComponent>("DataBinder");
binder.BindLabel(player, nameof(player.Health), healthLabel, "HP: {0}");
```

---

## Best Practices

1. **Use nameof()** — Type-safe property names: `nameof(player.Health)` not `"Health"`
2. **Set AutoRefresh = true** — UI stays in sync automatically
3. **Use convenience methods** — `BindLabel()`, `BindProgress()` instead of generic `Bind()`
4. **Formatters for display logic** — Keep formatting out of model objects
5. **TwoWay sparingly** — Usually OneWay (UI reads model) is cleaner
6. **Listen to signals** — Know when bindings are created/removed

---

**Files:**
- `ecs/ui/DataBinderHostComponent.cs` — Component implementation
- `core/BeepDataBinder.cs` — Legacy static utility (deprecated)

