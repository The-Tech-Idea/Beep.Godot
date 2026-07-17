# CoroutineHostComponent Implementation Guide

## Overview

`CoroutineHostComponent` is an instance-based task scheduler that replaces the static `BeepCoroutine` utility. Schedule delayed or repeating callbacks without global state or manual timer management.

**When to use:**
- Delayed actions (spawn enemy after 2 seconds, play sound after animation)
- Repeating timers (update HUD every 0.5 seconds, regenerate health every 1 second)
- Timed effects (fade out, knockback recovery, cooldown timers)
- Animation sequences (move, pause, move, pause)

---

## Quick Start

### 1. Add to Scene

```gdscript
[node name="Coroutines" type="Node" parent="."]
script = ExtResource("path/to/CoroutineHostComponent.cs")
```

### 2. Schedule Tasks

```csharp
var coro = GetNode<CoroutineHostComponent>("Coroutines");

// One-shot after delay
coro.Delay(2f, () =>
{
    SpawnEnemy();
});

// Next frame
coro.NextFrame(() =>
{
    UpdateUI();
});

// Repeating
var jobId = coro.Repeat(0.5f, () =>
{
    regenerateHealth();
});

// Stop repeating
coro.Cancel(jobId);
```

### 3. Listen to Events

```csharp
coro.JobStarted += (jobId) => GD.Print($"Job started: {jobId}");
coro.JobCompleted += (jobId) => GD.Print($"Job done: {jobId}");
```

---

## API Reference

### Methods

```csharp
// Schedule one-shot callback
string Delay(double seconds, Action callback, string jobId = null)

// Run callback next frame
string NextFrame(Action callback, string jobId = null)

// Schedule repeating callback (returns job ID)
string Repeat(double interval, Action callback, string jobId = null)

// Cancel a job by ID
void Cancel(string jobId)

// Cancel all jobs
void CancelAll()

// Query jobs
int ActiveJobCount { get; }
bool IsJobActive(string jobId)

// Wait for signal
void WaitSignal(GodotObject source, StringName signal, Action then = null)
```

### Signals

```csharp
[Signal] JobStarted(string jobId)
[Signal] JobCompleted(string jobId)
```

---

## Examples

### Example 1: Timed Enemy Spawn

```csharp
public override void _Ready()
{
    base._Ready();
    var coro = GetNode<CoroutineHostComponent>("Coroutines");

    // Spawn waves of enemies
    coro.Delay(2f, () => SpawnWave(1));
    coro.Delay(5f, () => SpawnWave(2));
    coro.Delay(10f, () => SpawnWave(3));
}

private void SpawnWave(int waveNum)
{
    GD.Print($"Wave {waveNum}!");
    for (int i = 0; i < waveNum; i++)
    {
        var enemy = (Node2D)EnemyScene.Instantiate();
        enemy.Position = new Vector2(GD.Randf() * 1000, -100);
        AddChild(enemy);
    }
}
```

### Example 2: Repeating Health Regen

```csharp
public partial class PlayerController : GameplayComponent
{
    private CoroutineHostComponent? _coro;
    private float _health = 100f;
    private const float MaxHealth = 100f;
    private const float RegenPerTick = 2f;

    public override void _Ready()
    {
        base._Ready();
        _coro = GetNode<CoroutineHostComponent>("Coroutines");

        // Start health regeneration (every 1 second when not in combat)
        _coro.Repeat(1f, () =>
        {
            if (!InCombat)
                _health = Mathf.Min(_health + RegenPerTick, MaxHealth);
        });
    }
}
```

### Example 3: Animation Sequence

```csharp
public partial class CutsceneController : Node
{
    private CoroutineHostComponent? _coro;
    private AnimatedSprite2D? _sprite;

    public override void _Ready()
    {
        _coro = GetNode<CoroutineHostComponent>("Coroutines");
        _sprite = GetNode<AnimatedSprite2D>("Sprite");

        // Animate character: walk → talk → leave
        _coro.Delay(0f, () => _sprite.Play("walk"));
        _coro.Delay(2f, () => _sprite.Play("talk"));
        _coro.Delay(4f, () => _sprite.Play("leave"));
        _coro.Delay(6f, () =>
        {
            QueueFree();  // Remove character when done
        });
    }
}
```

### Example 4: Countdown Timer

```csharp
public partial class GameTimer : Control
{
    private CoroutineHostComponent? _coro;
    private Label? _label;
    private int _secondsLeft = 60;

    public override void _Ready()
    {
        _coro = GetNode<CoroutineHostComponent>("Coroutines");
        _label = GetNode<Label>("TimeLabel");

        // Count down
        var jobId = _coro.Repeat(1f, () =>
        {
            _secondsLeft--;
            _label.Text = _secondsLeft.ToString();

            if (_secondsLeft <= 0)
            {
                _label.Text = "Time's up!";
                _coro.Cancel(jobId);
            }
        });
    }
}
```

