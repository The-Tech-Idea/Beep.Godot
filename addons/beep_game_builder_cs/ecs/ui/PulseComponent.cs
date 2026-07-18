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
        private bool _wasPulsing;

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint()) return;

            bool pulsing = IsActive && AutoStart && Targets.Count > 0;
            if (!pulsing)
            {
                // Level out once when stopped so a paused pulse doesn't leave targets scaled.
                if (_wasPulsing)
                {
                    foreach (var c in Targets)
                        if (GodotObject.IsInstanceValid(c)) c.OffsetTransformScale = Vector2.One;
                    _wasPulsing = false;
                }
                return;
            }

            _time += (float)delta * Speed;
            float s = Mathf.Lerp(MinScale, MaxScale, (Mathf.Sin(_time) + 1f) / 2f);
            var scale = new Vector2(s, s);
            // Pulse the offset_transform layer, not raw Scale — a container-managed Control
            // (this is meant to sit on menu buttons/labels) would otherwise have its Scale
            // overwritten every layout pass. Matches UIEffectComponent's own Pulse.
            foreach (var c in Targets)
                if (GodotObject.IsInstanceValid(c))
                {
                    c.OffsetTransformEnabled = true;
                    c.OffsetTransformScale = scale;
                }
            _wasPulsing = true;
        }
    }
}
