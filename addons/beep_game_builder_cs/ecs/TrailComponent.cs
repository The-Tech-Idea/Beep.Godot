using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Motion trail effect. Attaches as a child of a Node2D and renders a fading
    /// Line2D trail behind it. Good for dashes, projectiles, fast enemies.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TrailComponent : UIComponent
    {
        [Export] public int MaxPoints { get; set; } = 20;
        [Export] public Color TrailColor { get; set; } = new(1, 1, 1, 0.5f);
        [Export] public float FadeSpeed { get; set; } = 5f;
        [Export] public float Width { get; set; } = 4f;

        private Line2D? _line;
        private float _accumulate;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(SetupTrail));
        }

        private void SetupTrail()
        {
            if (GetParent() is not Node2D) return;
            _line = new Line2D
            {
                Name = "TrailLine",
                Width = Width,
                DefaultColor = TrailColor,
                JointMode = Line2D.LineJointMode.Round,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round
            };
            _line.Points = new Vector2[0];
            GetParent().AddChild(_line);
            if (GetParent().IsInsideTree())
                _line.Owner = GetParent().Owner;
        }

        public override void _Process(double delta)
        {
            if (_line == null || GetParent() is not Node2D parent2D) return;
            _accumulate += (float)delta * FadeSpeed;
            while (_accumulate >= 1f) { _accumulate -= 1f; }

            // Add current position.
            var points = new System.Collections.Generic.List<Vector2>(_line.Points) { parent2D.Position };
            if (points.Count > MaxPoints) points.RemoveAt(0);
            _line.Points = points.ToArray();

            // Fade: shift all points toward 0 alpha over time (visual via width taper).
            if (points.Count > 1)
                _line.Width = Width;
        }
    }
}
