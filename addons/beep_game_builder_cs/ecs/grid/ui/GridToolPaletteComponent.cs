using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// HUD palette for GridToolActionComponent. It creates tool buttons for common
    /// farming/settler actions and keeps the selected button in sync with the tool.
    /// The toggle-bar mechanics live in <see cref="GridToggleBarComponent"/>; this
    /// class supplies the actions, the selection, the optional Apply button, and the
    /// ToolAction/InteractionMode wiring.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridToolPaletteComponent : GridToggleBarComponent
    {
        [Signal] public delegate void ToolSelectedEventHandler(string action);
        [Signal] public delegate void ToolApplyRequestedEventHandler(string action, int appliedCount);

        [Export] public NodePath ToolActionPath { get; set; } = new("");
        [Export] public NodePath InteractionModePath { get; set; } = new("");
        [Export] public string[] BoundActionNames { get; set; } = Array.Empty<string>();
        [Export] public NodePath[] BoundButtonPaths { get; set; } = Array.Empty<NodePath>();
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

        protected override string ButtonNamePrefix => "Tool";
        protected override string GeneratedRowName => "GeneratedToolPalette";
        protected override string[] BoundOptionNames => BoundActionNames;
        protected override NodePath[] BoundOptionButtonPaths => BoundButtonPaths;
        protected override Vector2 OptionButtonMinimumSize => ButtonMinimumSize;

        protected override IReadOnlyList<ToggleOption> VisibleOptions
        {
            get
            {
                var options = new List<ToggleOption>();
                foreach (GridToolActionComponent.ToolAction action in VisibleActions())
                    options.Add(OptionFor(action));
                return options;
            }
        }

        protected override bool TryResolveName(string authored, out ToggleOption option)
        {
            if (GridEnumNames.TryParse(authored, out GridToolActionComponent.ToolAction action))
            {
                option = OptionFor(action);
                return true;
            }
            option = default;
            return false;
        }

        protected override string CurrentName => _tools?.CurrentAction.ToString() ?? "";

        protected override bool SelectByName(string name)
        {
            ResolveReferences();
            if (_tools == null || !GridEnumNames.TryParse(name, out GridToolActionComponent.ToolAction action))
                return false;

            _tools.CurrentAction = action;
            if (AutoSwitchInteractionMode && _interactionMode != null)
                _interactionMode.ToolMode();
            RefreshSelection();
            EmitSignal(SignalName.ToolSelected, action.ToString());
            return true;
        }

        protected override void ResolveReferences()
        {
            EntityComponent.Resolve(this, ToolActionPath, ref _tools);
            EntityComponent.Resolve(this, InteractionModePath, ref _interactionMode);
        }

        protected override void OnRowGenerated(HBoxContainer row)
        {
            if (!IncludeApplyButton)
                return;

            var apply = new Button
            {
                Name = "ApplyTool",
                Text = "Apply",
                CustomMinimumSize = ButtonMinimumSize
            };
            BindExtraButton(apply, () => ApplySelectedTool());
            row.AddChild(apply);
            SetEditedOwner(apply);
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

        /// <summary>The same rebuild as the base, under the palette's historical name (scenes call it).</summary>
        public void RebuildPalette() => RebuildBar();

        /// <summary>Select the tool as if its button was pressed.</summary>
        public bool SelectTool(GridToolActionComponent.ToolAction action) => SelectByName(action.ToString());

        /// <summary>Apply the current tool and report how many cells it affected.</summary>
        public int ApplySelectedTool()
        {
            ResolveReferences();
            if (_tools == null)
                return 0;

            int applied = _tools.ApplyCurrent();
            EmitSignal(SignalName.ToolApplyRequested, _tools.CurrentAction.ToString(), applied);
            return applied;
        }

        /// <summary>The current action's name, or "" when no tool component is wired.</summary>
        public string SelectedActionName() => CurrentName;

        /// <summary>How many tool buttons the palette drives.</summary>
        public int VisibleToolButtonCount() => VisibleButtonCount();

        private ToggleOption OptionFor(GridToolActionComponent.ToolAction action)
            => new(action.ToString(), LabelFor(action), action.ToString());

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
    }
}
