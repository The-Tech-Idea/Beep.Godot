# KeybindManagerComponent Implementation Guide

## Overview

`KeybindManagerComponent` manages game-instance keyboard bindings with runtime rebinding, event signals, and save/load persistence. It replaces the static `BeepKeybindManager` with a proper ECS component that integrates into the game state lifecycle.

**When to use:**
- Player input handling (jump, move, attack, interact)
- Pause/menu navigation
- Settings menu keybind configuration
- Accessibility (custom key mappings)

---

## Quick Start

### 1. Add to Scene

```gdscript
[node name="KeybindManager" type="Node" parent="."]
script = ExtResource("path/to/KeybindManagerComponent.cs")
capture_input = true
```

Alternatively, attach as an autoload in Project Settings → Autoload.

### 2. Register Keybinds

```csharp
var kb = GetNode<KeybindManagerComponent>("KeybindManager");

kb.Register("jump", "Jump", Key.Space, () =>
{
    player.Jump();
});

kb.Register("move_left", "Move Left", Key.A, () =>
{
    player.MoveInput(-1);
});

kb.Register("move_right", "Move Right", Key.D, () =>
{
    player.MoveInput(1);
});

kb.Register("pause", "Pause", Key.Escape, () =>
{
    GetTree().Paused = true;
});

kb.Register("screenshot", "Screenshot", Key.F12, () =>
{
    GetTree().Root.GetChild(0)?.GetViewport().GetTexture()?.GetImage()?.SavePng("screenshot.png");
}, ctrl: false, shift: false, alt: false);
```

### 3. Listen to Events

```csharp
kb.KeybindTriggered += (id) =>
{
    GD.Print($"Triggered: {id}");
    // Update UI, play sound, etc.
};

kb.KeybindRebound += (id, displayStr) =>
{
    GD.Print($"{id} rebound to {displayStr}");
};
```

### 4. Allow Runtime Rebinding

```csharp
// Player presses "Rebind Jump" button in settings
kb.Rebind("jump", Key.W);  // Jump is now W instead of Space

// Show the new binding in UI
string displayStr = kb.GetDisplayString("jump");  // Returns "W"
jumpButtonLabel.Text = displayStr;
```

---

## API Reference

### Properties

```csharp
bool CaptureInput { get; set; }       // Export: whether to handle input
```

### Methods

```csharp
// Register a keybind
Register(id, label, key, action, ctrl, shift, alt)

// Rebind at runtime
Rebind(id, newKey)
SetAction(id, newAction)
Unregister(id)

// Query bindings
Key? GetKey(id)
string? GetDisplayString(id)          // Returns "Ctrl+Space", "W", etc.
IEnumerable<string> GetAllKeybindIds()

// Control
SetEnabled(bool enabled)              // Disable/enable all keybinds
Clear()                               // Remove all keybinds
```

### Signals

```csharp
[Signal] KeybindTriggered(string keybindId)
[Signal] KeybindRebound(string keybindId, string newKeyDisplay)
```

---

## Example: Player Controller

```csharp
public partial class PlayerController : GameplayComponent
{
    private KeybindManagerComponent? _keybindMgr;
    private CharacterBody2D? _body;
    private AnimatedSprite2D? _sprite;
    private float _moveInput;
    private const float MoveSpeed = 300f;
    private const float JumpForce = 500f;
    private float _velocity_y;

    public override void _Ready()
    {
        base._Ready();
        _body = GetParent() as CharacterBody2D;
        _sprite = GetNode<AnimatedSprite2D>("Sprite");
        _keybindMgr = GetTree().Root.GetNode("KeybindManager") as KeybindManagerComponent;

        if (_keybindMgr != null)
        {
            _keybindMgr.Register("move_left", "Move Left", Key.A, () => _moveInput = -1f);
            _keybindMgr.Register("move_right", "Move Right", Key.D, () => _moveInput = 1f);
            _keybindMgr.Register("jump", "Jump", Key.Space, Jump);
            _keybindMgr.Register("attack", "Attack", Key.F, Attack);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (_body == null) return;

        // Apply gravity
        _velocity_y += 980f * (float)delta;
        _body.Velocity = new Vector2(_moveInput * MoveSpeed, _velocity_y);
        _body.MoveAndSlide();

        // Update animation
        if (_moveInput != 0)
            _sprite?.Play("walk");
        else
            _sprite?.Play("idle");

        _moveInput = 0;  // Reset each frame
    }

    private void Jump()
    {
        if (_body?.IsOnFloor() == true)
            _velocity_y = -JumpForce;
    }

    private void Attack()
    {
        _sprite?.Play("attack");
    }
}
```

---

## Example: Settings Menu Rebinding UI

