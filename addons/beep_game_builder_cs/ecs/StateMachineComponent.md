# StateMachineComponent Implementation Guide

## Overview

`StateMachineComponent` wraps the utility `BeepStateMachine` as a proper ECS component with lifecycle management, signal-based events, and state persistence.

**When to use:**
- AI/NPC behavior (Idle → Chase → Attack → Flee)
- Animation controller (Walk → Run → Jump → Fall)
- Game flow (Menu → Playing → Paused → GameOver)
- Dialogue states (Talking → Listening → Reacting)
- Any entity with discrete states and transitions

---

## Quick Start

### 1. Add to Scene

```gdscript
[node name="AI" type="Node" parent="."]
script = ExtResource("path/to/StateMachineComponent.cs")
initial_state = "idle"
```

Or attach via code:
```csharp
var fsm = new StateMachineComponent { InitialState = "idle" };
AddChild(fsm);
```

### 2. Define States

```csharp
var fsm = GetNode<StateMachineComponent>("AI");

fsm.AddState("idle", onEnter: () =>
{
    sprite.Play("idle");
    velocity = Vector2.Zero;
});

fsm.AddState("run", onUpdate: () =>
{
    sprite.Play("run");
    // Called every frame
    MoveTowardTarget();
});

fsm.AddState("attack", onEnter: () =>
{
    sprite.Play("attack");
    FireProjectile();
}, onExit: () =>
{
    isAttacking = false;
});
```

### 3. Define Transitions

```csharp
// Direct transitions (trigger-based)
fsm.AddTransition("idle", "run", trigger: "move");
fsm.AddTransition("run", "attack", trigger: "see_enemy");
fsm.AddTransition("attack", "idle", trigger: "no_target");
fsm.AddTransition("run", "idle", trigger: "stop");

// Or override with multiple triggers
fsm.AddTransition("idle", "run", trigger: "move");
fsm.AddTransition("idle", "run", trigger: "see_enemy");
```

### 4. Start & Control

```csharp
fsm.Start("idle");  // Enter initial state

// Fire transitions via triggers
if (Input.IsActionPressed("move"))
    fsm.Trigger("move");

if (EnemyNearby())
    fsm.Trigger("see_enemy");

// Or transition directly
fsm.ChangeState("attack");
```

### 5. Listen to Events

```csharp
fsm.StateEntered += (state) =>
{
    GD.Print($"Entered: {state}");
};

fsm.StateChanged += (from, to) =>
{
    GD.Print($"{from} → {to}");
};

fsm.StateExited += (state) =>
{
    GD.Print($"Left: {state}");
};
```

---

## API Reference

### Properties

```csharp
string CurrentState { get; }           // Current active state name
string PreviousState { get; }          // Previous state (or null)
string InitialState { get; set; }      // Export: initial state on _Ready()
float CurrentStateTime { get; }        // Seconds in current state
```

### Methods

```csharp
// Define behavior
AddState(name, onEnter, onUpdate, onExit)
AddTransition(from, to, trigger)

// Control
Start(stateName)
ChangeState(newState)
Trigger(triggerKey)

// Query
TimeInState(stateName)
```

### Signals

```csharp
[Signal] StateChanged(string from, string to)
[Signal] StateEntered(string state)
[Signal] StateExited(string state)
```

---

## Example: AI Enemy

