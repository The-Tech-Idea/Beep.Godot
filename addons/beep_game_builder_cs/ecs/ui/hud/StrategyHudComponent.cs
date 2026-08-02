using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Strategy HUD: top resource bar — Gold, Food, Wood, Units — plus a Turn readout.
    ///
    /// Driven by <see cref="StrategyEmpireComponent"/>. All five were previously registered as
    /// <c>Placeholder(...)</c>, so every number shown was typed into the scene. Placeholder is
    /// now the FALLBACK for a scene with no empire component, not the normal path.</summary>
    [Tool]
    [GlobalClass]
    public partial class StrategyHudComponent : GenreHudComponent
    {
        [Export] public NodePath GoldPath { get; set; } = "TopBar/Bar/GoldLabel";
        [Export] public NodePath FoodPath { get; set; } = "TopBar/Bar/FoodLabel";
        [Export] public NodePath WoodPath { get; set; } = "TopBar/Bar/WoodLabel";
        [Export] public NodePath UnitsPath { get; set; } = "TopBar/Bar/UnitsLabel";
        [Export] public NodePath TurnPath { get; set; } = "TurnLabel";

        /// <summary>Optional toast host for empire alerts (starvation, empty treasury).</summary>
        [Export] public NodePath AlertHostPath { get; set; } = new("");

        protected override string Genre => "strategy";

        private StrategyEmpireComponent? _empire;
        private ToastNotificationComponent? _alerts;
        private Godot.Control? _gold, _food, _wood, _units, _turn;

        protected override void Wire()
        {
            _empire = FindInScene<StrategyEmpireComponent>();

            if (_empire == null)
            {
                // No simulation in this scene: fall back to developer-driven readouts so the HUD
                // still functions, and say so once.
                Placeholder(GoldPath, "gold");
                Placeholder(FoodPath, "food");
                Placeholder(WoodPath, "wood");
                Placeholder(UnitsPath, "units");
                Placeholder(TurnPath, "turn");
                return;
            }

            _gold = ResolveReadout(GoldPath, "gold");
            _food = ResolveReadout(FoodPath, "food");
            _wood = ResolveReadout(WoodPath, "wood");
            _units = ResolveReadout(UnitsPath, "units");
            _turn = ResolveReadout(TurnPath, "turn");
            _alerts = ResolveNode<ToastNotificationComponent>(AlertHostPath);

            _empire.EmpireChanged += OnEmpire;
            _empire.TurnAdvanced += OnTurn;
            _empire.EmpireAlert += OnAlert;
            OnEmpire();
            OnTurn(_empire.Turn);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_empire != null && GodotObject.IsInstanceValid(_empire))
            {
                _empire.EmpireChanged -= OnEmpire;
                _empire.TurnAdvanced -= OnTurn;
                _empire.EmpireAlert -= OnAlert;
            }
            _empire = null;
        }

        /// <summary>A stockpile alone does not say whether an empire is sustainable, so every
        /// resource shows its per-turn delta alongside the total — the same reasoning as the
        /// city-builder's treasury readout.</summary>
        private static string WithDelta(int value, int delta)
            => $"{value:N0}   {(delta >= 0 ? "▲" : "▼")}{Mathf.Abs(delta):N0}";

        private void OnEmpire()
        {
            if (_empire == null) return;

            SetReadout(_gold, WithDelta(_empire.Gold, _empire.GoldDelta));
            Tint(_gold, _empire.IsBankrupt ? UiSurface.Role.Danger
                 : _empire.GoldDelta < 0 ? UiSurface.Role.Warning
                 : null);

            SetReadout(_food, WithDelta(_empire.Food, _empire.FoodDelta));
            Tint(_food, _empire.IsStarving ? UiSurface.Role.Danger
                 : _empire.FoodDelta < 0 ? UiSurface.Role.Warning
                 : null);

            SetReadout(_wood, WithDelta(_empire.Wood, _empire.WoodDelta));

            // Units fill against what the food yield can sustain, so the bar answers "am I
            // over-extended" rather than repeating the count next to it.
            float sustain = _empire.SustainableUnits <= 0 ? 1f
                : Mathf.Clamp((float)_empire.Units / _empire.SustainableUnits, 0f, 1f);
            SetReadout(_units, $"{_empire.Units}", sustain);
            Tint(_units, _empire.Units > _empire.SustainableUnits ? UiSurface.Role.Warning : null);
        }

        private void OnTurn(int turn) => SetReadout(_turn, turn.ToString());

        private void OnAlert(string severity, string text)
            => _alerts?.ShowToast(text, severity switch
            {
                "danger" => ToastNotificationComponent.ToastType.Error,
                "warning" => ToastNotificationComponent.ToastType.Warning,
                "success" => ToastNotificationComponent.ToastType.Success,
                _ => ToastNotificationComponent.ToastType.Info,
            });
    }
}
