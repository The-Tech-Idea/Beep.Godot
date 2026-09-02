using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD strip for GridResourceWalletComponent. It displays every
    /// non-zero wallet entry as a resource label and refreshes on wallet changes.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridResourceBarComponent : Control
    {
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public NodePath RowPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool HideZeroAmounts { get; set; } = true;
        [Export] public bool SortByResourceId { get; set; } = true;
        [Export] public Vector2 BadgeMinimumSize { get; set; } = new(96, 32);
        [Export] public string[] BoundResourceIds { get; set; } = Array.Empty<string>();
        [Export] public NodePath[] BoundLabelPaths { get; set; } = Array.Empty<NodePath>();

        private GridResourceWalletComponent? _wallet;
        private HBoxContainer? _row;
        private readonly Dictionary<string, Label> _boundLabels = new(StringComparer.OrdinalIgnoreCase);
        private bool _createdRow;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildBar));

            if (!Engine.IsEditorHint() && _wallet != null)
                _wallet.ResourcesChanged += RebuildBar;

            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (_wallet != null && GodotObject.IsInstanceValid(_wallet))
                _wallet.ResourcesChanged -= RebuildBar;

            if (_createdRow && _row != null && GodotObject.IsInstanceValid(_row))
                _row.QueueFree();
            _row = null;
            _createdRow = false;
            _boundLabels.Clear();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (ResourceWalletPath.IsEmpty)
                return new[] { "ResourceWalletPath should point to a GridResourceWalletComponent." };
            if (BoundResourceIds.Length != BoundLabelPaths.Length)
                return new[] { "BoundResourceIds and BoundLabelPaths should have the same length." };
            if (!GenerateControlsWhenPathsEmpty && BoundResourceIds.Length == 0 && FindResourceRow() == null)
                return new[] { "Add an authored HBoxContainer named ResourceBar, set BoundResourceIds/BoundLabelPaths, set RowPath, or enable GenerateControlsWhenPathsEmpty." };
            return Array.Empty<string>();
        }

        public void RebuildBar()
        {
            ResolveReferences();
            if (BindExistingLabels())
            {
                RefreshBoundLabels();
                return;
            }

            if (!BindExistingRow())
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                BuildGeneratedRow();
            }

            RefreshRowLabels();
        }

        private void BuildGeneratedRow()
        {
            ClearGeneratedRow();

            _createdRow = true;
            _row = new HBoxContainer
            {
                Name = "GeneratedResourceBar",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(_row, "separation", 6);
            AddChild(_row);
            SetEditedOwner(_row);
        }

        private void RefreshRowLabels()
        {
            if (_wallet == null)
                return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in ResourceEntries())
            {
                if (HideZeroAmounts && entry.Amount <= 0)
                    continue;

                string nodeName = $"Resource_{SafeName(entry.ResourceId)}";
                seen.Add(nodeName);

                Label? label = _row?.GetNodeOrNull<Label>(nodeName);
                if (label == null || !label.HasMeta("beep_generated_resource_label"))
                {
                    label = new Label
                    {
                        Name = nodeName,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    label.SetMeta("beep_generated_resource_label", true);
                    _row?.AddChild(label);
                    SetEditedOwner(label);
                }

                label.Text = FormatEntry(entry.ResourceId, entry.Amount);
                label.CustomMinimumSize = BadgeMinimumSize;
                label.TooltipText = entry.ResourceId;
                label.Visible = true;
                label.SetMeta("beep_generated_resource_label", true);
                KitChrome.SetColorOverrideIfChanged(label, "font_color", Colors.White);
            }

            if (_row == null)
                return;

            foreach (Node child in _row.GetChildren())
            {
                if (child is not Label label || !label.HasMeta("beep_generated_resource_label"))
                    continue;
                if (seen.Contains(label.Name.ToString()))
                    continue;

                label.Visible = false;
                label.QueueFree();
            }
        }

        public int VisibleResourceCount()
        {
            if (_boundLabels.Count > 0)
            {
                int boundCount = 0;
                foreach (Label label in _boundLabels.Values)
                    if (label.Visible)
                        boundCount++;
                return boundCount;
            }

            if (_row == null)
                return 0;

            int count = 0;
            foreach (Node child in _row.GetChildren())
                if (child is Label label && label.Visible)
                    count++;
            return count;
        }

        public string TextForResource(string resourceId)
        {
            if (_boundLabels.TryGetValue(Normalize(resourceId), out Label? boundLabel))
                return boundLabel.Text;

            if (_row == null)
                return "";

            string nodeName = $"Resource_{SafeName(resourceId)}";
            Label? label = _row.GetNodeOrNull<Label>(nodeName);
            return label != null && label.Visible ? label.Text : "";
        }

        private List<(string ResourceId, int Amount)> ResourceEntries()
        {
            var entries = new List<(string ResourceId, int Amount)>();
            if (_wallet == null)
                return entries;

            var amounts = _wallet.GetAmounts();
            foreach (Variant key in amounts.Keys)
            {
                string id = key.AsString();
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                entries.Add((id, GridVariantReader.Int(amounts[key], 0)));
            }

            if (SortByResourceId)
                entries.Sort((a, b) => string.Compare(a.ResourceId, b.ResourceId, StringComparison.OrdinalIgnoreCase));

            return entries;
        }

        private static string FormatEntry(string resourceId, int amount)
        {
            string label = string.IsNullOrWhiteSpace(resourceId) ? "Resource" : resourceId.Trim();
            return $"{label}: {amount}";
        }

        private void ResolveReferences()
        {
            if (_wallet == null || !GodotObject.IsInstanceValid(_wallet))
                _wallet = !ResourceWalletPath.IsEmpty
                    ? GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridResourceWalletComponent>(GetTree()?.CurrentScene) : null;
        }

        private bool BindExistingLabels()
        {
            _boundLabels.Clear();
            _createdRow = false;
            if (BoundResourceIds.Length == 0 && BoundLabelPaths.Length == 0)
                return false;

            if (BoundResourceIds.Length != BoundLabelPaths.Length)
                return false;

            for (int i = 0; i < BoundResourceIds.Length; i++)
            {
                string id = Normalize(BoundResourceIds[i]);
                if (string.IsNullOrEmpty(id))
                    return false;

                Label? label = FindResourceLabel(BoundResourceIds[i], i);
                if (label == null)
                    return false;

                _boundLabels[id] = label;
            }

            return _boundLabels.Count > 0;
        }

        private bool BindExistingRow()
        {
            _createdRow = false;
            _row = FindResourceRow();
            if (_row == null)
                return false;

            KitChrome.SetConstantOverrideIfChanged(_row, "separation", 6);
            return true;
        }

        public bool UsesSceneControls()
            => BoundResourceIds.Length > 0 || FindResourceRow() != null;

        private HBoxContainer? FindResourceRow()
        {
            if (!RowPath.IsEmpty && GetNodeOrNull<HBoxContainer>(RowPath) is { } pathRow)
                return pathRow;

            if (FindChild("ResourceBar", recursive: true, owned: false) is HBoxContainer resourceBar)
                return resourceBar;

            if (FindChild("GeneratedResourceBar", recursive: true, owned: false) is HBoxContainer generatedBar)
                return generatedBar;

            if (GetParent()?.FindChild("ResourceBar", recursive: true, owned: false) is HBoxContainer parentResourceBar)
                return parentResourceBar;

            return GetParent()?.FindChild("GeneratedResourceBar", recursive: true, owned: false) as HBoxContainer;
        }

        private Label? FindResourceLabel(string resourceId, int index)
        {
            if (BoundLabelPaths.Length > index && !BoundLabelPaths[index].IsEmpty
                && GetNodeOrNull<Label>(BoundLabelPaths[index]) is { } pathLabel)
                return pathLabel;

            string nodeName = $"Resource_{SafeName(resourceId)}";
            if (FindChild(nodeName, recursive: true, owned: false) is Label childLabel)
                return childLabel;

            return GetParent()?.FindChild(nodeName, recursive: true, owned: false) as Label;
        }

        private void RefreshBoundLabels()
        {
            foreach ((string resourceId, Label label) in _boundLabels)
            {
                int amount = _wallet?.GetAmount(resourceId) ?? 0;
                label.Text = FormatEntry(resourceId, amount);
                label.TooltipText = resourceId;
                label.Visible = !HideZeroAmounts || amount > 0;
            }
        }

        private void ClearGeneratedRow()
        {
            if (_createdRow && _row != null && GodotObject.IsInstanceValid(_row))
                _row.QueueFree();
            _row = null;
            _createdRow = false;
            _boundLabels.Clear();
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }

        private static string SafeName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Resource" : value.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');
            return result.Replace(' ', '_');
        }

        private static string Normalize(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }
}
