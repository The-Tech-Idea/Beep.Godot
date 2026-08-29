using System.Collections.Generic;
using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// UI shake. Attach as a child of a Godot.Control. Shake() triggers a decaying jitter.
    /// Cascade: set ApplyToChildren = true to shake every descendant Control/Button.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ShakeComponent : EffectComponent
    {
        [Export] public float Intensity { get; set; } = 10f;
        [Export] public float Duration { get; set; } = 0.3f;
        [Export] public int Vibrato { get; set; } = 20;

        public float EffectiveIntensity => Mathf.Max(0f, float.IsFinite(Intensity) ? Intensity : 0f);
        public float EffectiveDuration => Mathf.Max(0.001f, float.IsFinite(Duration) ? Duration : 0.001f);
        public int EffectiveVibrato => Mathf.Clamp(Vibrato, 1, 200);

        [Signal] public delegate void ShakeStartedEventHandler();
        [Signal] public delegate void ShakeFinishedEventHandler();

        // Targets being shaken this run. Shake animates the offset_transform layer (Godot 4.7
        // render transform that containers don't overwrite — see CLAUDE.md), so neutral is
        // Vector2.Zero and there is nothing to snapshot/restore. Animating raw Position fought
        // any VBox/HBox/GridContainer re-sort every frame.
        private readonly List<Godot.Control> _shaking = new();
        private float _elapsed;
        // The ACTIVE shake's values — a one-shot Shake(50) must not overwrite the configured Intensity.
        private float _activeIntensity = 10f;
        private float _activeDuration = 0.3f;

        public override void _Ready()
        {
            base._Ready();
            SetProcess(false);
        }

        public void Shake(float intensity = -1, float duration = -1)
        {
            if (!IsActive || Targets.Count == 0) return;
            _shaking.Clear();
            foreach (var c in Targets)
                if (GodotObject.IsInstanceValid(c))
                {
                    c.OffsetTransformEnabled = true;
                    _shaking.Add(c);
                }
            _elapsed = 0;
            _activeIntensity = intensity > 0 && float.IsFinite(intensity) ? intensity : EffectiveIntensity;   // don't clobber the exports
            _activeDuration = duration > 0 && float.IsFinite(duration) ? duration : EffectiveDuration;
            SetProcess(true);
            EmitSignal(SignalName.ShakeStarted);
        }

        public override void _Process(double delta)
        {
            if (!IsActive || _elapsed >= _activeDuration || _shaking.Count == 0)
            {
                FinishShake(emitSignal: false);
                return;
            }
            _elapsed += Mathf.Max(0f, (float)delta);
            float decay = Mathf.Clamp(1f - (_elapsed / _activeDuration), 0f, 1f);

            foreach (var c in _shaking)
            {
                if (!GodotObject.IsInstanceValid(c)) continue;
                float x = (GD.Randf() * 2 - 1) * _activeIntensity * decay;
                float y = (GD.Randf() * 2 - 1) * _activeIntensity * decay;
                c.OffsetTransformPosition = new Vector2(x, y);
            }

            if (_elapsed >= _activeDuration)
            {
                FinishShake(emitSignal: true);
            }
        }

        private void FinishShake(bool emitSignal)
        {
            foreach (var c in _shaking)
                if (GodotObject.IsInstanceValid(c)) c.OffsetTransformPosition = Vector2.Zero;
            _shaking.Clear();
            SetProcess(false);
            if (emitSignal)
                EmitSignal(SignalName.ShakeFinished);
        }
    }
}
