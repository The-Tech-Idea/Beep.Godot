using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Auto-destroy component. Blind — attach to any entity to auto-remove after time.
    /// Works for projectiles, effects, temporary objects, decals.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class LifetimeComponent : WorldComponent
    {
        [Export] public float Lifetime { get; set; } = 2f;
        [Export] public bool FadeOut { get; set; } = false;
        [Export] public float FadeStartPercent { get; set; } = 0.8f;

        [Signal] public delegate void ExpiredEventHandler();
        [Signal] public delegate void FadingStartedEventHandler();

        private float _elapsed;
        private CanvasItem? _canvas;

        public override void _Ready()
        {
            base._Ready();
            _canvas = GetParent() as CanvasItem;
        }

        public override void _Process(double delta)
        {
            if (!IsActive) return;
            _elapsed += (float)delta;
            float pct = _elapsed / Lifetime;

            if (FadeOut && _canvas != null && pct >= FadeStartPercent)
            {
                if (pct < FadeStartPercent + 0.01f) EmitSignal(SignalName.FadingStarted);
                float fadePct = (pct - FadeStartPercent) / (1f - FadeStartPercent);
                _canvas.Modulate = new Color(1, 1, 1, 1f - fadePct);
            }

            if (_elapsed >= Lifetime)
            {
                EmitSignal(SignalName.Expired);
                GetParent()?.QueueFree();
            }
        }

        public void Reset(float newLifetime = -1)
        {
            _elapsed = 0;
            if (newLifetime > 0) Lifetime = newLifetime;
            if (_canvas != null) _canvas.Modulate = Colors.White;
        }
    }
}
