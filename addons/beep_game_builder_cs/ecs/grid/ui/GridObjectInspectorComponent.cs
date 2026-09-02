using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD inspector for the currently selected GridObjectComponent.
    /// It bridges grid selection to normal Godot UI so games can show selected
    /// buildings, props, resource nodes, machines, and units without custom glue.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridObjectInspectorComponent : Control
    {
        [Signal] public delegate void ObjectInspectedEventHandler(string objectId, int x, int y);
        [Signal] public delegate void InspectorClearedEventHandler();

        [Export] public NodePath SelectionPath { get; set; } = new("");
        [Export] public NodePath ObjectsRootPath { get; set; } = new("");
        [Export] public NodePath PanelPath { get; set; } = new("");
        [Export] public NodePath TitleLabelPath { get; set; } = new("");
        [Export] public NodePath DetailsLabelPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool HideWhenEmpty { get; set; } = false;
        [Export] public bool ShowCategory { get; set; } = true;
        [Export] public bool ShowCell { get; set; } = true;
        [Export] public bool ShowFootprint { get; set; } = true;
        [Export] public bool ShowCompletion { get; set; } = true;
        [Export] public bool ShowMetadata { get; set; } = true;
        [Export] public string EmptyText { get; set; } = "No object selected";
        [Export] public Vector2 PanelMinimumSize { get; set; } = new(220, 86);

        public GridObjectComponent? SelectedObject { get; private set; }
        public string InspectedObjectId => SelectedObject?.ObjectId ?? "";

        private GridSelectionComponent? _selection;
        private PanelContainer? _panel;
        private Label? _title;
        private Label? _details;
        private bool _createdPanel;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildInspector));

            if (!Engine.IsEditorHint() && _selection != null)
                _selection.SelectionChanged += OnSelectionChanged;

            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (_selection != null && GodotObject.IsInstanceValid(_selection))
                _selection.SelectionChanged -= OnSelectionChanged;

            if (_createdPanel && _panel != null && GodotObject.IsInstanceValid(_panel))
                _panel.QueueFree();
            _panel = null;
            _title = null;
            _details = null;
            _createdPanel = false;
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (SelectionPath.IsEmpty)
                return new[] { "SelectionPath should point to a GridSelectionComponent." };
            if (!GenerateControlsWhenPathsEmpty && !HasAuthoredControls())
                return new[] { "Set TitleLabelPath and DetailsLabelPath to authored labels, add Panel/Content/Title and Panel/Content/Details children, or enable GenerateControlsWhenPathsEmpty." };
            return Array.Empty<string>();
        }

        public void RebuildInspector()
        {
            ResolveReferences();
            EnsureUi();

            SelectedObject = FindSelectedObject();
            if (SelectedObject == null)
            {
                if (_title != null)
                    _title.Text = EmptyText;
                if (_details != null)
                    _details.Text = "";
                Visible = !HideWhenEmpty;
                EmitSignal(SignalName.InspectorCleared);
                return;
            }

            Visible = true;
            if (_title != null)
                _title.Text = TitleForObject(SelectedObject);
            if (_details != null)
                _details.Text = TextForObject(SelectedObject);

            EmitSignal(SignalName.ObjectInspected, SelectedObject.ObjectId, SelectedObject.Cell.X, SelectedObject.Cell.Y);
        }

        public void SetSelectedObject(GridObjectComponent? gridObject)
        {
            SelectedObject = gridObject != null && GodotObject.IsInstanceValid(gridObject) ? gridObject : null;
            EnsureUi();

            if (SelectedObject == null)
            {
                if (_title != null)
                    _title.Text = EmptyText;
                if (_details != null)
                    _details.Text = "";
                Visible = !HideWhenEmpty;
                EmitSignal(SignalName.InspectorCleared);
                return;
            }

            Visible = true;
            if (_title != null)
                _title.Text = TitleForObject(SelectedObject);
            if (_details != null)
                _details.Text = TextForObject(SelectedObject);
            EmitSignal(SignalName.ObjectInspected, SelectedObject.ObjectId, SelectedObject.Cell.X, SelectedObject.Cell.Y);
        }

        public string TextForObject(GridObjectComponent gridObject)
        {
            var lines = new List<string>();

            if (ShowCategory && !string.IsNullOrWhiteSpace(gridObject.EffectiveCategory))
                lines.Add($"Type: {gridObject.EffectiveCategory}");
            if (!string.IsNullOrWhiteSpace(gridObject.Description))
                lines.Add(gridObject.Description.Trim());
            if (ShowCell)
                lines.Add($"Cell: {gridObject.Cell.X}, {gridObject.Cell.Y}");
            if (ShowFootprint)
                lines.Add($"Size: {gridObject.Footprint.X} x {gridObject.Footprint.Y}");
            if (ShowCompletion)
                lines.Add(gridObject.Complete ? "Ready" : "Under construction");

            if (ShowMetadata)
            {
                foreach (Variant key in gridObject.Metadata.Keys)
                {
                    string name = key.AsString();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    lines.Add($"{name}: {gridObject.Metadata[key]}");
                }
            }

            return string.Join("\n", lines);
        }

        public int VisibleLineCount()
            => _details == null || string.IsNullOrWhiteSpace(_details.Text)
                ? 0
                : _details.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        private void OnSelectionChanged(int count) => RebuildInspector();

        private GridObjectComponent? FindSelectedObject()
        {
            if (_selection == null)
                return SelectedObject != null && GodotObject.IsInstanceValid(SelectedObject) ? SelectedObject : null;

            var cells = _selection.GetSelectedCells();
            if (cells.Count == 0)
                return null;

            foreach (GridObjectComponent gridObject in CandidateObjects())
            {
                if (!gridObject.Selectable)
                    continue;

                foreach (Vector2I cell in cells)
                    if (CoversCell(gridObject, cell))
                        return gridObject;
            }

            return null;
        }

        private List<GridObjectComponent> CandidateObjects()
        {
            var objects = new List<GridObjectComponent>();
            Node? root = ResolveObjectsRoot();

            if (IsInsideTree())
            {
                foreach (Node node in GetTree().GetNodesInGroup(GridObjectComponent.ComponentGroupName))
                {
                    if (node is not GridObjectComponent gridObject)
                        continue;
                    if (root != null && !IsNodeWithin(gridObject, root))
                        continue;
                    objects.Add(gridObject);
                }
            }

            if (objects.Count == 0 && root != null)
                CollectObjects(root, objects);

            return objects;
        }

        private Node? ResolveObjectsRoot()
        {
            if (!ObjectsRootPath.IsEmpty)
                return GetNodeOrNull<Node>(ObjectsRootPath);
            return IsInsideTree() ? GetTree()?.CurrentScene : null;
        }

        private void ResolveReferences()
        {
            if (_selection != null && GodotObject.IsInstanceValid(_selection))
                return;

            if (!SelectionPath.IsEmpty)
                _selection = GetNodeOrNull<GridSelectionComponent>(SelectionPath);
            else if (IsInsideTree())
                _selection = EntityComponent.FindComponent<GridSelectionComponent>(GetTree()?.CurrentScene);
        }

        private void EnsureUi()
        {
            if (_panel != null && GodotObject.IsInstanceValid(_panel))
                return;

            if (BindExistingControls())
            {
                StyleControls();
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            BuildGeneratedControls();
        }

        private void BuildGeneratedControls()
        {
            foreach (Node child in GetChildren())
                child.QueueFree();

            _createdPanel = true;
            _panel = new PanelContainer
            {
                Name = "GeneratedObjectInspector",
                CustomMinimumSize = PanelMinimumSize,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            AddChild(_panel);
            SetEditedOwner(_panel);

            var layout = new VBoxContainer
            {
                Name = "Content",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(layout, "separation", 4);
            _panel.AddChild(layout);
            SetEditedOwner(layout);

            _title = new Label
            {
                Name = "Title",
                Text = EmptyText,
                HorizontalAlignment = HorizontalAlignment.Left,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
            };
            KitChrome.SetColorOverrideIfChanged(_title, "font_color", Colors.White);
            layout.AddChild(_title);
            SetEditedOwner(_title);

            _details = new Label
            {
                Name = "Details",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            KitChrome.SetColorOverrideIfChanged(_details, "font_color", new Color(0.86f, 0.89f, 0.92f));
            layout.AddChild(_details);
            SetEditedOwner(_details);
        }

        private bool BindExistingControls()
        {
            _createdPanel = false;
            _panel = FindPanel();
            _title = FindTitleLabel();
            _details = FindDetailsLabel();

            return _title != null && _details != null;
        }

        public bool UsesSceneControls()
            => HasAuthoredControls();

        private bool HasAuthoredControls()
            => FindTitleLabel() != null && FindDetailsLabel() != null;

        private PanelContainer? FindPanel()
        {
            if (!PanelPath.IsEmpty && GetNodeOrNull<PanelContainer>(PanelPath) is { } pathPanel)
                return pathPanel;

            if (FindChild("Panel", recursive: true, owned: false) is PanelContainer childPanel)
                return childPanel;

            return GetParent()?.FindChild("Panel", recursive: true, owned: false) as PanelContainer;
        }

        private Label? FindTitleLabel()
        {
            if (!TitleLabelPath.IsEmpty && GetNodeOrNull<Label>(TitleLabelPath) is { } pathLabel)
                return pathLabel;

            if (GetNodeOrNull<Label>("Panel/Content/Title") is { } localLabel)
                return localLabel;

            if (FindChild("Title", recursive: true, owned: false) is Label childLabel)
                return childLabel;

            return GetParent()?.FindChild("Title", recursive: true, owned: false) as Label;
        }

        private Label? FindDetailsLabel()
        {
            if (!DetailsLabelPath.IsEmpty && GetNodeOrNull<Label>(DetailsLabelPath) is { } pathLabel)
                return pathLabel;

            if (GetNodeOrNull<Label>("Panel/Content/Details") is { } localLabel)
                return localLabel;

            if (FindChild("Details", recursive: true, owned: false) is Label childLabel)
                return childLabel;

            return GetParent()?.FindChild("Details", recursive: true, owned: false) as Label;
        }

        private void StyleControls()
        {
            if (_panel != null)
            {
                _panel.CustomMinimumSize = PanelMinimumSize;
                _panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            }

            if (_title != null)
            {
                _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                KitChrome.SetColorOverrideIfChanged(_title, "font_color", Colors.White);
            }

            if (_details != null)
            {
                _details.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _details.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                KitChrome.SetColorOverrideIfChanged(_details, "font_color", new Color(0.86f, 0.89f, 0.92f));
            }
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }

        private static string TitleForObject(GridObjectComponent gridObject)
        {
            if (!string.IsNullOrWhiteSpace(gridObject.DisplayName))
                return gridObject.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(gridObject.ObjectId))
                return gridObject.ObjectId.Trim();
            return "Grid Object";
        }

        private static bool CoversCell(GridObjectComponent gridObject, Vector2I cell)
        {
            Vector2I max = gridObject.Cell + new Vector2I(Mathf.Max(1, gridObject.Footprint.X), Mathf.Max(1, gridObject.Footprint.Y));
            return cell.X >= gridObject.Cell.X && cell.Y >= gridObject.Cell.Y && cell.X < max.X && cell.Y < max.Y;
        }

        private static bool IsNodeWithin(Node node, Node root)
        {
            Node? current = node;
            while (current != null)
            {
                if (current == root)
                    return true;
                current = current.GetParent();
            }
            return false;
        }

        private static void CollectObjects(Node node, List<GridObjectComponent> objects)
        {
            if (node is GridObjectComponent gridObject)
                objects.Add(gridObject);

            foreach (Node child in node.GetChildren())
                CollectObjects(child, objects);
        }
    }
}
