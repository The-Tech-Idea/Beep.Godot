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
            // Runtime only: SetupTrail adds a Line2D to the parent.
            if (Engine.IsEditorHint()) return;
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
                EndCapMode = Line2D.LineCapMode.Round,
                WidthCurve = CreateWidthFadeCurve()
            };
            _line.Points = new Vector2[0];
            // TopLevel so the line lives in WORLD space, not glued to the moving parent — otherwise
            // every stored point translated with the parent and the "trail" was a fixed shape
            // dragged along with the sprite instead of a streak left behind.
            _line.TopLevel = true;
            GetParent().AddChild(_line);
            if (GetParent().IsInsideTree())
                _line.Owner = GetParent().Owner;
        }

        private Curve CreateWidthFadeCurve()
        {
            var curve = new Curve();
            curve.AddPoint(new Vector2(0, 1), 0, 0);
            curve.AddPoint(new Vector2(1, 0), 0, 0);
            return curve;
        }

        public override void _Process(double delta)
        {
            if (_line == null || GetParent() is not Node2D parent2D || !IsActive) return;

            // Record the parent's GLOBAL position (the line is TopLevel/world-space now); Position
            // was the parent's local coord in a different space and moved the whole trail with it.
            var points = new System.Collections.Generic.List<Vector2>(_line.Points) { parent2D.GlobalPosition };
            if (points.Count > MaxPoints) points.RemoveAt(0);
            _line.Points = points.ToArray();
        }

        public override void _ExitTree()
        {
            if (_line != null && GodotObject.IsInstanceValid(_line))
                _line.QueueFree();
        }
    }
}
