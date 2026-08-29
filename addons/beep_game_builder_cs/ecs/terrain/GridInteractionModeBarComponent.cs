using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// HUD button bar for GridInteractionModeComponent. It lets players switch
    /// between select, inspect, tool, build, and disabled map interaction modes.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridInteractionModeBarComponent : Control
    {
        [Signal] public delegate void ModeButtonPressedEventHandler(int mode);

        [Export] public NodePath InteractionModePath { get; set; } = new("");
        [Export] public string[] BoundModeNames { get; set; } = Array.Empty<string>();
        [Export] public NodePath[] BoundButtonPaths { get; set; } = Array.Empty<NodePath>();
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool ShowSelect { get; set; } = true;
        [Export] public bool ShowInspect { get; set; } = true;
        [Export] public bool ShowTool { get; set; } = true;
        [Export] public bool ShowBuild { get; set; } = true;
        [Export] public bool ShowDisabled { get; set; } = false;
        [Export] public Vector2 ButtonMinimumSize { get; set; } = new(88, 34);

        private GridInteractionModeComponent? _interaction;
        private HBoxContainer? _row;
        private readonly Dictionary<GridInteractionModeComponent.InteractionMode, Button> _buttons = new();
        private readonly List<(Button Button, Action Handler)> _connectedButtons = new();

        public override void _Ready()
        {
            ResolveReferences();
            ConnectInteractionSignals();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildBar));
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectButtons();
            DisconnectInteractionSignals();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (InteractionModePath.IsEmpty)
                return new[] { "InteractionModePath should point to a GridInteractionModeComponent." };
            if (BoundModeNames.Length != BoundButtonPaths.Length)
                return new[] { "BoundModeNames and BoundButtonPaths should have the same length." };
            if (!GenerateControlsWhenPathsEmpty && BoundModeNames.Length == 0)
                return new[] { "Add authored Mode_Select/Mode_Inspect/Mode_Tool/Mode_Build buttons, set BoundModeNames/BoundButtonPaths, or enable GenerateControlsWhenPathsEmpty." };
            return Array.Empty<string>();
        }

        public void RebuildBar()
        {
            ResolveReferences();
            if (BindExistingButtons())
            {
                RefreshSelection();
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            ClearChildren();
            _buttons.Clear();

            _row = new HBoxContainer
            {
                Name = "GeneratedInteractionModeBar",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(_row, "separation", 6);
            AddChild(_row);
            SetEditedOwner(_row);

            foreach (GridInteractionModeComponent.InteractionMode mode in VisibleModes())
                AddModeButton(mode);

            RefreshSelection();
        }

        public bool SelectMode(GridInteractionModeComponent.InteractionMode mode)
        {
            ResolveReferences();
            if (_interaction == null)
                return false;

            _interaction.SetMode(mode);
            RefreshSelection();
            EmitSignal(SignalName.ModeButtonPressed, (int)mode);
            return true;
        }

        public string SelectedModeName()
        {
            ResolveReferences();
            return _interaction?.CurrentMode.ToString() ?? "";
        }

        public int VisibleModeButtonCount()
            => _buttons.Count;

        public void RefreshSelection()
        {
            ResolveReferences();
            if (_interaction == null)
                return;

            foreach (var pair in _buttons)
                if (GodotObject.IsInstanceValid(pair.Value))
                    pair.Value.SetPressedNoSignal(pair.Key == _interaction.CurrentMode);
        }

        private void AddModeButton(GridInteractionModeComponent.InteractionMode mode)
        {
            if (_row == null)
                return;

            var button = new Button
            {
                Name = $"Mode_{mode}",
                Text = LabelFor(mode),
                ToggleMode = true,
                CustomMinimumSize = ButtonMinimumSize,
                TooltipText = TooltipFor(mode)
            };
            Action handler = () => SelectMode(mode);
            button.Pressed += handler;
            _connectedButtons.Add((button, handler));
            _row.AddChild(button);
            SetEditedOwner(button);
            _buttons[mode] = button;
        }

        private IEnumerable<GridInteractionModeComponent.InteractionMode> VisibleModes()
        {
            if (ShowSelect) yield return GridInteractionModeComponent.InteractionMode.Select;
            if (ShowInspect) yield return GridInteractionModeComponent.InteractionMode.Inspect;
            if (ShowTool) yield return GridInteractionModeComponent.InteractionMode.Tool;
            if (ShowBuild) yield return GridInteractionModeComponent.InteractionMode.Build;
            if (ShowDisabled) yield return GridInteractionModeComponent.InteractionMode.Disabled;
        }

        private void ResolveReferences()
        {
            if (_interaction == null || !GodotObject.IsInstanceValid(_interaction))
            {
                _interaction = !InteractionModePath.IsEmpty
                    ? GetNodeOrNull<GridInteractionModeComponent>(InteractionModePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridInteractionModeComponent>(GetTree()?.CurrentScene) : null;
                ConnectInteractionSignals();
            }
        }

        public bool UsesSceneButtons()
            => BoundModeNames.Length > 0 || BoundButtonPaths.Length > 0 || HasConventionalModeButtons();

        private bool BindExistingButtons()
        {
            DisconnectButtons();
            _buttons.Clear();

            if (BoundModeNames.Length > 0 || BoundButtonPaths.Length > 0)
            {
                if (BoundModeNames.Length != BoundButtonPaths.Length)
                    return false;

                for (int i = 0; i < BoundModeNames.Length; i++)
                {
                    if (!TryParseMode(BoundModeNames[i], out GridInteractionModeComponent.InteractionMode mode))
                        return false;

                    Button? button = FindModeButton(mode, i);
                    if (button == null)
                        return false;

                    BindModeButton(mode, button);
                }
            }
            else
            {
                foreach (GridInteractionModeComponent.InteractionMode mode in VisibleModes())
                {
                    Button? button = FindModeButton(mode, -1);
                    if (button == null)
                        continue;

                    BindModeButton(mode, button);
                }
            }

            return _buttons.Count > 0;
        }

        private bool HasConventionalModeButtons()
        {
            foreach (GridInteractionModeComponent.InteractionMode mode in VisibleModes())
                if (FindModeButton(mode, -1) != null)
                    return true;

            return false;
        }

        private Button? FindModeButton(GridInteractionModeComponent.InteractionMode mode, int index)
        {
            if (index >= 0 && BoundButtonPaths.Length > index && !BoundButtonPaths[index].IsEmpty
                && GetNodeOrNull<Button>(BoundButtonPaths[index]) is { } pathButton)
                return pathButton;

            string name = $"Mode_{mode}";
            if (FindChild(name, recursive: true, owned: false) is Button childButton)
                return childButton;

            return GetParent()?.FindChild(name, recursive: true, owned: false) as Button;
        }

        private void BindModeButton(GridInteractionModeComponent.InteractionMode mode, Button button)
        {
            GridInteractionModeComponent.InteractionMode capturedMode = mode;
            Action handler = () => SelectMode(capturedMode);
            button.ToggleMode = true;
            if (string.IsNullOrWhiteSpace(button.Text))
                button.Text = LabelFor(mode);
            if (string.IsNullOrWhiteSpace(button.TooltipText))
                button.TooltipText = TooltipFor(mode);
            button.Pressed += handler;
            _connectedButtons.Add((button, handler));
            _buttons[mode] = button;
        }

        private static bool TryParseMode(string value, out GridInteractionModeComponent.InteractionMode mode)
        {
            if (Enum.TryParse(value?.Trim(), ignoreCase: true, out mode))
                return true;

            string normalized = (value ?? "").Trim().Replace(" ", "").Replace("-", "").Replace("_", "");
            foreach (GridInteractionModeComponent.InteractionMode candidate in Enum.GetValues(typeof(GridInteractionModeComponent.InteractionMode)))
            {
                if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    mode = candidate;
                    return true;
                }
            }

            mode = default;
            return false;
        }

        private void DisconnectButtons()
        {
            foreach ((Button button, Action handler) in _connectedButtons)
                if (GodotObject.IsInstanceValid(button))
                    button.Pressed -= handler;
            _connectedButtons.Clear();
        }

        private void ConnectInteractionSignals()
        {
            if (_interaction == null || Engine.IsEditorHint())
                return;

            _interaction.ModeChanged -= OnModeChanged;
            _interaction.ModeChanged += OnModeChanged;
        }

        private void DisconnectInteractionSignals()
        {
            if (_interaction != null && GodotObject.IsInstanceValid(_interaction))
                _interaction.ModeChanged -= OnModeChanged;
        }

        private void OnModeChanged(int mode)
            => RefreshSelection();

        private void ClearChildren()
        {
            DisconnectButtons();
            foreach (Node child in GetChildren())
                child.QueueFree();
            _row = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }

        private static string LabelFor(GridInteractionModeComponent.InteractionMode mode)
            => mode switch
            {
                GridInteractionModeComponent.InteractionMode.Select => "Select",
                GridInteractionModeComponent.InteractionMode.Inspect => "Inspect",
                GridInteractionModeComponent.InteractionMode.Tool => "Tools",
                GridInteractionModeComponent.InteractionMode.Build => "Build",
                GridInteractionModeComponent.InteractionMode.Disabled => "Lock",
                _ => mode.ToString()
            };

        private static string TooltipFor(GridInteractionModeComponent.InteractionMode mode)
            => mode switch
            {
                GridInteractionModeComponent.InteractionMode.Select => "Select cells on the map",
                GridInteractionModeComponent.InteractionMode.Inspect => "Inspect placed objects",
                GridInteractionModeComponent.InteractionMode.Tool => "Apply the selected land tool",
                GridInteractionModeComponent.InteractionMode.Build => "Confirm active build placement",
                GridInteractionModeComponent.InteractionMode.Disabled => "Ignore map clicks",
                _ => mode.ToString()
            };
    }
}
