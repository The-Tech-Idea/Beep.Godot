using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Pulse (breathing scale) animation. Attach as a child of a Godot.Control.
    /// Cascade: set ApplyToChildren = true to pulse every descendant Control/ Button
    /// instead of just the parent.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PulseComponent : EffectComponent
    {
        [Export] public float MinScale { get; set; } = 0.95f;
        [Export] public float MaxScale { get; set; } = 1.05f;
        [Export] public float Speed { get; set; } = 2f;
        [Export] public bool AutoStart { get; set; } = true;

        private float _time;

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint()) return;
            if (!IsActive || !AutoStart || Targets.Count == 0) return;
            _time += (float)delta * Speed;
            float s = Mathf.Lerp(MinScale, MaxScale, (Mathf.Sin(_time) + 1f) / 2f);
            var scale = new Vector2(s, s);
            foreach (var c in Targets)
                if (GodotObject.IsInstanceValid(c))
                    c.Scale = scale;
        }
    }
}
