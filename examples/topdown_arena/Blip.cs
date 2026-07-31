using Godot;

namespace Beep.Examples;

/// <summary>
/// A drawn game token — the player, an enemy, a coin.
///
/// The addon ships no art, so the demo draws its own: a filled disc with a darker rim, optionally
/// bobbing (coins) or ringed (the player). [Tool] so an entity scene actually shows something in
/// the editor instead of an empty node with a script on it.
/// </summary>
[Tool]
[GlobalClass]
public partial class Blip : Node2D
{
    private float _radius = 12f;
    [Export] public float Radius { get => _radius; set { _radius = value; QueueRedraw(); } }

    private Color _tint = Colors.White;
    [Export] public Color Tint { get => _tint; set { _tint = value; QueueRedraw(); } }

    /// <summary>Bob up and down. Coins only.</summary>
    [Export] public bool Bob { get; set; }

    /// <summary>Draw an outer halo ring. Marks the player.</summary>
    private bool _ring;
    [Export] public bool Ring { get => _ring; set { _ring = value; QueueRedraw(); } }

    private float _t;

    public override void _Process(double delta)
    {
        if (!Bob || Engine.IsEditorHint()) return;
        _t += (float)delta * 3.2f;
        Position = new Vector2(Position.X, Mathf.Sin(_t) * 2.5f);
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, _radius, _tint);
        DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 28,
                new Color(_tint.R * 0.45f, _tint.G * 0.45f, _tint.B * 0.45f), 2.5f);
        if (_ring)
            DrawArc(Vector2.Zero, _radius + 5f, 0f, Mathf.Tau, 32, _tint with { A = 0.35f }, 2f);
    }
}