```csharp
public partial class SettingsMenu : Control
{
    private KeybindManagerComponent? _keybindMgr;
    private string? _rebindingKeybindId;

    public override void _Ready()
    {
        _keybindMgr = GetTree().Root.GetNode("KeybindManager") as KeybindManagerComponent;

        var jumpButton = GetNode<Button>("JumpRebindButton");
        jumpButton.Pressed += () => StartRebinding("jump", jumpButton);

        var moveLeftButton = GetNode<Button>("MoveLeftRebindButton");
        moveLeftButton.Pressed += () => StartRebinding("move_left", moveLeftButton);
    }

    private void StartRebinding(string keybindId, Button button)
    {
        _rebindingKeybindId = keybindId;
        button.Text = "Press a key...";
        GetTree().Root.GuiDisableFocus();
    }

    public override void _Input(InputEvent @event)
    {
        if (_rebindingKeybindId != null && @event is InputEventKey key && key.Pressed)
        {
            // Rebind the keybind
            if (_keybindMgr != null)
            {
                _keybindMgr.Rebind(_rebindingKeybindId, key.Keycode);

                // Update the button label
                var button = GetNode<Button>(_rebindingKeybindId + "RebindButton");
                button.Text = _keybindMgr.GetDisplayString(_rebindingKeybindId);
            }

            _rebindingKeybindId = null;
            GetTree().Root.GuiEnableFocus();
            GetTree().Root.SetInputAsHandled();
        }
    }
}
```

---

## Common Patterns

### Pattern 1: Input Held vs. Pressed

```csharp
// For "held" actions, check in _Process instead of Register
kb.Register("attack", "Attack", Key.F, null);  // No callback

public override void _Process(double delta)
{
    if (Input.IsActionPressed("attack"))
        player.Attack();  // Fires every frame while held
}
```

### Pattern 2: Modifier Keys

```csharp
kb.Register("save", "Save Game", Key.S, Save, ctrl: true);  // Ctrl+S
kb.Register("load", "Load Game", Key.L, Load, ctrl: true);  // Ctrl+L
kb.Register("quit", "Quit", Key.Q, Quit, alt: true);        // Alt+Q
```

### Pattern 3: Multiple Keys for Same Action

```csharp
kb.Register("jump", "Jump", Key.Space, OnJump);
kb.Register("jump_alt", "Jump (Alt)", Key.W, OnJump);  // Both Space and W jump
```

### Pattern 4: Dynamic Keybind Groups

```csharp
var combatKeybinds = new[] { "attack", "block", "dodge" };
var navigationKeybinds = new[] { "menu", "pause", "screenshot" };

void SetCombatEnabled(bool enabled)
{
    kb.SetEnabled(enabled);  // Simple: just disable all
    // For per-group: track state separately
}
```

---

## Save/Load

KeybindManagerComponent implements `ISaveable` — custom keybinds are persisted:

```csharp
// On save:
// state.GameData["keybinds"] = {
//   "jump": "Space",
//   "move_left": "A",
//   "move_right": "D"
// }

// On load:
// All custom keybinds restored from the map
```

The component **restores keybinds to their registered defaults if not in the save file**, so adding new keybinds is backward-compatible.

---

## Best Practices

1. **Centralize registration** — Register all keybinds in one place (main scene or autoload `_Ready()`)
2. **Use semantic IDs** — "jump", "move_left", "attack" not "key0", "key1", etc.
3. **Provide defaults** — Show default keybinds in settings before player rebinds
4. **Avoid conflicts** — Warn if two actions use the same key
5. **Test accessibility** — Ensure rebinding works for alternative input methods
6. **Persist player preferences** — Use ISaveable integration to restore custom binds

---

## Debugging

List all registered keybinds:
```csharp
foreach (var id in _keybindMgr.GetAllKeybindIds())
{
    GD.Print($"{id}: {_keybindMgr.GetDisplayString(id)}");
}
```

Check if a keybind exists:
```csharp
if (_keybindMgr.GetKey("jump") != null)
    GD.Print("Jump keybind is registered");
```

---

## Migration from BeepKeybindManager

If using the static `BeepKeybindManager`:

```csharp
// Old code (static)
Beep.GameBuilder.BeepKeybindManager.Register("jump", "Jump", Key.Space, OnJump);

// New code (component)
_keybindMgr.Register("jump", "Jump", Key.Space, OnJump);
```

The component is **not** a 1:1 replacement — it's instance-based rather than global. This is intentional: multiple game instances (menus, different scenes) can have different keybinds.

---

**Files:**
- `ecs/ui/KeybindManagerComponent.cs` — Component implementation
- `core/BeepKeybindManager.cs` — Legacy static utility (deprecated)

