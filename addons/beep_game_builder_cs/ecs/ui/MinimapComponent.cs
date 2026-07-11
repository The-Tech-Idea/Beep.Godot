using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Circular minimap. Extends Control (one documented exception, matching the
    /// ProgressRingComponent precedent) because it needs _Draw to render blips.
    /// Place it as a child of a HUD CanvasLayer. Blips are nodes in a tracked
    /// group (default "minimap_blips"); each blip's global position is mapped into
    /// the minimap's world radius. The player (optional, named "Player" or the
    /// configured path) is centered.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MinimapComponent : Godot.Control
    {
        [Export] public float WorldRadius { get; set; } = 800f;
        [Export] public Color BackgroundColor { get; set; } = new(0, 0, 0, 0.5f);
        [Export] public Color BorderColor { get; set; } = new(1, 1, 1, 0.4f);
        [Export] public Color PlayerColor { get; set; } = new(0.2f, 0.9f, 0.3f);
        [Export] public Color BlipColor { get; set; } = new(1, 0.3f, 0.3f);
        [Export] public float BlipSize { get; set; } = 3f;
        [Export] public string BlipGroup { get; set; } = "minimap_blips";
        [Export] public NodePath PlayerPath { get; set; } = new("../Player");

        private Node2D? _player;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(120, 120);
            if (!Engine.IsEditorHint())
                CallDeferred(nameof(ResolvePlayer));
        }

        private void ResolvePlayer()
        {
            _player = GetNodeOrNull<Node2D>(PlayerPath);
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        public override void _Draw()
        {
            Vector2 center = Size * 0.5f;
            float r = Mathf.Min(center.X, center.Y);

            DrawCircle(center, r, BackgroundColor);
            DrawArc(center, r, 0, Mathf.Tau, 48, BorderColor, 2f);

            if (Engine.IsEditorHint()) return;

            if (_player == null || !GodotObject.IsInstanceValid(_player)) ResolvePlayer();
            if (_player == null) return;
            Vector2 playerPos = _player.GlobalPosition;

            foreach (var n in GetTree().GetNodesInGroup(BlipGroup))
            {
                if (n is not Node2D blip || !GodotObject.IsInstanceValid(blip)) continue;
                Vector2 rel = (blip.GlobalPosition - playerPos).LimitLength(WorldRadius) / WorldRadius * r;
                DrawCircle(center + rel, BlipSize, BlipColor);
            }
            DrawCircle(center, BlipSize * 1.3f, PlayerColor);
        }
    }
}
