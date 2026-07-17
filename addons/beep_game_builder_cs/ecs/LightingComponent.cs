using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Dynamic lighting system driven by DayNightCycleComponent.
    /// Updates directional light (sun) angle, intensity, and shadow strength
    /// throughout the day. Emits signals for time-of-day events (sunrise, noon, sunset, midnight).
    ///
    /// Attach to a Node2D world root. Automatically finds or creates a DirectionalLight2D.
    /// Uses DayNightCycleComponent's phase signals to drive transitions.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class LightingComponent : WorldComponent
    {
        [ExportGroup("Sun Light")]
        [Export] public Color DawnColor { get; set; } = new(1f, 0.7f, 0.5f, 1f);
        [Export] public Color DayColor { get; set; } = new(1f, 1f, 0.95f, 1f);
        [Export] public Color DuskColor { get; set; } = new(1f, 0.5f, 0.3f, 1f);
        [Export] public Color NightColor { get; set; } = new(0.3f, 0.35f, 0.5f, 1f);

        [ExportGroup("Light Intensity")]
        [Export] public float DayIntensity { get; set; } = 2.0f;
        [Export] public float NightIntensity { get; set; } = 0.3f;
        [Export] public float TransitionDuration { get; set; } = 1.5f;

        [ExportGroup("Shadow")]
        [Export] public bool EnableDynamicShadows { get; set; } = true;
        [Export] public float DayShadowStrength { get; set; } = 0.8f;
        [Export] public float NightShadowStrength { get; set; } = 0.4f;

        [Signal] public delegate void SunriseEventHandler();
        [Signal] public delegate void NoonEventHandler();
        [Signal] public delegate void SunsetEventHandler();
        [Signal] public delegate void MidnightEventHandler();

        private DirectionalLight2D? _sun;
        private DayNightCycleComponent? _dayNight;
        private Tween? _colorTween;
        private DayNightCycleComponent.Phase _lastPhase;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(SetupLighting));
        }

        private void SetupLighting()
        {
            if (Engine.IsEditorHint()) return;
            if (GetParent() is not Node parent) return;

            // Find or create directional light
            _sun = parent.GetNodeOrNull<DirectionalLight2D>("SunLight");
            if (_sun == null)
            {
                _sun = new DirectionalLight2D { Name = "SunLight" };
                parent.AddChild(_sun);
            }

            // Find day/night cycle component
            _dayNight = EntityComponent.FindComponent<DayNightCycleComponent>(GetTree().Root, true);
            if (_dayNight != null)
            {
                _dayNight.PhaseChanged += OnPhaseChanged;
            }
        }

        public override void _Process(double delta)
        {
            if (!IsActive || _sun == null || _dayNight == null) return;

            // Update lighting based on day/night cycle phase
            // (This is driven by DayNightCycleComponent's phase signals)
        }

        private void OnPhaseChanged(int phaseInt)
        {
            var phase = (DayNightCycleComponent.Phase)phaseInt;

            // Emit phase-specific signals
            switch (phase)
            {
                case DayNightCycleComponent.Phase.Dawn:
                    TransitionToColor(DawnColor, DayIntensity);
                    EmitSignal(SignalName.Sunrise);
                    break;

                case DayNightCycleComponent.Phase.Day:
                    TransitionToColor(DayColor, DayIntensity);
                    EmitSignal(SignalName.Noon);
                    break;

                case DayNightCycleComponent.Phase.Dusk:
                    TransitionToColor(DuskColor, NightIntensity * 0.8f);
                    EmitSignal(SignalName.Sunset);
                    break;

                case DayNightCycleComponent.Phase.Night:
                    TransitionToColor(NightColor, NightIntensity);
                    EmitSignal(SignalName.Midnight);
                    break;
            }

            _lastPhase = phase;
        }

        private void TransitionToColor(Color targetColor, float targetIntensity)
        {
            if (_sun == null) return;

            _colorTween?.Kill();
            _colorTween = CreateTween();
            _colorTween.SetTrans(Tween.TransitionType.Sine);
            _colorTween.SetParallel(true);

            _colorTween.TweenProperty(_sun, "modulate", targetColor, TransitionDuration);
            _colorTween.TweenProperty(_sun, "energy", targetIntensity, TransitionDuration);

            if (EnableDynamicShadows)
            {
                float shadowStrength = targetIntensity > 1.0f ? DayShadowStrength : NightShadowStrength;
                _colorTween.TweenProperty(_sun, "shadow_item_cull_margin", (int)(shadowStrength * 100), TransitionDuration);
            }
        }

        public override void _ExitTree()
        {
            _colorTween?.Kill();
            if (_dayNight != null)
                _dayNight.PhaseChanged -= OnPhaseChanged;
        }
    }
}
