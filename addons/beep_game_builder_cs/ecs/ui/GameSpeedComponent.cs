using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Simulation speed control — pause / 1x / 2x / 3x.
    ///
    /// Pause is the most-pressed control in a city builder: the player pauses to plan. It had
    /// no representation in the HUD at all, and the simulation had no way to be stopped.
    ///
    /// This drives <see cref="CityEconomyComponent.Speed"/> only — it deliberately does NOT
    /// touch <c>GetTree().Paused</c>. Pausing the tree would freeze the HUD, the camera and
    /// the pause menu itself; a city builder pauses its SIMULATION while the interface stays
    /// fully live so the player can keep building. Those are different things and conflating
    /// them is the usual bug here.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameSpeedComponent : UIComponent
    {
        /// <summary>Economy to drive. Empty = search the scene for the first one.</summary>
        [Export] public NodePath EconomyPath { get; set; } = new("");
        /// <summary>Also bind the pause action, so space works as well as the button.</summary>
        [Export] public string TogglePauseAction { get; set; } = "";

        [Signal] public delegate void SpeedSelectedEventHandler(int speed);

        private static readonly string[] Labels = { "‖", "▶", "▶▶", "▶▶▶" };
        private static readonly string[] Tips = { "Pause", "Normal speed", "Fast", "Fastest" };

        private readonly Button[] _buttons = new Button[4];
        private CityEconomyComponent? _economy;
        private int _lastRunningSpeed = 1;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            // Deferred: a node cannot AddChild to a parent that is still inside its own
            // _Ready ("Parent node is busy setting up children"), which silently produced an
            // EMPTY widget — the code ran, the error went to the log, and the UI was blank.
            // GenreHudComponent already defers its Setup for the same reason.
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            _economy = ResolveEconomy();
            if (_economy == null)
                GD.PushWarning($"[{Name}] GameSpeedComponent found no CityEconomyComponent — the buttons would control nothing, so they are not built.");
            else
            {
                Build();
                _economy.SpeedChanged += OnSpeedChanged;
                OnSpeedChanged(_economy.Speed);
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_economy != null && GodotObject.IsInstanceValid(_economy)) _economy.SpeedChanged -= OnSpeedChanged;
            _economy = null;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Engine.IsEditorHint() || _economy == null) return;
            if (string.IsNullOrEmpty(TogglePauseAction) || !InputMap.HasAction(TogglePauseAction)) return;
            if (!@event.IsActionPressed(TogglePauseAction)) return;
            // Toggle back to the speed the player was last running at, not always 1x — losing
            // 3x every time you glance at something is the classic annoyance.
            Select(_economy.Speed == 0 ? _lastRunningSpeed : 0);
            GetViewport()?.SetInputAsHandled();
        }

        private CityEconomyComponent? ResolveEconomy()
        {
            if (!EconomyPath.IsEmpty && GetNodeOrNull<CityEconomyComponent>(EconomyPath) is { } direct) return direct;
            var scene = GetTree()?.CurrentScene;
            return scene == null ? null : FindIn(scene);

            static CityEconomyComponent? FindIn(Node n)
            {
                if (n is CityEconomyComponent c) return c;
                foreach (var child in n.GetChildren())
                    if (FindIn(child) is { } found) return found;
                return null;
            }
        }

        private void Build()
        {
            if (GetParent() is not Godot.Control parent) return;
            int fs = UiSurface.FontSize(this);
            var row = new HBoxContainer { Name = "SpeedButtons" };
            row.AddThemeConstantOverride("separation", 4);
            parent.AddChild(row);

            for (int i = 0; i < 4; i++)
            {
                int speed = i;
                var b = new KitIconButton
                {
                    Name = $"Speed{i}",
                    Glyph = Labels[i],
                    TooltipText = Tips[i],
                    ToggleMode = true,           // shows WHICH speed is active, not just that it was clicked
                    CustomMinimumSize = new Vector2(fs * 2.25f, fs * 2.25f),
                    FocusMode = Godot.Control.FocusModeEnum.None,
                    Accent = i == 0 ? UiSurface.Role.Warning : UiSurface.Role.Info,
                };
                b.Pressed += () => Select(speed);
                row.AddChild(b);
                _buttons[i] = b;
            }
        }

        /// <summary>Set the speed. Public so a hotkey or a cutscene can drive it too.</summary>
        public void Select(int speed)
        {
            if (_economy == null) return;
            if (speed > 0) _lastRunningSpeed = speed;
            _economy.Speed = speed;
            EmitSignal(SignalName.SpeedSelected, speed);
        }

        private void OnSpeedChanged(int speed)
        {
            for (int i = 0; i < _buttons.Length; i++)
                if (_buttons[i] != null && GodotObject.IsInstanceValid(_buttons[i]))
                    _buttons[i].SetPressedNoSignal(i == speed);
        }
    }
}
