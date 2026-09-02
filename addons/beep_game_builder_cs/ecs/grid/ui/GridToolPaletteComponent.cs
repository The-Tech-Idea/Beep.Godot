using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// HUD palette for GridToolActionComponent. It creates tool buttons for common
    /// farming/settler actions and keeps the selected button in sync with the tool.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridToolPaletteComponent : Control
    {
        [Signal] public delegate void ToolSelectedEventHandler(string action);
        [Signal] public delegate void ToolApplyRequestedEventHandler(string action, int appliedCount);

        [Export] public NodePath ToolActionPath { get; set; } = new("");
        [Export] public NodePath InteractionModePath { get; set; } = new("");
        [Export] public string[] BoundActionNames { get; set; } = Array.Empty<string>();
        [Export] public NodePath[] BoundButtonPaths { get; set; } = Array.Empty<NodePath>();
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool AutoSwitchInteractionMode { get; set; } = true;
        [Export] public bool IncludeApplyButton { get; set; } = false;
        [Export] public bool ShowClear { get; set; } = true;
        [Export] public bool ShowHoe { get; set; } = true;
        [Export] public bool ShowWater { get; set; } = true;
        [Export] public bool ShowPlant { get; set; } = true;
        [Export] public bool ShowHarvest { get; set; } = true;
        [Export] public bool ShowQueueJob { get; set; } = true;
        [Export] public bool ShowRoad { get; set; } = true;
        [Export] public bool ShowRemoveRoad { get; set; } = true;
        [Export] public Vector2 ButtonMinimumSize { get; set; } = new(86, 34);

        private GridToolActionComponent? _tools;
        private GridInteractionModeComponent? _interactionMode;
        private HBoxContainer? _row;
        private readonly Dictionary<GridToolActionComponent.ToolAction, Button> _buttons = new();
        private readonly List<(Button Button, Action Handler)> _connectedButtons = new();

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildPalette));
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectButtons();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (ToolActionPath.IsEmpty)
                return new[] { "ToolActionPath should point to a GridToolActionComponent." };
            if (BoundActionNames.Length != BoundButtonPaths.Length)
                return new[] { "BoundActionNames and BoundButtonPaths should have the same length." };
            if (!GenerateControlsWhenPathsEmpty && BoundActionNames.Length == 0)
                return new[] { "Add authored Tool_Clear/Tool_Hoe/Tool_Water buttons, set BoundActionNames/BoundButtonPaths, or enable GenerateControlsWhenPathsEmpty." };
            return Array.Empty<string>();
        }

        public void RebuildPalette()
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
                Name = "GeneratedToolPalette",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(_row, "separation", 6);
            AddChild(_row);
            SetEditedOwner(_row);

            foreach (GridToolActionComponent.ToolAction action in VisibleActions())
                AddToolButton(action);

            if (IncludeApplyButton)
            {
                var apply = new Button
                {
                    Name = "ApplyTool",
                    Text = "Apply",
                    CustomMinimumSize = ButtonMinimumSize
                };
                Action handler = () => ApplySelectedTool();
                apply.Pressed += handler;
                _connectedButtons.Add((apply, handler));
                _row.AddChild(apply);
                SetEditedOwner(apply);
            }

            RefreshSelection();
        }

        public bool SelectTool(GridToolActionComponent.ToolAction action)
        {
            ResolveReferences();
            if (_tools == null)
                return false;

            _tools.CurrentAction = action;
            if (AutoSwitchInteractionMode && _interactionMode != null)
                _interactionMode.ToolMode();
            RefreshSelection();
            EmitSignal(SignalName.ToolSelected, action.ToString());
            return true;
        }

        public int ApplySelectedTool()
        {
            ResolveReferences();
            if (_tools == null)
                return 0;

            int applied = _tools.ApplyCurrent();
            EmitSignal(SignalName.ToolApplyRequested, _tools.CurrentAction.ToString(), applied);
            return applied;
        }

        public string SelectedActionName()
        {
            ResolveReferences();
            return _tools?.CurrentAction.ToString() ?? "";
        }

        public int VisibleToolButtonCount() => _buttons.Count;

        public bool UsesSceneButtons()
            => BoundActionNames.Length > 0 || BoundButtonPaths.Length > 0 || HasConventionalToolButtons();

        public void RefreshSelection()
        {
            ResolveReferences();
            if (_tools == null)
                return;

            foreach (var pair in _buttons)
                if (GodotObject.IsInstanceValid(pair.Value))
                    pair.Value.SetPressedNoSignal(pair.Key == _tools.CurrentAction);
        }

        private void AddToolButton(GridToolActionComponent.ToolAction action)
        {
            if (_row == null)
                return;

            var button = new Button
            {
                Name = $"Tool_{action}",
                Text = LabelFor(action),
                ToggleMode = true,
                CustomMinimumSize = ButtonMinimumSize,
                TooltipText = action.ToString()
            };
            Action handler = () => SelectTool(action);
            button.Pressed += handler;
            _connectedButtons.Add((button, handler));
            _row.AddChild(button);
            SetEditedOwner(button);
            _buttons[action] = button;
        }

        private IEnumerable<GridToolActionComponent.ToolAction> VisibleActions()
        {
            if (ShowClear) yield return GridToolActionComponent.ToolAction.Clear;
            if (ShowHoe) yield return GridToolActionComponent.ToolAction.Hoe;
            if (ShowWater) yield return GridToolActionComponent.ToolAction.Water;
            if (ShowPlant) yield return GridToolActionComponent.ToolAction.Plant;
            if (ShowHarvest) yield return GridToolActionComponent.ToolAction.Harvest;
            if (ShowQueueJob) yield return GridToolActionComponent.ToolAction.QueueJob;
            if (ShowRoad) yield return GridToolActionComponent.ToolAction.Road;
            if (ShowRemoveRoad) yield return GridToolActionComponent.ToolAction.RemoveRoad;
        }

        private static string LabelFor(GridToolActionComponent.ToolAction action)
            => action switch
            {
                GridToolActionComponent.ToolAction.QueueJob => "Job",
                GridToolActionComponent.ToolAction.RemoveRoad => "No Road",
                _ => action.ToString()
            };

        private void ResolveReferences()
        {
            if (_tools == null || !GodotObject.IsInstanceValid(_tools))
                _tools = !ToolActionPath.IsEmpty
                    ? GetNodeOrNull<GridToolActionComponent>(ToolActionPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridToolActionComponent>(GetTree()?.CurrentScene) : null;

            if (_interactionMode == null || !GodotObject.IsInstanceValid(_interactionMode))
                _interactionMode = !InteractionModePath.IsEmpty
                    ? GetNodeOrNull<GridInteractionModeComponent>(InteractionModePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridInteractionModeComponent>(GetTree()?.CurrentScene) : null;
        }

        private bool BindExistingButtons()
        {
            DisconnectButtons();
            _buttons.Clear();

            if (BoundActionNames.Length > 0 || BoundButtonPaths.Length > 0)
            {
                if (BoundActionNames.Length != BoundButtonPaths.Length)
                    return false;

                for (int i = 0; i < BoundActionNames.Length; i++)
                {
                    if (!TryParseAction(BoundActionNames[i], out GridToolActionComponent.ToolAction action))
                        return false;

                    Button? button = FindToolButton(action, i);
                    if (button == null)
                        return false;

                    BindToolButton(action, button);
                }
            }
            else
            {
                foreach (GridToolActionComponent.ToolAction action in VisibleActions())
                {
                    Button? button = FindToolButton(action, -1);
                    if (button == null)
                        continue;

                    BindToolButton(action, button);
                }
            }

            return _buttons.Count > 0;
        }

        private bool HasConventionalToolButtons()
        {
            foreach (GridToolActionComponent.ToolAction action in VisibleActions())
                if (FindToolButton(action, -1) != null)
                    return true;

            return false;
        }

        private Button? FindToolButton(GridToolActionComponent.ToolAction action, int index)
        {
            if (index >= 0 && BoundButtonPaths.Length > index && !BoundButtonPaths[index].IsEmpty
                && GetNodeOrNull<Button>(BoundButtonPaths[index]) is { } pathButton)
                return pathButton;

            string name = $"Tool_{action}";
            if (FindChild(name, recursive: true, owned: false) is Button childButton)
                return childButton;

            return GetParent()?.FindChild(name, recursive: true, owned: false) as Button;
        }

        private void BindToolButton(GridToolActionComponent.ToolAction action, Button button)
        {
            GridToolActionComponent.ToolAction capturedAction = action;
            Action handler = () => SelectTool(capturedAction);
            button.ToggleMode = true;
            if (string.IsNullOrWhiteSpace(button.Text))
                button.Text = LabelFor(action);
            if (string.IsNullOrWhiteSpace(button.TooltipText))
                button.TooltipText = action.ToString();
            button.Pressed += handler;
            _connectedButtons.Add((button, handler));
            _buttons[action] = button;
        }

        private static bool TryParseAction(string value, out GridToolActionComponent.ToolAction action)
        {
            if (Enum.TryParse(value?.Trim(), ignoreCase: true, out action))
                return true;

            string normalized = (value ?? "").Trim().Replace(" ", "").Replace("-", "").Replace("_", "");
            foreach (GridToolActionComponent.ToolAction candidate in Enum.GetValues(typeof(GridToolActionComponent.ToolAction)))
            {
                if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    action = candidate;
                    return true;
                }
            }

            action = default;
            return false;
        }

        private void DisconnectButtons()
        {
            foreach ((Button button, Action handler) in _connectedButtons)
                if (GodotObject.IsInstanceValid(button))
                    button.Pressed -= handler;
            _connectedButtons.Clear();
        }

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
    }
}
