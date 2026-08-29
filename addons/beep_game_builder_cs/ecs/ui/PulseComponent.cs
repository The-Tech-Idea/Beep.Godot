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
        [Export] public bool AutoStart { get => _autoStart; set { if (_autoStart == value) return; _autoStart = value; UpdateProcessing(); } }

        public float EffectiveMinScale => Mathf.Clamp(Mathf.Min(FiniteOr(MinScale, 0.95f), FiniteOr(MaxScale, 1.05f)), 0.01f, 10f);
        public float EffectiveMaxScale => Mathf.Clamp(Mathf.Max(FiniteOr(MinScale, 0.95f), FiniteOr(MaxScale, 1.05f)), 0.01f, 10f);
        public float EffectiveSpeed => Mathf.Max(0f, FiniteOr(Speed, 0f));

        private float _time;
        private bool _autoStart = true;
        private bool _wasPulsing;

        public override void _Ready()
        {
            base._Ready();
            SetProcess(false);
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint())
            {
                UpdateProcessing();
                return;
            }

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
                UpdateProcessing();
                return;
            }

            _time += Mathf.Max(0f, (float)delta) * EffectiveSpeed;
            float s = Mathf.Lerp(EffectiveMinScale, EffectiveMaxScale, (Mathf.Sin(_time) + 1f) / 2f);
            var scale = new Vector2(s, s);
            // Pulse the offset_transform layer, not raw Scale — a container-managed Control
            // (this is meant to sit on menu buttons/labels) would otherwise have its Scale
            // overwritten every layout pass. Matches UIEffectComponent's own Pulse.
            foreach (var c in Targets)
                if (GodotObject.IsInstanceValid(c))
                {
                    c.OffsetTransformEnabled = true;
                    // offset_transform_scale pivots around pivot_offset, which defaults to the
                    // top-left corner (0,0) — a "breathing" button would grow toward its
                    // bottom-right. Centre the pivot so it pulses in place. Re-set each frame
                    // so it self-corrects on resize (cheap Vector2 write, no re-sort).
                    c.PivotOffset = c.Size / 2f;
                    c.OffsetTransformScale = scale;
                }
            _wasPulsing = true;
        }

        protected override void ResolveTargets()
        {
            base.ResolveTargets();
            UpdateProcessing();
        }

        private void UpdateProcessing()
            => SetProcess(!Engine.IsEditorHint() && IsActive && AutoStart && Targets.Count > 0);

        private static float FiniteOr(float value, float fallback) => float.IsFinite(value) ? value : fallback;
    }
}
