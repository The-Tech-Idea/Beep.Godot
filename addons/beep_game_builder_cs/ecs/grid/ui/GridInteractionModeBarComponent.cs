using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// HUD button bar for GridInteractionModeComponent. It lets players switch
    /// between select, inspect, tool, build, and disabled map interaction modes.
    /// The toggle-bar mechanics live in <see cref="GridToggleBarComponent"/>; this
    /// class supplies the modes, the selection, and the InteractionMode wiring.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridInteractionModeBarComponent : GridToggleBarComponent
    {
        [Signal] public delegate void ModeButtonPressedEventHandler(int mode);

        [Export] public NodePath InteractionModePath { get; set; } = new("");
        [Export] public string[] BoundModeNames { get; set; } = Array.Empty<string>();
        [Export] public NodePath[] BoundButtonPaths { get; set; } = Array.Empty<NodePath>();
        [Export] public bool ShowSelect { get; set; } = true;
        [Export] public bool ShowInspect { get; set; } = true;
        [Export] public bool ShowTool { get; set; } = true;
        [Export] public bool ShowBuild { get; set; } = true;
        [Export] public bool ShowDisabled { get; set; } = false;
        [Export] public Vector2 ButtonMinimumSize { get; set; } = new(88, 34);

        private GridInteractionModeComponent? _interaction;

        protected override string ButtonNamePrefix => "Mode";
        protected override string GeneratedRowName => "GeneratedInteractionModeBar";
        protected override string[] BoundOptionNames => BoundModeNames;
        protected override NodePath[] BoundOptionButtonPaths => BoundButtonPaths;
        protected override Vector2 OptionButtonMinimumSize => ButtonMinimumSize;

        protected override IReadOnlyList<ToggleOption> VisibleOptions
        {
            get
            {
                var options = new List<ToggleOption>();
                foreach (GridInteractionModeComponent.InteractionMode mode in VisibleModes())
                    options.Add(OptionFor(mode));
                return options;
            }
        }

        protected override bool TryResolveName(string authored, out ToggleOption option)
        {
            if (GridEnumNames.TryParse(authored, out GridInteractionModeComponent.InteractionMode mode))
            {
                option = OptionFor(mode);
                return true;
            }
            option = default;
            return false;
        }

        protected override string CurrentName => _interaction?.CurrentMode.ToString() ?? "";

        protected override bool SelectByName(string name)
        {
            ResolveReferences();
            if (_interaction == null || !GridEnumNames.TryParse(name, out GridInteractionModeComponent.InteractionMode mode))
                return false;

            _interaction.SetMode(mode);
            RefreshSelection();
            EmitSignal(SignalName.ModeButtonPressed, (int)mode);
            return true;
        }

        protected override void ResolveReferences()
        {
            // The guard stays so signals are (re)connected only when Resolve
            // actually picks a new instance up, not on every call.
            if (_interaction == null || !GodotObject.IsInstanceValid(_interaction))
            {
                EntityComponent.Resolve(this, InteractionModePath, ref _interaction);
                ConnectInteractionSignals();
            }
        }

        protected override void OnExitTree() => DisconnectInteractionSignals();

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

        /// <summary>Set the interaction mode as if its button was pressed.</summary>
        public bool SelectMode(GridInteractionModeComponent.InteractionMode mode) => SelectByName(mode.ToString());

        /// <summary>The current mode's name, or "" when no interaction component is wired.</summary>
        public string SelectedModeName() => CurrentName;

        /// <summary>How many mode buttons the bar drives.</summary>
        public int VisibleModeButtonCount() => VisibleButtonCount();

        private ToggleOption OptionFor(GridInteractionModeComponent.InteractionMode mode)
            => new(mode.ToString(), LabelFor(mode), TooltipFor(mode));

        private IEnumerable<GridInteractionModeComponent.InteractionMode> VisibleModes()
        {
            if (ShowSelect) yield return GridInteractionModeComponent.InteractionMode.Select;
            if (ShowInspect) yield return GridInteractionModeComponent.InteractionMode.Inspect;
            if (ShowTool) yield return GridInteractionModeComponent.InteractionMode.Tool;
            if (ShowBuild) yield return GridInteractionModeComponent.InteractionMode.Build;
            if (ShowDisabled) yield return GridInteractionModeComponent.InteractionMode.Disabled;
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

        private void OnModeChanged(int mode) => RefreshSelection();

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