```csharp
public partial class EnemyAI : GameplayComponent
{
    private StateMachineComponent? _fsm;
    private AnimatedSprite2D? _sprite;
    private Vector2 _targetPos;
    private const float MoveSpeed = 150f;
    private const float DetectRange = 300f;

    public override void _Ready()
    {
        base._Ready();
        _sprite = GetNode<AnimatedSprite2D>("Sprite");
        _fsm = GetNode<StateMachineComponent>("FSM");

        // Define states
        _fsm.AddState("idle", onEnter: () =>
        {
            _sprite.Play("idle");
        });

        _fsm.AddState("patrol", onUpdate: () =>
        {
            _sprite.Play("walk");
            Position = Position.MoveToward(_targetPos, MoveSpeed * (float)GetPhysicsProcess());
            if (Position.DistanceTo(_targetPos) < 10f)
                _targetPos = new Vector2(GD.Randf() * 300, Position.Y);
        });

        _fsm.AddState("chase", onUpdate: () =>
        {
            _sprite.Play("run");
            var player = GetParent().GetNode("Player") as CharacterBody2D;
            if (player != null)
                Position = Position.MoveToward(player.GlobalPosition, MoveSpeed * 1.5f * (float)GetPhysicsProcess());
        });

        _fsm.AddState("attack", onEnter: () =>
        {
            _sprite.Play("attack");
        }, onUpdate: () =>
        {
            // Fire projectile logic
        });

        // Define transitions
        _fsm.AddTransition("idle", "patrol", trigger: "patrol");
        _fsm.AddTransition("patrol", "chase", trigger: "see_player");
        _fsm.AddTransition("chase", "attack", trigger: "in_range");
        _fsm.AddTransition("attack", "chase", trigger: "out_of_range");
        _fsm.AddTransition("chase", "patrol", trigger: "lost_player");

        _fsm.Start("idle");
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_fsm == null) return;

        var player = GetParent().GetNode("Player") as CharacterBody2D;
        if (player == null) return;

        float dist = GlobalPosition.DistanceTo(player.GlobalPosition);

        if (dist < 50f && _fsm.CurrentState == "chase")
            _fsm.Trigger("in_range");
        else if (dist > 100f && _fsm.CurrentState == "attack")
            _fsm.Trigger("out_of_range");
        else if (dist < DetectRange && _fsm.CurrentState != "chase")
            _fsm.Trigger("see_player");
        else if (dist > DetectRange && _fsm.CurrentState == "chase")
            _fsm.Trigger("lost_player");
    }
}
```

---

## Common Patterns

### Pattern 1: Conditional Transitions

```csharp
fsm.AddState("attack", onUpdate: () =>
{
    attackTimer -= (float)GetPhysicsProcess();
    if (attackTimer <= 0)
        fsm.Trigger("cooldown_done");
});
```

### Pattern 2: Timed States

```csharp
fsm.AddState("stun", onUpdate: () =>
{
    if (fsm.CurrentStateTime > 2f)
        fsm.ChangeState("idle");
});
```

### Pattern 3: State Entry Cleanup

```csharp
fsm.AddState("prev_state", onExit: () =>
{
    // Clean up any animations, particles, etc.
    GetNode<Timer>("StateTimer").Stop();
});
```

### Pattern 4: Animation Integration

```csharp
var sprite = GetNode<AnimatedSprite2D>("Sprite");
fsm.StateEntered += (state) =>
{
    sprite.Play(state);  // Match state name to animation
};
```

---

## Save/Load

StateMachineComponent implements `ISaveable` — current state and time in state are automatically persisted:

```csharp
// On save:
// state.GameData["state_machine_current"] = "run"
// state.GameData["state_machine_time"] = 3.5f

// On load:
// FSM restores to "run" and resets timer
```

Handlers (OnEnter/OnExit callbacks) **do not** run during restore — only the state is set. This is correct: if you need side effects on load, call them in a separate `_Ready()` hook.

---

## Best Practices

1. **Name states clearly** — "idle", "walk", "run", "attack" are better than "s0", "s1", etc.
2. **Keep transitions simple** — use short trigger keys ("move", "hit", "done")
3. **Avoid circular transitions** — design fallback paths (attack → back to chase, not attack → attack)
4. **Use OnUpdate sparingly** — expensive logic goes in _Process, not in the state callback
5. **Signal for UI** — connect StateChanged to UI updates (health bar, status text)

---

## Debugging

Check current state at runtime:
```csharp
GD.Print($"State: {fsm.CurrentState}, Time: {fsm.CurrentStateTime:F1}s");
```

Listen to all state changes:
```csharp
fsm.StateChanged += (from, to) => GD.Print($"{from} → {to}");
```

---

**Files:**
- `ecs/StateMachineComponent.cs` — Component implementation
- `core/BeepStateMachine.cs` — Underlying state machine (don't use directly)

