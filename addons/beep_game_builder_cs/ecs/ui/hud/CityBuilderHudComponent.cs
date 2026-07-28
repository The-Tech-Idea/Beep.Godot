using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// City-builder HUD: the top resource strip — treasury + monthly delta, population, power,
    /// happiness, date — plus the RCI demand meter.
    ///
    /// Every readout is now driven by <see cref="CityEconomyComponent"/>. This component
    /// previously registered all five as <c>Placeholder(...)</c>, which meant each kept whatever
    /// text was typed into the scene and only warned — so the numbers a player saw were
    /// invented. `Placeholder` is now the FALLBACK for a scene with no economy, not the
    /// normal path.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CityBuilderHudComponent : GenreHudComponent
    {
        [Export] public NodePath PopulationPath { get; set; } = "TopBar/Bar/PopulationRow/PopulationLabel";
        [Export] public NodePath BudgetPath { get; set; } = "TopBar/Bar/BudgetRow/BudgetLabel";
        [Export] public NodePath PowerPath { get; set; } = "TopBar/Bar/PowerRow/PowerLabel";
        [Export] public NodePath HappinessPath { get; set; } = "TopBar/Bar/HappinessRow/HappinessLabel";
        [Export] public NodePath DatePath { get; set; } = "TopBar/Bar/DateRow/DateLabel";

        /// <summary>Optional RCI meter. Empty = this scene has none.</summary>
        [Export] public NodePath DemandMeterPath { get; set; } = new("");
        /// <summary>Optional toast host for economy alerts (debt, power shortfall).</summary>
        [Export] public NodePath AlertHostPath { get; set; } = new("");

        protected override string Genre => "citybuilder";

        private CityEconomyComponent? _economy;
        private DemandMeterComponent? _demand;
        private ToastNotificationComponent? _alerts;
        // Godot.Control, not Label: a readout is now either a Label (older scenes) or a
        // ResourceBadgeComponent (the game-centric capsule). Binding to Label only would mean
        // rewriting every genre's scene in lockstep with this component, and would silently
        // resolve to null the moment a scene upgraded — which is exactly the class of breakage
        // the DemandMeterPath move caused earlier.
        private Godot.Control? _population, _budget, _power, _happiness, _date;

        protected override void Wire()
        {
            _economy = FindInScene<CityEconomyComponent>();

            if (_economy == null)
            {
                // No simulation in this scene: fall back to developer-driven readouts so the HUD
                // still functions, and say so once. This is the only path that should ever warn.
                Placeholder(PopulationPath, "population");
                Placeholder(BudgetPath, "budget");
                Placeholder(PowerPath, "power");
                Placeholder(HappinessPath, "happiness");
                Placeholder(DatePath, "date");
                return;
            }

            _population = ResolveReadout(PopulationPath, "population");
            _budget = ResolveReadout(BudgetPath, "budget");
            _power = ResolveReadout(PowerPath, "power");
            _happiness = ResolveReadout(HappinessPath, "happiness");
            _date = ResolveReadout(DatePath, "date");
            _demand = ResolveNode<DemandMeterComponent>(DemandMeterPath);
            _alerts = ResolveNode<ToastNotificationComponent>(AlertHostPath);

            _economy.StatsChanged += OnStats;
            _economy.AlertRaised += OnAlert;
            OnStats();
        }


        private void OnStats()
        {
            if (_economy == null) return;

            SetReadout(_population, _economy.Population.ToString("N0"));

            // The DELTA is what a city-builder player actually reads — a balance alone does not
            // say whether the city is sustainable. Both are shown, the delta coloured.
            if (_budget != null)
            {
                int d = _economy.MonthlyDelta;
                SetReadout(_budget, $"{_economy.Treasury:N0}   {(d >= 0 ? "▲" : "▼")}{d:+#,0;-#,0;0}");
                Tint(_budget, d >= 0 ? UiSurface.Role.Success : UiSurface.Role.Danger);
            }

            if (_power != null)
            {
                // Capacity is a ratio, so the badge draws it as a fill as well as a number —
                // the reference HUDs all show power/water as a meter, not a bare pair.
                float cap = _economy.PowerCapacity <= 0 ? -1f
                          : Mathf.Clamp((float)_economy.PowerUsed / _economy.PowerCapacity, 0f, 1f);
                SetReadout(_power, $"{_economy.PowerUsed} / {_economy.PowerCapacity}", cap);
                bool over = _economy.PowerUsed > _economy.PowerCapacity;
                Tint(_power, over ? UiSurface.Role.Danger : null);
            }

            SetReadout(_happiness, $"{_economy.Happiness}%", _economy.Happiness / 100f);
            SetReadout(_date, $"Yr {_economy.Year} · {_economy.Season}");

            _demand?.SetDemand(_economy.DemandResidential, _economy.DemandCommercial, _economy.DemandIndustrial);
        }

        /// <summary>Economy alerts carry a severity string; the toast host takes its own enum.
        /// Mapped here rather than making the simulation depend on a UI type — the economy must
        /// stay usable by a project that has no toast host at all.</summary>
        private void OnAlert(string severity, string text)
            => _alerts?.ShowToast(text, severity switch
            {
                "danger" => ToastNotificationComponent.ToastType.Error,
                "warning" => ToastNotificationComponent.ToastType.Warning,
                "success" => ToastNotificationComponent.ToastType.Success,
                _ => ToastNotificationComponent.ToastType.Info,
            });

        public override void _ExitTree()
        {
            base._ExitTree();
            // The economy lives in the gameplay scene and outlives this HUD across a scene
            // change, so the handlers must come off or they fire on a freed component.
            if (_economy != null && GodotObject.IsInstanceValid(_economy))
            {
                _economy.StatsChanged -= OnStats;
                _economy.AlertRaised -= OnAlert;
            }
            _economy = null;
        }
    }
}
