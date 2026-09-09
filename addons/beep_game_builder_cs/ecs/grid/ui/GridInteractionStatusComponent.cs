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
    public partial class GridInteractionStatusComponent : GridPanelComponent
    {
        [Export] public NodePath InteractionModePath { get; set; } = new("");
        [Export] public NodePath SelectionPath { get; set; } = new("");
        [Export] public NodePath ToolActionPath { get; set; } = new("");
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public NodePath StatusLabelPath { get; set; } = new("");
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
        private GridInteractionModeComponent? _connectedInteraction;
        private GridSelectionComponent? _connectedSelection;
        private GridToolActionComponent? _connectedTools;
        private GridPlacementComponent? _connectedPlacement;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildStatus));

            // At runtime every change this readout shows arrives through a
            // signal (mode, hover, tool, placement), so per-frame polling is
            // editor preview only. AutoRefresh keeps gating that preview.
            SetProcess(Engine.IsEditorHint() && AutoRefresh);
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectSignals();
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint())
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
            EntityComponent.Resolve(this, InteractionModePath, ref _interaction);
            EntityComponent.Resolve(this, SelectionPath, ref _selection);
            EntityComponent.Resolve(this, ToolActionPath, ref _tools);
            EntityComponent.Resolve(this, PlacementPath, ref _placement);
            SyncSignals();
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

        private Label? FindStatusLabel() => FindControl<Label>(StatusLabelPath, "Status");

        // Connect each source when it resolves to a NEW instance - including one found
        // after _Ready, when an empty path is filled by a scene-wide search once a level
        // loads - and disconnect the previous instance. Idempotent per source, so calling
        // it from every ResolveReferences never double-subscribes. The old once-only
        // _connected flag left a late-resolved source permanently unwired.
        private void SyncSignals()
        {
            if (Engine.IsEditorHint())
                return;

            if (!ReferenceEquals(_connectedInteraction, _interaction))
            {
                if (GodotObject.IsInstanceValid(_connectedInteraction))
                {
                    _connectedInteraction!.ModeChanged -= OnModeChanged;
                    _connectedInteraction.InteractionApplied -= OnInteractionApplied;
                    _connectedInteraction.InteractionRejected -= OnInteractionRejected;
                }
                _connectedInteraction = _interaction;
                if (_interaction != null)
                {
                    _interaction.ModeChanged += OnModeChanged;
                    _interaction.InteractionApplied += OnInteractionApplied;
                    _interaction.InteractionRejected += OnInteractionRejected;
                }
            }

            if (!ReferenceEquals(_connectedSelection, _selection))
            {
                if (GodotObject.IsInstanceValid(_connectedSelection))
                    _connectedSelection!.HoverCellChanged -= OnHoverCellChanged;
                _connectedSelection = _selection;
                if (_selection != null)
                    _selection.HoverCellChanged += OnHoverCellChanged;
            }

            if (!ReferenceEquals(_connectedTools, _tools))
            {
                if (GodotObject.IsInstanceValid(_connectedTools))
                {
                    _connectedTools!.ToolApplied -= OnToolApplied;
                    _connectedTools.ToolRejected -= OnToolRejected;
                }
                _connectedTools = _tools;
                if (_tools != null)
                {
                    _tools.ToolApplied += OnToolApplied;
                    _tools.ToolRejected += OnToolRejected;
                }
            }

            if (!ReferenceEquals(_connectedPlacement, _placement))
            {
                if (GodotObject.IsInstanceValid(_connectedPlacement))
                {
                    _connectedPlacement!.PlacementStarted -= OnPlacementStarted;
                    _connectedPlacement.PlacementMoved -= OnPlacementMoved;
                    _connectedPlacement.PlacementPlaced -= OnPlacementPlaced;
                    _connectedPlacement.PlacementCancelled -= OnPlacementCancelled;
                    _connectedPlacement.PlacementRejected -= OnPlacementRejected;
                }
                _connectedPlacement = _placement;
                if (_placement != null)
                {
                    _placement.PlacementStarted += OnPlacementStarted;
                    _placement.PlacementMoved += OnPlacementMoved;
                    _placement.PlacementPlaced += OnPlacementPlaced;
                    _placement.PlacementCancelled += OnPlacementCancelled;
                    _placement.PlacementRejected += OnPlacementRejected;
                }
            }
        }

        private void DisconnectSignals()
        {
            if (GodotObject.IsInstanceValid(_connectedInteraction))
            {
                _connectedInteraction!.ModeChanged -= OnModeChanged;
                _connectedInteraction.InteractionApplied -= OnInteractionApplied;
                _connectedInteraction.InteractionRejected -= OnInteractionRejected;
            }
            _connectedInteraction = null;

            if (GodotObject.IsInstanceValid(_connectedSelection))
                _connectedSelection!.HoverCellChanged -= OnHoverCellChanged;
            _connectedSelection = null;

            if (GodotObject.IsInstanceValid(_connectedTools))
            {
                _connectedTools!.ToolApplied -= OnToolApplied;
                _connectedTools.ToolRejected -= OnToolRejected;
            }
            _connectedTools = null;

            if (GodotObject.IsInstanceValid(_connectedPlacement))
            {
                _connectedPlacement!.PlacementStarted -= OnPlacementStarted;
                _connectedPlacement.PlacementMoved -= OnPlacementMoved;
                _connectedPlacement.PlacementPlaced -= OnPlacementPlaced;
                _connectedPlacement.PlacementCancelled -= OnPlacementCancelled;
                _connectedPlacement.PlacementRejected -= OnPlacementRejected;
            }
            _connectedPlacement = null;
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

    }
}
