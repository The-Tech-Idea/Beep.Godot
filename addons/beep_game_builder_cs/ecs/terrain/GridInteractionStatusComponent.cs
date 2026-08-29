using Godot;
using System;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD status readout for grid interaction. It shows the active
    /// mode, hovered/placement cell, selected tool or build id, and recent
    /// interaction feedback.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridInteractionStatusComponent : Control
    {
        [Export] public NodePath InteractionModePath { get; set; } = new("");
        [Export] public NodePath SelectionPath { get; set; } = new("");
        [Export] public NodePath ToolActionPath { get; set; } = new("");
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public NodePath StatusLabelPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool AutoRefresh { get; set; } = true;
        [Export] public bool ShowHoverCell { get; set; } = true;
        [Export] public bool ShowFeedback { get; set; } = true;
        [Export] public Vector2 PanelMinimumSize { get; set; } = new(380, 34);

        private GridInteractionModeComponent? _interaction;
        private GridSelectionComponent? _selection;
        private GridToolActionComponent? _tools;
        private GridPlacementComponent? _placement;
        private Label? _label;
        private string _lastFeedback = "";
        private bool _connected;

        public override void _Ready()
        {
            ResolveReferences();
            ConnectSignals();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildStatus));

            SetProcess(AutoRefresh || Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectSignals();
        }

        public override void _Process(double delta)
        {
            if (AutoRefresh || Engine.IsEditorHint())
                RefreshStatus();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (InteractionModePath.IsEmpty)
                return new[] { "InteractionModePath should point to a GridInteractionModeComponent." };
            if (!GenerateControlsWhenPathsEmpty && FindStatusLabel() == null)
                return new[] { "Set StatusLabelPath, add a scene-authored Label named Status, or enable GenerateControlsWhenPathsEmpty." };
            return Array.Empty<string>();
        }

        public void RebuildStatus()
        {
            ResolveReferences();
            if (BindExistingControls())
            {
                RefreshStatus();
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            ClearChildren();

            var panel = new PanelContainer
            {
                Name = "GeneratedInteractionStatus",
                CustomMinimumSize = PanelMinimumSize,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            AddChild(panel);
            SetEditedOwner(panel);

            _label = new Label
            {
                Name = "Status",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
            };
            KitChrome.SetColorOverrideIfChanged(_label, "font_color", new Color(0.94f, 0.96f, 0.98f));
            panel.AddChild(_label);
            SetEditedOwner(_label);

            RefreshStatus();
        }

        public void RefreshStatus()
        {
            ResolveReferences();
            if (_label != null)
                _label.Text = StatusText();
        }

        public string StatusText()
        {
            ResolveReferences();
            string mode = _interaction?.CurrentMode.ToString() ?? "No Mode";
            string detail = DetailText();
            string cell = ShowHoverCell ? CellText() : "";
            string feedback = ShowFeedback && !string.IsNullOrWhiteSpace(_lastFeedback) ? $" | {_lastFeedback}" : "";

            string text = mode;
            if (!string.IsNullOrWhiteSpace(detail))
                text += $" | {detail}";
            if (!string.IsNullOrWhiteSpace(cell))
                text += $" | {cell}";
            return text + feedback;
        }

        public string LastFeedback
        {
            get => _lastFeedback;
            set
            {
                _lastFeedback = value ?? "";
                RefreshStatus();
            }
        }

        private string DetailText()
        {
            if (_interaction == null)
                return "";

            if (_interaction.CurrentMode == GridInteractionModeComponent.InteractionMode.Tool && _tools != null)
                return _tools.CurrentAction.ToString();

            if (_interaction.CurrentMode == GridInteractionModeComponent.InteractionMode.Build && _placement != null)
                return string.IsNullOrWhiteSpace(_placement.PlacementId) ? "Placement" : _placement.PlacementId;

            return "";
        }

        private string CellText()
        {
            Vector2I cell = _placement != null && _placement.State == GridPlacementComponent.PlacementState.Placing
                ? _placement.CurrentCell
                : _selection?.HoverCell ?? new Vector2I(int.MinValue, int.MinValue);

            if (cell.X == int.MinValue || cell.Y == int.MinValue)
                return "";

            if (_placement != null && _placement.State == GridPlacementComponent.PlacementState.Placing)
                return $"Cell {cell.X},{cell.Y} {(_placement.CurrentCellValid ? "ok" : "blocked")}";

            return $"Cell {cell.X},{cell.Y}";
        }

        private void ResolveReferences()
        {
            if (_interaction == null || !GodotObject.IsInstanceValid(_interaction))
                _interaction = !InteractionModePath.IsEmpty
                    ? GetNodeOrNull<GridInteractionModeComponent>(InteractionModePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridInteractionModeComponent>(GetTree()?.CurrentScene) : null;

            if (_selection == null || !GodotObject.IsInstanceValid(_selection))
                _selection = !SelectionPath.IsEmpty
                    ? GetNodeOrNull<GridSelectionComponent>(SelectionPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridSelectionComponent>(GetTree()?.CurrentScene) : null;

            if (_tools == null || !GodotObject.IsInstanceValid(_tools))
                _tools = !ToolActionPath.IsEmpty
                    ? GetNodeOrNull<GridToolActionComponent>(ToolActionPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridToolActionComponent>(GetTree()?.CurrentScene) : null;

            if (_placement == null || !GodotObject.IsInstanceValid(_placement))
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;
        }

        public bool UsesSceneControls()
            => !StatusLabelPath.IsEmpty || FindStatusLabel() != null;

        private bool BindExistingControls()
        {
            if (!UsesSceneControls())
                return false;

            Label? label = FindStatusLabel();
            if (label == null)
                return false;

            _label = label;
            return true;
        }

        private Label? FindStatusLabel()
        {
            if (!StatusLabelPath.IsEmpty && GetNodeOrNull<Label>(StatusLabelPath) is { } pathLabel)
                return pathLabel;

            if (FindChild("Status", recursive: true, owned: false) is Label childLabel)
                return childLabel;

            return GetParent()?.FindChild("Status", recursive: true, owned: false) as Label;
        }

        private void ConnectSignals()
        {
            if (_connected || Engine.IsEditorHint())
                return;

            ResolveReferences();
            if (_interaction != null)
            {
                _interaction.ModeChanged += OnModeChanged;
                _interaction.InteractionApplied += OnInteractionApplied;
                _interaction.InteractionRejected += OnInteractionRejected;
            }
            if (_selection != null)
                _selection.HoverCellChanged += OnHoverCellChanged;
            if (_tools != null)
            {
                _tools.ToolApplied += OnToolApplied;
                _tools.ToolRejected += OnToolRejected;
            }
            if (_placement != null)
            {
                _placement.PlacementStarted += OnPlacementStarted;
                _placement.PlacementMoved += OnPlacementMoved;
                _placement.PlacementPlaced += OnPlacementPlaced;
                _placement.PlacementCancelled += OnPlacementCancelled;
                _placement.PlacementRejected += OnPlacementRejected;
            }

            _connected = true;
        }

        private void DisconnectSignals()
        {
            if (!_connected)
                return;

            if (_interaction != null && GodotObject.IsInstanceValid(_interaction))
            {
                _interaction.ModeChanged -= OnModeChanged;
                _interaction.InteractionApplied -= OnInteractionApplied;
                _interaction.InteractionRejected -= OnInteractionRejected;
            }
            if (_selection != null && GodotObject.IsInstanceValid(_selection))
                _selection.HoverCellChanged -= OnHoverCellChanged;
            if (_tools != null && GodotObject.IsInstanceValid(_tools))
            {
                _tools.ToolApplied -= OnToolApplied;
                _tools.ToolRejected -= OnToolRejected;
            }
            if (_placement != null && GodotObject.IsInstanceValid(_placement))
            {
                _placement.PlacementStarted -= OnPlacementStarted;
                _placement.PlacementMoved -= OnPlacementMoved;
                _placement.PlacementPlaced -= OnPlacementPlaced;
                _placement.PlacementCancelled -= OnPlacementCancelled;
                _placement.PlacementRejected -= OnPlacementRejected;
            }

            _connected = false;
        }

        private void OnModeChanged(int mode)
        {
            _lastFeedback = "";
            RefreshStatus();
        }

        private void OnInteractionApplied(string mode, int x, int y)
        {
            _lastFeedback = $"{mode} applied";
            RefreshStatus();
        }

        private void OnInteractionRejected(string mode, int x, int y, string reason)
        {
            _lastFeedback = $"{mode} rejected: {reason}";
            RefreshStatus();
        }

        private void OnHoverCellChanged(int x, int y) => RefreshStatus();
        private void OnToolApplied(string action, int x, int y) => SetToolFeedback(action, x, y, "applied");
        private void OnToolRejected(string action, int x, int y, string reason) => SetToolFeedback(action, x, y, reason);
        private void OnPlacementStarted(string id) => SetPlacementFeedback(id, "started");
        private void OnPlacementMoved(string id, int x, int y, bool valid) => SetPlacementFeedback(id, valid ? "valid" : "blocked");
        private void OnPlacementPlaced(string id, Node2D placed, int x, int y) => SetPlacementFeedback(id, "placed");
        private void OnPlacementCancelled(string id) => SetPlacementFeedback(id, "cancelled");
        private void OnPlacementRejected(string id, int x, int y, string reason) => SetPlacementFeedback(id, reason);

        private void SetToolFeedback(string action, int x, int y, string result)
        {
            _lastFeedback = $"{action} {result}";
            RefreshStatus();
        }

        private void SetPlacementFeedback(string id, string result)
        {
            _lastFeedback = $"{(string.IsNullOrWhiteSpace(id) ? "Placement" : id)} {result}";
            RefreshStatus();
        }

        private void ClearChildren()
        {
            foreach (Node child in GetChildren())
                child.QueueFree();
            _label = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}
