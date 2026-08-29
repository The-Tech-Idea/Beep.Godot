using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Simple build palette for grid builder/farming/settler scenes. It reads
    /// GridBuildCatalogComponent and creates category/build buttons that call
    /// BeginPlacement on the selected build.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridBuildToolbarComponent : Control
    {
        [Signal] public delegate void BuildButtonPressedEventHandler(string buildId);
        [Signal] public delegate void BuildButtonRejectedEventHandler(string buildId, string reason);
        [Signal] public delegate void CategoryChangedEventHandler(string category);

        [Export] public NodePath BuildCatalogPath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public NodePath InteractionModePath { get; set; } = new("");
        [Export] public NodePath CategoryRowPath { get; set; } = new("");
        [Export] public NodePath BuildGridPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool AutoSwitchInteractionMode { get; set; } = true;
        [Export] public bool HideUnaffordable { get; set; } = false;
        [Export] public Vector2 ButtonMinimumSize { get; set; } = new(120, 56);
        [Export] public string CurrentCategory { get; set; } = "";

        public Vector2 EffectiveButtonMinimumSize
            => new(Mathf.Max(1f, float.IsFinite(ButtonMinimumSize.X) ? ButtonMinimumSize.X : 120f),
                   Mathf.Max(1f, float.IsFinite(ButtonMinimumSize.Y) ? ButtonMinimumSize.Y : 56f));

        private GridBuildCatalogComponent? _catalog;
        private GridResourceWalletComponent? _wallet;
        private GridInteractionModeComponent? _interactionMode;
        private HBoxContainer? _categoryRow;
        private GridContainer? _buildGrid;
        private readonly Dictionary<string, Button> _buildButtons = new();
        private readonly List<(Button Button, Action Handler)> _connectedButtons = new();

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildToolbar));

            if (!Engine.IsEditorHint() && _wallet != null)
                _wallet.ResourcesChanged += RefreshAffordability;

            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectButtons();
            if (_wallet != null && GodotObject.IsInstanceValid(_wallet))
                _wallet.ResourcesChanged -= RefreshAffordability;
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (BuildCatalogPath.IsEmpty)
                return new[] { "BuildCatalogPath should point to a GridBuildCatalogComponent." };
            if (!GenerateControlsWhenPathsEmpty && (CategoryRowPath.IsEmpty || BuildGridPath.IsEmpty))
                return new[] { "Set CategoryRowPath and BuildGridPath, or enable GenerateControlsWhenPathsEmpty." };
            return Array.Empty<string>();
        }

        public void RebuildToolbar()
        {
            ResolveReferences();
            DisconnectButtons();
            _buildButtons.Clear();

            if (_catalog == null)
                return;

            bool hasSurface = BindExistingControls();
            if (!hasSurface)
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                ClearChildren();
                BuildGeneratedSurface();
            }

            PopulateToolbar();
        }

        private void BuildGeneratedSurface()
        {
            var root = new VBoxContainer
            {
                Name = "GeneratedBuildToolbar",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(root, "separation", 6);
            AddChild(root);
            SetEditedOwner(root);

            _categoryRow = new HBoxContainer { Name = "Categories" };
            KitChrome.SetConstantOverrideIfChanged(_categoryRow, "separation", 6);
            root.AddChild(_categoryRow);
            SetEditedOwner(_categoryRow);

            var scroll = new ScrollContainer
            {
                Name = "BuildScroll",
                HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
                VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            root.AddChild(scroll);
            SetEditedOwner(scroll);

            _buildGrid = new GridContainer
            {
                Name = "Builds",
                Columns = 8,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(_buildGrid, "h_separation", 6);
            KitChrome.SetConstantOverrideIfChanged(_buildGrid, "v_separation", 6);
            scroll.AddChild(_buildGrid);
            SetEditedOwner(_buildGrid);
        }

        private void PopulateToolbar()
        {
            if (_categoryRow == null || _buildGrid == null || _catalog == null)
                return;

            foreach (Node child in _categoryRow.GetChildren())
                child.QueueFree();
            _buildButtons.Clear();

            var categories = Categories();
            if (categories.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(CurrentCategory) || !categories.Contains(CurrentCategory))
                CurrentCategory = categories[0];

            foreach (string category in categories)
                AddCategoryButton(category);

            BuildButtonsForCategory(CurrentCategory);
            RefreshAffordability();
        }

        public void SelectCategory(string category)
        {
            CurrentCategory = category;
            if (_categoryRow != null)
            {
                foreach (Node child in _categoryRow.GetChildren())
                    if (child is Button button)
                        button.SetPressedNoSignal(button.Text == category);
            }

            BuildButtonsForCategory(category);
            RefreshAffordability();
            EmitSignal(SignalName.CategoryChanged, category);
        }

        public bool SelectBuild(string buildId)
        {
            ResolveReferences();
            if (_catalog == null)
            {
                EmitSignal(SignalName.BuildButtonRejected, buildId, "missing_catalog");
                return false;
            }

            bool ok = _catalog.BeginPlacement(buildId);
            if (ok)
            {
                if (AutoSwitchInteractionMode && _interactionMode != null)
                    _interactionMode.BuildMode();
                EmitSignal(SignalName.BuildButtonPressed, buildId);
            }
            else
                EmitSignal(SignalName.BuildButtonRejected, buildId, "catalog_rejected");

            RefreshAffordability();
            return ok;
        }

        public int VisibleBuildButtonCount() => _buildButtons.Count;

        public void RefreshAffordability()
        {
            ResolveReferences();
            if (_catalog == null)
                return;

            foreach (var pair in _buildButtons)
            {
                bool affordable = _wallet == null || _catalog.CanAfford(pair.Key);
                pair.Value.Disabled = !affordable;
                pair.Value.Modulate = affordable ? Colors.White : new Color(1f, 1f, 1f, 0.45f);
            }
        }

        private void BuildButtonsForCategory(string category)
        {
            if (_buildGrid == null || _catalog == null)
                return;

            foreach (Node child in _buildGrid.GetChildren())
                child.QueueFree();
            _buildButtons.Clear();

            foreach (GridBuildDefinition build in GridBuildDefinition.Enumerate(_catalog.Builds))
            {
                if (build == null || !string.Equals(build.Category, category, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool affordable = _wallet == null || _catalog.CanAfford(build.BuildId);
                if (HideUnaffordable && !affordable)
                    continue;

                string id = build.BuildId;
                var button = new Button
                {
                    Name = $"Build_{SafeName(id)}",
                    Text = ButtonText(build),
                    TooltipText = Tooltip(build),
                    CustomMinimumSize = EffectiveButtonMinimumSize,
                    TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                    Disabled = !affordable
                };
                if (build.PreviewTexture != null)
                    button.Icon = build.PreviewTexture;

                string capturedId = id;
                Action handler = () => SelectBuild(capturedId);
                button.Pressed += handler;
                _connectedButtons.Add((button, handler));
                _buildGrid.AddChild(button);
                SetEditedOwner(button);
                _buildButtons[id] = button;
            }
        }

        private void AddCategoryButton(string category)
        {
            if (_categoryRow == null)
                return;

            var tab = new Button
            {
                Name = $"Category_{SafeName(category)}",
                Text = category,
                ToggleMode = true,
                ButtonPressed = category == CurrentCategory,
                CustomMinimumSize = new Vector2(96, 34)
            };
            string capturedCategory = category;
            Action handler = () => SelectCategory(capturedCategory);
            tab.Pressed += handler;
            _connectedButtons.Add((tab, handler));
            _categoryRow.AddChild(tab);
            SetEditedOwner(tab);
        }

        private List<string> Categories()
        {
            var categories = new List<string>();
            if (_catalog == null)
                return categories;

            foreach (GridBuildDefinition build in GridBuildDefinition.Enumerate(_catalog.Builds))
            {
                if (build == null)
                    continue;

                string category = string.IsNullOrWhiteSpace(build.Category) ? "Build" : build.Category;
                if (!categories.Contains(category))
                    categories.Add(category);
            }

            return categories;
        }

        private string ButtonText(GridBuildDefinition build)
        {
            string label = string.IsNullOrWhiteSpace(build.DisplayName) ? build.BuildId : build.DisplayName;
            string cost = CostText(build);
            return string.IsNullOrEmpty(cost) ? label : $"{label}\n{cost}";
        }

        private string Tooltip(GridBuildDefinition build)
        {
            string label = string.IsNullOrWhiteSpace(build.DisplayName) ? build.BuildId : build.DisplayName;
            string cost = CostText(build);
            Vector2I footprint = build.EffectiveFootprint;
            string size = $"{footprint.X}x{footprint.Y}";
            return string.IsNullOrEmpty(cost)
                ? $"{label}\nFootprint {size}"
                : $"{label}\nCost {cost}\nFootprint {size}";
        }

        private static string CostText(GridBuildDefinition build)
        {
            var parts = new List<string>();
            foreach ((string resourceId, int amount) in GridResourceAmount.Enumerate(build.Costs))
            {
                if (amount <= 0 || string.IsNullOrWhiteSpace(resourceId))
                    continue;
                parts.Add($"{resourceId} {amount}");
            }
            return string.Join(", ", parts);
        }

        private void ResolveReferences()
        {
            if (_catalog == null || !GodotObject.IsInstanceValid(_catalog))
                _catalog = !BuildCatalogPath.IsEmpty
                    ? GetNodeOrNull<GridBuildCatalogComponent>(BuildCatalogPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridBuildCatalogComponent>(GetTree()?.CurrentScene) : null;

            if (_wallet == null || !GodotObject.IsInstanceValid(_wallet))
                _wallet = !ResourceWalletPath.IsEmpty
                    ? GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridResourceWalletComponent>(GetTree()?.CurrentScene) : null;

            if (_interactionMode == null || !GodotObject.IsInstanceValid(_interactionMode))
                _interactionMode = !InteractionModePath.IsEmpty
                    ? GetNodeOrNull<GridInteractionModeComponent>(InteractionModePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridInteractionModeComponent>(GetTree()?.CurrentScene) : null;
        }

        public bool UsesSceneControls()
            => FindCategoryRow() != null || FindBuildGrid() != null;

        private bool BindExistingControls()
        {
            HBoxContainer? categoryRow = FindCategoryRow();
            GridContainer? buildGrid = FindBuildGrid();

            if (categoryRow == null || buildGrid == null)
                return false;

            _categoryRow = categoryRow;
            _buildGrid = buildGrid;
            return true;
        }

        private HBoxContainer? FindCategoryRow()
        {
            if (!CategoryRowPath.IsEmpty && GetNodeOrNull<HBoxContainer>(CategoryRowPath) is { } pathRow)
                return pathRow;

            if (FindChild("Categories", recursive: true, owned: false) is HBoxContainer childRow)
                return childRow;

            return GetParent()?.FindChild("Categories", recursive: true, owned: false) as HBoxContainer;
        }

        private GridContainer? FindBuildGrid()
        {
            if (!BuildGridPath.IsEmpty && GetNodeOrNull<GridContainer>(BuildGridPath) is { } pathGrid)
                return pathGrid;

            if (FindChild("Builds", recursive: true, owned: false) is GridContainer childGrid)
                return childGrid;

            return GetParent()?.FindChild("Builds", recursive: true, owned: false) as GridContainer;
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
            _categoryRow = null;
            _buildGrid = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }

        private static string SafeName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Item" : value.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');
            return result.Replace(' ', '_');
        }
    }
}
