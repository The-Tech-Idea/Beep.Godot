using Godot;

namespace Beep.ECS;

/// <summary>Fits authored sprite art within the actor definition without changing its collision footprint.</summary>
[Tool, GlobalClass]
public partial class ActorPresentationComponent : Node2D
{
    [Export] public NodePath SpritePath { get; set; } = new("../Sprite2D");
    [Export] public NodePath PlayerPath { get; set; } = new("");
    [Export] public Color SelectionColor { get; set; } = new(0.2f, 0.95f, 0.55f);
    private ActorComponent? _actor;
    private PlayerContextComponent? _player;
    private Vector2 _footprint = new(20, 16);
    private bool _selected;
    private AnimatedSprite2D? _animated;
    private float _frameScale = 1;
    private Vector2 _anchor = new(0.5f, 1);

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        _actor = ActorComponent.ForBody(GetParent());
        _player = GetNodeOrNull<PlayerContextComponent>(PlayerPath);
        ApplyDefinition();
        if (_player is not null) _player.SelectionChanged += RefreshSelection;
        RefreshSelection();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_player)) _player!.SelectionChanged -= RefreshSelection;
        DisconnectSprite();
        _player = null;
        _actor = null;
        RequestReady();
    }

    public void ApplyDefinition()
    {
        DisconnectSprite();
        var definition = _actor?.Definition;
        if (definition is null) return;
        _footprint = definition.Footprint.IsFinite() ? definition.Footprint.Max(Vector2.One) : new(20, 16);
        QueueRedraw();
        if (!definition.VisualSize.IsFinite() || definition.VisualSize.X <= 0 || definition.VisualSize.Y <= 0) return;
        _anchor = definition.FeetAnchor.IsFinite() ? definition.FeetAnchor.Clamp(Vector2.Zero, Vector2.One) : new(0.5f, 1);
        var target = GetNodeOrNull<Node2D>(SpritePath);
        if (target is Sprite2D { Texture: not null } sprite)
        {
            var rect = sprite.GetRect();
            if (rect.Size.X <= 0 || rect.Size.Y <= 0) return;
            float scale = Mathf.Min(definition.VisualSize.X / rect.Size.X, definition.VisualSize.Y / rect.Size.Y);
            sprite.Scale = Vector2.One * scale;
            sprite.Position = -(rect.Position + rect.Size * _anchor) * scale;
        }
        else if (target is AnimatedSprite2D animated)
        {
            _animated = animated;
            _animated.FrameChanged += ApplyAnimatedFrame;
            _animated.AnimationChanged += ApplyAnimatedFrame;
            _animated.SpriteFramesChanged += ApplyDefinition;
            Vector2 maximum = Vector2.Zero;
            if (animated.SpriteFrames is { } frames)
                foreach (string name in frames.GetAnimationNames())
                    for (int i = 0; i < frames.GetFrameCount(name); i++)
                        if (frames.GetFrameTexture(name, i) is { } texture) maximum = maximum.Max(texture.GetSize());
            if (maximum.X <= 0 || maximum.Y <= 0) return;
            _frameScale = Mathf.Min(definition.VisualSize.X / maximum.X, definition.VisualSize.Y / maximum.Y);
            ApplyAnimatedFrame();
        }
    }

    private void ApplyAnimatedFrame()
    {
        if (!GodotObject.IsInstanceValid(_animated) || _animated!.SpriteFrames is not { } frames
            || !frames.HasAnimation(_animated.Animation) || frames.GetFrameCount(_animated.Animation) == 0) return;
        var texture = frames.GetFrameTexture(_animated.Animation, Mathf.Clamp(_animated.Frame, 0, frames.GetFrameCount(_animated.Animation) - 1));
        if (texture is null) return;
        Vector2 size = texture.GetSize();
        Vector2 origin = _animated.Offset - (_animated.Centered ? size * 0.5f : Vector2.Zero);
        _animated.Scale = Vector2.One * _frameScale;
        _animated.Position = -(origin + size * _anchor) * _frameScale;
    }

    private void DisconnectSprite()
    {
        if (GodotObject.IsInstanceValid(_animated))
        {
            _animated!.FrameChanged -= ApplyAnimatedFrame;
            _animated.AnimationChanged -= ApplyAnimatedFrame;
            _animated.SpriteFramesChanged -= ApplyDefinition;
        }
        _animated = null;
    }

    private void RefreshSelection()
    {
        _selected = _actor is not null && _player?.GetSelectedActors().Contains(_actor.ActorId) == true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_selected) return;
        DrawSetTransform(Vector2.Zero, 0, new(1, _footprint.Y / _footprint.X));
        DrawArc(Vector2.Zero, _footprint.X * 0.65f, 0, Mathf.Tau, 32, SelectionColor, 2, true);
        DrawSetTransform(Vector2.Zero);
    }
}
