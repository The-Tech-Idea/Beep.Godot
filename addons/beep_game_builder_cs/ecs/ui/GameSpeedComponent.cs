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
        [Export] public Godot.Collections.Array<NodePath> BoundButtonPaths { get; set; } = new();
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [Signal] public delegate void SpeedSelectedEventHandler(int speed);

        private static readonly string[] Labels = { "II", "1x", "2x", "3x" };
        private static readonly string[] Tips = { "Pause", "Normal speed", "Fast", "Fastest" };

        private readonly Button[] _buttons = new Button[4];
        private readonly System.Action[] _handlers = new System.Action[4];
        private CityEconomyComponent? _economy;
        private Control? _generatedRoot;
        private int _lastRunningSpeed = 1;

        public override void _Ready()
        {
            base._Ready();
            // Deferred: a node cannot AddChild to a parent that is still inside its own
            // _Ready ("Parent node is busy setting up children"), which silently produced an
            // EMPTY widget — the code ran, the error went to the log, and the UI was blank.
            // GenreHudComponent already defers its Setup for the same reason.
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            _economy = ResolveEconomy();
            RebuildControls();

            if (_economy == null)
            {
                GD.PushWarning($"[{Name}] GameSpeedComponent found no CityEconomyComponent — speed buttons are shown but disabled.");
                SetButtonsDisabled(true);
            }
            else
            {
                _economy.SpeedChanged += OnSpeedChanged;
                OnSpeedChanged(_economy.Speed);
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            DisconnectButtons();
            if (_economy != null && GodotObject.IsInstanceValid(_economy)) _economy.SpeedChanged -= OnSpeedChanged;
            _economy = null;
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && !HasAuthoredSpeedButtons())
                return new[] { "Add authored Buttons named Speed0, Speed1, Speed2, and Speed3; set BoundButtonPaths; or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
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

        public void RebuildControls()
        {
            DisconnectButtons();

            if (!BindExistingButtons())
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                BuildGeneratedButtons();
            }

            int fs = UiSurface.FontSize(this);
            for (int i = 0; i < 4; i++)
                ConfigureButton(i, fs);
        }

        private void BuildGeneratedButtons()
        {
            if (_generatedRoot != null && GodotObject.IsInstanceValid(_generatedRoot))
                _generatedRoot.QueueFree();

            _generatedRoot = null;
            if (GetParent() is not Godot.Control parent) return;

            var row = new HBoxContainer { Name = "SpeedButtons" };
            KitChrome.SetConstantOverrideIfChanged(row, "separation", 4);
            parent.AddChild(row);
            SetEditedOwner(row);
            _generatedRoot = row;

            for (int i = 0; i < 4; i++)
            {
                var b = new KitIconButton
                {
                    Name = $"Speed{i}",
                };
                row.AddChild(b);
                SetEditedOwner(b);
                _buttons[i] = b;
            }
        }

        private bool BindExistingButtons()
        {
            if (!HasAuthoredSpeedButtons())
                return false;

            for (int i = 0; i < 4; i++)
            {
                Button? button = FindSpeedButton(i);
                if (button == null)
                    return false;
                _buttons[i] = button;
            }

            if (_generatedRoot != null && GodotObject.IsInstanceValid(_generatedRoot))
                _generatedRoot.QueueFree();
            _generatedRoot = null;
            return true;
        }

        public bool UsesSceneButtons() => HasAuthoredSpeedButtons();

        private bool HasAuthoredSpeedButtons()
        {
            for (int i = 0; i < 4; i++)
                if (FindSpeedButton(i) == null)
                    return false;

            return true;
        }

        private Button? FindSpeedButton(int index)
        {
            if (BoundButtonPaths.Count > index && !BoundButtonPaths[index].IsEmpty && GetNodeOrNull<Button>(BoundButtonPaths[index]) is { } pathButton)
                return pathButton;

            string name = $"Speed{index}";
            if (FindChild(name, recursive: true, owned: false) is Button childButton)
                return childButton;

            return GetParent()?.FindChild(name, recursive: true, owned: false) as Button;
        }

        private void ConfigureButton(int index, int fontSize)
        {
            Button? button = _buttons[index];
            if (button == null || !GodotObject.IsInstanceValid(button))
                return;

            button.Text = Labels[index];
            button.TooltipText = Tips[index];
            button.ToggleMode = true;
            button.FocusMode = Godot.Control.FocusModeEnum.All;
            button.CustomMinimumSize = new Vector2(fontSize * 2.25f, fontSize * 2.25f);
            KitChrome.SetFontSizeOverrideIfChanged(button, "font_size", fontSize);

            if (button is KitIconButton iconButton)
            {
                iconButton.Glyph = Labels[index];
                iconButton.Accent = index == 0 ? UiSurface.Role.Warning : UiSurface.Role.Info;
            }

            int speed = index;
            System.Action handler = () => Select(speed);
            button.Pressed += handler;
            _handlers[index] = handler;
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

        private void SetButtonsDisabled(bool disabled)
        {
            for (int i = 0; i < _buttons.Length; i++)
                if (_buttons[i] != null && GodotObject.IsInstanceValid(_buttons[i]))
                    _buttons[i].Disabled = disabled;
        }

        private void DisconnectButtons()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null && GodotObject.IsInstanceValid(_buttons[i]) && _handlers[i] != null)
                    _buttons[i].Pressed -= _handlers[i];
                _handlers[i] = null!;
                _buttons[i] = null!;
            }
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}
