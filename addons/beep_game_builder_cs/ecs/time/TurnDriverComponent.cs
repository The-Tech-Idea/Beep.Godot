using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The thing that actually ends a turn. A turn-based game needs one of these
    /// somewhere, or its clock never advances.
    ///
    /// This exists because "declares the turn axis" and "can advance a turn" were
    /// two different facts that nothing connected. The strategy genre declared
    /// turns in its genre.json and shipped no way to end one, so its clock never
    /// ticked and every modifier duration and every producer in the game was
    /// frozen for the whole session - silently, because a frozen duration looks
    /// exactly like a long one. The clock now reports a missing driver instead of
    /// pretending, and this component is the driver.
    ///
    /// Binds an authored Button when the scene has one, and generates a plain
    /// default when it does not, the same bind-or-generate shape the HUD panels
    /// use. On the real-time axis it hides itself: there are no turns to end.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TurnDriverComponent : Control
    {
        /// <summary>A turn was ended through this driver. Carries the new turn number.</summary>
        [Signal] public delegate void TurnRequestedEventHandler(int turn);

        /// <summary>The authored Button, if the scene has one. Empty falls back to a child named "EndTurn".</summary>
        [Export] public NodePath EndTurnButtonPath { get; set; } = new("");

        /// <summary>Build a default button when the scene authored none.</summary>
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = true;

        /// <summary>Optional input action that also ends the turn, for keyboard/gamepad play.</summary>
        [Export] public string EndTurnAction { get; set; } = "";

        [Export] public string ButtonText { get; set; } = "End Turn";

        private Button? _button;
        private GameClock? _clock;
        private bool _connected;

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
                return;

            _clock = GameApp.Instance?.Clock;
            BindOrGenerateButton();

            // Nothing to drive on the real-time axis - the clock advances itself.
            bool turnBasedGame = _clock != null && _clock.Axis == GameTimeAxis.Turns;
            Visible = turnBasedGame;
            SetProcessUnhandledInput(turnBasedGame && !string.IsNullOrWhiteSpace(EndTurnAction));

            if (_clock == null)
                GD.PushWarning($"[{Name}] No GameClock found (GameApp is the owner). This driver can end no turns.");
        }

        public override void _ExitTree()
        {
            if (_connected && _button != null && GodotObject.IsInstanceValid(_button))
                _button.Pressed -= OnButtonPressed;
            _connected = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (string.IsNullOrWhiteSpace(EndTurnAction) || !@event.IsActionPressed(EndTurnAction))
                return;

            RequestEndTurn();
            GetViewport()?.SetInputAsHandled();
        }

        /// <summary>
        /// Ends the turn. Reports whether the clock actually advanced - a paused
        /// or real-time clock refuses, and a caller that wants to re-enable UI
        /// needs to know which happened.
        /// </summary>
        public bool RequestEndTurn()
        {
            if (_clock == null || !_clock.EndTurn())
                return false;

            EmitSignal(SignalName.TurnRequested, _clock.Turn);
            return true;
        }

        private void BindOrGenerateButton()
        {
            _button = !EndTurnButtonPath.IsEmpty
                ? GetNodeOrNull<Button>(EndTurnButtonPath)
                : FindChild("EndTurn", recursive: true, owned: false) as Button
                  ?? GetParent()?.FindChild("EndTurn", recursive: true, owned: false) as Button;

            if (_button == null && GenerateControlsWhenPathsEmpty)
            {
                _button = new Button { Name = "EndTurn", Text = ButtonText };
                AddChild(_button);
                // A Control child is not laid out by anything; without this the
                // generated button has zero size and cannot be clicked.
                _button.SetAnchorsPreset(LayoutPreset.FullRect);
            }

            if (_button == null)
                return;

            _button.Pressed += OnButtonPressed;
            _connected = true;
        }

        // Button.Pressed is an Action; RequestEndTurn reports its outcome, so it
        // cannot be the handler directly.
        private void OnButtonPressed() => RequestEndTurn();
    }
}
