using Godot;
using Beep.ECS;
using Beep.ECS.UI;

namespace Beep.Examples;

/// <summary>
/// COIN RUSH ARENA — the game rules. The WORLD lives in `arena.tscn`.
///
/// The first version of this file built the whole arena in code, so opening the scene showed one
/// empty node and there was nothing to look at, move, or learn from. That is backwards for an addon
/// whose entire premise is composing scenes out of drop-in components. Everything is now authored:
/// 13 wall blocks, 14 coins, 5 enemies, the player, and three UI layers are all real nodes you can
/// select and drag in the 2D viewport.
///
/// What is left here is only what a scene cannot express: the score, the win/lose condition, and
/// contact damage.
///
/// Nodes are resolved BY NAME (`GetNode("Coins")`), never by path from the root — inserting one
/// wrapper container is exactly how the addon's own menus broke when a `Margin` was added.
/// </summary>
public partial class ArenaGame : Node2D
{
    [Export] public float ContactDamage { get; set; } = 12f;
    [Export] public float ContactCooldown { get; set; } = 0.7f;
    [Export] public int CoinScore { get; set; } = 100;

    /// <summary>Theme applied on load. Try "classic" — topdown's pixel register.</summary>
    [Export] public string Theme { get; set; } = "fantasy";

    private CharacterBody2D _player = null!;
    private HealthComponent _health = null!;
    private Node2D _coins = null!, _enemies = null!;
    private ArenaHud _hud = null!;
    private ResultScreen _result = null!;
    private PauseScreen _pause = null!;
    private Marker2D _spawn = null!;

    private Vector2[] _enemyHome = System.Array.Empty<Vector2>();
    private int _score, _collected, _total;
    private float _contactTimer;
    private bool _over;

    public override void _Ready()
    {
        // One line restyles every widget in the game. A generated project takes this from
        // GameInfo; the demo sets it directly so it runs standalone.
        SkinCatalog.SetActiveSkin("topdown", Theme, "", "");
        ArenaInput.Ensure();

        _player = GetNode<CharacterBody2D>("Player");
        _health = _player.GetNode<HealthComponent>("Health");
        _coins = GetNode<Node2D>("Coins");
        _enemies = GetNode<Node2D>("Enemies");
        _spawn = GetNode<Marker2D>("PlayerSpawn");
        _hud = GetNode<ArenaHud>("HudLayer/Hud");
        _result = GetNode<ResultScreen>("ResultLayer/Result");
        _pause = GetNode<PauseScreen>("PauseLayer/Pause");

        _total = _coins.GetChildCount();
        _enemyHome = new Vector2[_enemies.GetChildCount()];
        for (int i = 0; i < _enemyHome.Length; i++)
            _enemyHome[i] = _enemies.GetChild<Node2D>(i).Position;

        _health.Died += () => End(false);

        foreach (var node in _coins.GetChildren())
            if (node is Area2D coin)
                coin.BodyEntered += body => OnCoinTouched(coin, body);

        _result.Again += Restart;
        _result.Menu += ToMenu;
        _pause.Resume += () => SetPaused(false);
        _pause.Restart += Restart;
        _pause.Menu += ToMenu;

        Restart();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_over) return;
        if (_contactTimer > 0f) _contactTimer -= (float)delta;

        if (_contactTimer <= 0f)
        {
            foreach (var node in _enemies.GetChildren())
            {
                if (node is not Node2D e) continue;
                if (e.GlobalPosition.DistanceTo(_player.GlobalPosition) > 28f) continue;
                _health.TakeDamage(new GameDamage(ContactDamage, DamageType.Physical, e));
                _contactTimer = ContactCooldown;
                break;
            }
        }
        _hud.Set(_score, _collected, _total, _health.CurrentHealth, _health.MaxHealth);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_over) return;
        if (@event.IsActionPressed("pause")) SetPaused(!GetTree().Paused);
    }

    private void OnCoinTouched(Area2D coin, Node body)
    {
        if (_over || body != _player || !coin.Visible) return;
        coin.Visible = false;
        // Deferred: a body_entered callback runs mid-physics-flush, and toggling monitoring inline
        // makes Godot complain about changing state during a query.
        coin.SetDeferred(Area2D.PropertyName.Monitoring, false);
        _score += CoinScore;
        if (++_collected >= _total) End(true);
    }

    private void SetPaused(bool paused)
    {
        GetTree().Paused = paused;
        _pause.Visible = paused;
    }

    private void End(bool won)
    {
        if (_over) return;
        _over = true;
        GetTree().Paused = true;
        _result.Show(won, _score, _collected, _total);
    }

    private void Restart()
    {
        _over = false;
        _score = 0;
        _collected = 0;
        _contactTimer = 0f;
        _health.CurrentHealth = _health.MaxHealth;
        _player.Position = _spawn.Position;
        _player.Velocity = Vector2.Zero;

        foreach (var node in _coins.GetChildren())
            if (node is Area2D a)
            {
                a.Visible = true;
                a.SetDeferred(Area2D.PropertyName.Monitoring, true);
            }
        for (int i = 0; i < _enemyHome.Length; i++)
            _enemies.GetChild<Node2D>(i).Position = _enemyHome[i];

        _result.Visible = false;
        _pause.Visible = false;
        GetTree().Paused = false;
    }

    private void ToMenu() =>
        GetTree().ChangeSceneToFile("res://examples/topdown_arena/ui/main_menu.tscn");
}

/// <summary>
/// Registers the actions the demo needs, if the project has not.
///
/// `project.godot` in THIS repo defines no input actions at all — `BeepInputMapGenerator` writes
/// them into a GENERATED project, not into the addon repo. Without this the demo would load,
/// render perfectly, and simply not respond to the keyboard, which is the most confusing way for
/// an example to fail. A real generated project does not need it.
/// </summary>
public static class ArenaInput
{
    private static bool _done;

    public static void Ensure()
    {
        if (_done) return;
        _done = true;

        (string action, Key[] keys)[] wanted =
        {
            ("move_left",  new[] { Key.A, Key.Left }),
            ("move_right", new[] { Key.D, Key.Right }),
            ("move_up",    new[] { Key.W, Key.Up }),
            ("move_down",  new[] { Key.S, Key.Down }),
            ("pause",      new[] { Key.Escape, Key.P }),
        };
        var added = new System.Collections.Generic.List<string>();
        foreach (var (action, keys) in wanted)
        {
            if (InputMap.HasAction(action)) continue;
            InputMap.AddAction(action);
            foreach (var k in keys)
                InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = k });
            added.Add(action);
        }
        if (added.Count > 0)
            GD.Print($"[ArenaInput] registered at runtime: {string.Join(", ", added)}. "
                   + "A generated project gets these from BeepInputMapGenerator instead.");
    }
}