### Example 5: Wait for Signal

```csharp
public override void _Ready()
{
    base._Ready();
    var coro = GetNode<CoroutineHostComponent>("Coroutines");
    var timer = GetNode<Timer>("AnimationTimer");

    // Wait for animation to complete, then spawn particles
    coro.WaitSignal(timer, "timeout", () =>
    {
        SpawnParticles();
    });
}
```

---

## Common Patterns

### Pattern 1: One-Time Setup After Delay

```csharp
coro.Delay(0.5f, () =>
{
    // Safe to reference child nodes here
    var ui = GetNode<HUD>("HUD");
    ui.ShowHealthBar(player);
});
```

### Pattern 2: Repeating with Condition

```csharp
var jobId = coro.Repeat(0.1f, () =>
{
    if (playerDead)
    {
        coro.Cancel(jobId);
        return;
    }
    UpdateHUD();
});
```

### Pattern 3: Cooldown Timer

```csharp
private float _attackCooldown = 0f;
private const float AttackCooldownTime = 1f;

public override void _Ready()
{
    var coro = GetNode<CoroutineHostComponent>("Coroutines");

    coro.Repeat(0.1f, () =>
    {
        if (_attackCooldown > 0)
            _attackCooldown -= 0.1f;
    });
}

public void Attack()
{
    if (_attackCooldown <= 0)
    {
        FireProjectile();
        _attackCooldown = AttackCooldownTime;
    }
}
```

### Pattern 4: Staggered Initialization

```csharp
public override void _Ready()
{
    var coro = GetNode<CoroutineHostComponent>("Coroutines");

    // Load expensive assets over multiple frames
    coro.NextFrame(() => LoadLevel());
    coro.Delay(0.1f, () => LoadEnemies());
    coro.Delay(0.2f, () => LoadParticles());
    coro.Delay(0.3f, () => LoadUI());
}
```

---

## Tips & Tricks

### Generating Unique Job IDs

By default, job IDs are random (Guid.NewGuid()). Provide your own for easier management:

```csharp
const string Regen = "player_regen";
const string Cooldown = "attack_cooldown";

coro.Repeat(1f, OnHealthRegen, Regen);
coro.Delay(5f, OnCooldownEnd, Cooldown);

// Easy to cancel later
coro.Cancel(Regen);
```

### Chaining Delayed Actions

```csharp
coro.Delay(1f, () =>
{
    Action();
    coro.Delay(1f, () =>
    {
        AnotherAction();
        coro.Delay(1f, () => FinalAction());
    });
});
```

Or use a helper method:

```csharp
private void Chain(params (float delay, Action action)[] sequence)
{
    float totalDelay = 0;
    foreach (var (delay, action) in sequence)
    {
        totalDelay += delay;
        _coro.Delay(totalDelay, action);
    }
}

Chain((1f, SpawnWave), (2f, PlaySound), (0.5f, ShowText));
```

### Check Active Jobs

```csharp
if (_coro.IsJobActive("my_job"))
    _coro.Cancel("my_job");

GD.Print($"Active jobs: {_coro.ActiveJobCount}");
```

---

## Save/Load Behavior

CoroutineHostComponent implements `ISaveable` with this behavior:

- **On Save:** Jobs are **not** persisted (they're transient runtime tasks)
- **On Load:** All active jobs are **cancelled** to resume from a clean state

This is intentional: animations, effects, and timers don't make sense after a save/load.

---

## Best Practices

1. **Use semantic job IDs** — "player_regen", "enemy_spawn_wave_1", not "job_42"
2. **Keep callbacks simple** — Heavy logic goes in _Process, not the coroutine callback
3. **Listen to signals** — Connect JobStarted/JobCompleted to UI for feedback
4. **Clean up in _ExitTree** — The component auto-clears, but be safe with external refs
5. **Prefer Repeat for timers** — More efficient than scheduling many Delay calls

---

## Migration from BeepCoroutine

If using static `BeepCoroutine`:

```csharp
// Old code (static)
BeepCoroutine.Init(this);
BeepCoroutine.Run(2f, OnTimeout);

// New code (component)
var coro = GetNode<CoroutineHostComponent>("Coroutines");
coro.Delay(2f, OnTimeout);
```

---

**Files:**
- `ecs/ui/CoroutineHostComponent.cs` — Component implementation
- `core/BeepCoroutine.cs` — Legacy static utility (deprecated)

