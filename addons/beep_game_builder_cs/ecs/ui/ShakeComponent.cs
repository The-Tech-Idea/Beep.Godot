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

        [Signal] public delegate void ShakeStartedEventHandler();
        [Signal] public delegate void ShakeFinishedEventHandler();

        // Each target shakes around its own original position.
        private readonly Dictionary<Godot.Control, Vector2> _origPos = new();
        private float _elapsed;
        // The ACTIVE shake's values — a one-shot Shake(50) must not overwrite the configured Intensity.
        private float _activeIntensity = 10f;
        private float _activeDuration = 0.3f;

        public void Shake(float intensity = -1, float duration = -1)
        {
            if (!IsActive || Targets.Count == 0) return;
            _origPos.Clear();
            foreach (var c in Targets)
                if (GodotObject.IsInstanceValid(c))
                    _origPos[c] = c.Position;
            _elapsed = 0;
            _activeIntensity = intensity > 0 ? intensity : Intensity;   // don't clobber the exports
            _activeDuration = duration > 0 ? duration : Duration;
            EmitSignal(SignalName.ShakeStarted);
        }

        public override void _Process(double delta)
        {
            if (_elapsed >= _activeDuration || _origPos.Count == 0) return;
            _elapsed += (float)delta;
            float decay = 1f - (_elapsed / _activeDuration);

            foreach (var (c, orig) in _origPos)
            {
                if (!GodotObject.IsInstanceValid(c)) continue;
                float x = (float)(GD.Randf() * 2 - 1) * _activeIntensity * decay;
                float y = (float)(GD.Randf() * 2 - 1) * _activeIntensity * decay;
                c.Position = orig + new Vector2(x, y);
            }

            if (_elapsed >= _activeDuration)
            {
                foreach (var (c, orig) in _origPos)
                    if (GodotObject.IsInstanceValid(c)) c.Position = orig;
                EmitSignal(SignalName.ShakeFinished);
            }
        }
    }
}
