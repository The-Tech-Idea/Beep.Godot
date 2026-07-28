using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Survival HUD: bottom-left vitals — Health, Hunger, Thirst, Stamina.
    ///
    /// Driven by <see cref="SurvivalVitalsComponent"/>. All four readouts were previously
    /// registered as <c>Placeholder(...)</c>, which meant each kept whatever text was typed into
    /// the scene and only warned — so the numbers a player saw were invented and never moved.
    /// Placeholder is now the FALLBACK for a scene with no vitals component, not the normal path,
    /// exactly as in <see cref="CityBuilderHudComponent"/>.</summary>
    [Tool]
    [GlobalClass]
    public partial class SurvivalHudComponent : GenreHudComponent
    {
        [Export] public NodePath HealthPath { get; set; } = "Vitals/HealthLabel";
        [Export] public NodePath HungerPath { get; set; } = "Vitals/HungerLabel";
        [Export] public NodePath ThirstPath { get; set; } = "Vitals/ThirstLabel";
        [Export] public NodePath StaminaPath { get; set; } = "Vitals/StaminaLabel";

        /// <summary>Optional toast host for vitals alerts (starving, parched).</summary>
        [Export] public NodePath AlertHostPath { get; set; } = new("");

        protected override string Genre => "survival";

        private SurvivalVitalsComponent? _vitals;
        private ToastNotificationComponent? _alerts;
        private Godot.Control? _health, _hunger, _thirst, _stamina;

        protected override void Wire()
        {
            _vitals = FindInScene<SurvivalVitalsComponent>();

            if (_vitals == null)
            {
                // No simulation in this scene: fall back to developer-driven readouts so the HUD
                // still functions, and say so once. This is the only path that should ever warn.
                Placeholder(HealthPath, "health");
                Placeholder(HungerPath, "hunger");
                Placeholder(ThirstPath, "thirst");
                Placeholder(StaminaPath, "stamina");
                return;
            }

            _health = ResolveReadout(HealthPath, "health");
            _hunger = ResolveReadout(HungerPath, "hunger");
            _thirst = ResolveReadout(ThirstPath, "thirst");
            _stamina = ResolveReadout(StaminaPath, "stamina");
            _alerts = ResolveNode<ToastNotificationComponent>(AlertHostPath);

            _vitals.VitalsChanged += OnVitals;
            _vitals.VitalCritical += OnCritical;
            OnVitals();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_vitals != null && GodotObject.IsInstanceValid(_vitals))
            {
                _vitals.VitalsChanged -= OnVitals;
                _vitals.VitalCritical -= OnCritical;
            }
            _vitals = null;
        }


        private void OnVitals()
        {
            if (_vitals == null) return;

            // Each vital shows the number AND drives its badge fill, so a bar is legible at a
            // glance and exact when read — the reference survival HUDs all do both.
            Show(_health, _vitals.Health, _vitals.HealthFraction);
            Show(_hunger, _vitals.Hunger, _vitals.HungerFraction);
            Show(_thirst, _vitals.Thirst, _vitals.ThirstFraction);
            Show(_stamina, _vitals.Stamina, _vitals.StaminaFraction);

            void Show(Godot.Control? c, float value, float fraction)
            {
                if (c == null) return;
                SetReadout(c, Mathf.RoundToInt(value).ToString(), fraction);
                // Empty is danger, low is a warning, anything else keeps its own colour.
                Tint(c, fraction <= 0f ? UiSurface.Role.Danger
                      : fraction <= _vitals!.LowThreshold ? UiSurface.Role.Warning
                      : null);
            }
        }

        /// <summary>Null-conditional, so a scene with no toast host stays usable — same as the
        /// city-builder HUD. ShowToast is the INSTANCE method; Show is a static shortcut that
        /// posts to a different host and would bypass the one this HUD resolved.</summary>
        private void OnCritical(string vital)
            => _alerts?.ShowToast(vital == "thirst" ? "Dehydrated — find water"
                                                   : "Starving — find food",
                                  ToastNotificationComponent.ToastType.Warning);
    }
}
