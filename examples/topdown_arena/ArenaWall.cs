using Godot;

namespace Beep.Examples;

/// <summary>
/// One wall or obstacle block, sized from the editor.
///
/// The arena's geometry used to be generated in code, which meant opening the scene showed a single
/// empty node and there was nothing to look at or move. A block is now a real node you can select,
/// drag and resize in the 2D viewport — which is the whole point of an addon built on scene
/// composition.
///
/// [Tool] so it draws and builds its collider at design time, not only at run time.
/// </summary>
[Tool]
[GlobalClass]
public partial class ArenaWall : StaticBody2D
{
    private Vector2 _size = new(120, 80);

    /// <summary>Size in px, centred on the node's own origin.</summary>
    [Export]
    public Vector2 Size
    {
        get => _size;
        set { _size = new Vector2(Mathf.Max(4f, value.X), Mathf.Max(4f, value.Y)); Rebuild(); }
    }

    private Color _tint = new(0.21f, 0.24f, 0.30f);
    [Export] public Color Tint { get => _tint; set { _tint = value; QueueRedraw(); } }

    public override void _Ready() => Rebuild();

    private void Rebuild()
    {
        // IDEMPOTENT. Every setter calls this and the editor calls setters freely, so it reuses
        // the existing shape node rather than adding another one each time — the same rule the
        // addon's public ApplyTheme() has to follow.
        var shape = GetNodeOrNull<CollisionShape2D>("Shape");
        if (shape == null)
        {
            shape = new CollisionShape2D { Name = "Shape" };
            AddChild(shape);
            // Owned by the scene root so the editor saves it with the scene instead of silently
            // dropping it on reload.
            if (Engine.IsEditorHint() && Owner != null) shape.Owner = Owner;
        }
        if (shape.Shape is not RectangleShape2D rect)
            shape.Shape = rect = new RectangleShape2D();
        rect.Size = _size;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var r = new Rect2(-_size / 2f, _size);
        DrawRect(r, _tint);
        DrawRect(r, new Color(_tint.R * 1.5f, _tint.G * 1.5f, _tint.B * 1.6f), false, 2f);
    }
}
