using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>One option a toggle bar can offer: its canonical name (also the button-name suffix and the selection key), its button label, and its tooltip.</summary>
    public readonly struct ToggleOption
    {
        public ToggleOption(string name, string label, string tooltip)
        {
            Name = name;
            Label = label;
            Tooltip = tooltip;
        }

        public string Name { get; }
        public string Label { get; }
        public string Tooltip { get; }
    }

    /// <summary>
    /// The shared mechanics of a HUD toggle bar - a row of mutually-exclusive toggle
    /// buttons over an enum-like option set (map interaction modes, land tools). The
    /// concrete bar supplies its options, the current selection, how to select one, and
    /// how it resolves its collaborators; this base owns binding authored buttons (by
    /// the bar's Bound*Names / paths or the <c>Prefix_Name</c> convention), generating a
    /// fallback row when asked, keeping the pressed button in sync, and the press wiring
    /// (through <see cref="GridButtonBindings"/>).
    ///
    /// <para>Buttons are keyed by canonical option NAME and a bound name is resolved
    /// against EVERY option, so an authored button for an option hidden from the
    /// generated set still binds - a contract keyed by visible index would not.</para>
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class GridToggleBarComponent : GridPanelComponent
    {
        private HBoxContainer? _row;
        private readonly Dictionary<string, Button> _buttons = new();
        private readonly GridButtonBindings _buttonBindings = new();

        // --- the contract each concrete bar fills ---

        /// <summary>The button-name prefix: "Mode" gives "Mode_Build".</summary>
        protected abstract string ButtonNamePrefix { get; }

        /// <summary>The generated fallback row's node name.</summary>
        protected abstract string GeneratedRowName { get; }

        /// <summary>The authored bound option names (the bar's own <c>Bound*Names</c> export).</summary>
        protected abstract string[] BoundOptionNames { get; }

        /// <summary>The authored bound button paths, index-aligned with <see cref="BoundOptionNames"/>.</summary>
        protected abstract NodePath[] BoundOptionButtonPaths { get; }

        /// <summary>The minimum size for a generated button.</summary>
        protected abstract Vector2 OptionButtonMinimumSize { get; }

        /// <summary>The options shown in the generated fallback row (visibility already applied).</summary>
        protected abstract IReadOnlyList<ToggleOption> VisibleOptions { get; }

        /// <summary>Resolve an authored bound name to an option - ANY option, visible or not.</summary>
        protected abstract bool TryResolveName(string authored, out ToggleOption option);

        /// <summary>The canonical name of the current selection, or "" for none.</summary>
        protected abstract string CurrentName { get; }

        /// <summary>Select the option with this canonical name; return whether it took.</summary>
        protected abstract bool SelectByName(string name);

        /// <summary>Resolve the bar's collaborators (and connect any change signal). Called before every rebuild/refresh.</summary>
        protected abstract void ResolveReferences();

        // --- lifecycle ---

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildBar));
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            _buttonBindings.UnbindAll();
            OnExitTree();
        }

        /// <summary>Extra teardown for a subclass (e.g. disconnecting a mode-changed signal). Default none.</summary>
        protected virtual void OnExitTree() { }

        /// <summary>Bind authored buttons or generate a fallback row, then sync the pressed state.</summary>
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

            _row = new HBoxContainer
            {
                Name = GeneratedRowName,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(_row, "separation", 6);
            AddChild(_row);
            SetEditedOwner(_row);

            foreach (ToggleOption option in VisibleOptions)
                AddOptionButton(option);

            OnRowGenerated(_row);
            RefreshSelection();
        }

        /// <summary>Hook after the fallback row is built (e.g. the tool palette's Apply button). Default none.</summary>
        protected virtual void OnRowGenerated(HBoxContainer row) { }

        /// <summary>Push each button's pressed state to match the current selection, without emitting.</summary>
        public void RefreshSelection()
        {
            ResolveReferences();
            string current = CurrentName;
            foreach (var pair in _buttons)
                if (GodotObject.IsInstanceValid(pair.Value))
                    pair.Value.SetPressedNoSignal(pair.Key == current);
        }

        /// <summary>How many buttons the bar currently drives.</summary>
        public int VisibleButtonCount() => _buttons.Count;

        /// <summary>True when the bar drives authored buttons (bound explicitly or found by the name convention).</summary>
        public bool UsesSceneButtons()
            => BoundOptionNames.Length > 0 || BoundOptionButtonPaths.Length > 0 || HasConventionalButtons();

        // --- internals ---

        private bool BindExistingButtons()
        {
            _buttonBindings.UnbindAll();
            _buttons.Clear();

            string[] names = BoundOptionNames;
            NodePath[] paths = BoundOptionButtonPaths;

            if (names.Length > 0 || paths.Length > 0)
            {
                if (names.Length != paths.Length)
                    return false;

                for (int i = 0; i < names.Length; i++)
                {
                    if (!TryResolveName(names[i], out ToggleOption option))
                        return false;

                    Button? button = FindOptionButton(option.Name, i);
                    if (button == null)
                        return false;

                    BindOptionButton(option, button);
                }
            }
            else
            {
                foreach (ToggleOption option in VisibleOptions)
                {
                    Button? button = FindOptionButton(option.Name, -1);
                    if (button == null)
                        continue;

                    BindOptionButton(option, button);
                }
            }

            return _buttons.Count > 0;
        }

        private bool HasConventionalButtons()
        {
            foreach (ToggleOption option in VisibleOptions)
                if (FindOptionButton(option.Name, -1) != null)
                    return true;
            return false;
        }

        private Button? FindOptionButton(string name, int boundIndex)
            => FindControl<Button>(
                boundIndex >= 0 && BoundOptionButtonPaths.Length > boundIndex ? BoundOptionButtonPaths[boundIndex] : new NodePath(""),
                $"{ButtonNamePrefix}_{name}");

        private void BindOptionButton(ToggleOption option, Button button)
        {
            string name = option.Name;
            button.ToggleMode = true;
            if (string.IsNullOrWhiteSpace(button.Text))
                button.Text = option.Label;
            if (string.IsNullOrWhiteSpace(button.TooltipText))
                button.TooltipText = option.Tooltip;
            _buttonBindings.Bind(button, () => SelectByName(name));
            _buttons[name] = button;
        }

        private void AddOptionButton(ToggleOption option)
        {
            if (_row == null)
                return;

            string name = option.Name;
            var button = new Button
            {
                Name = $"{ButtonNamePrefix}_{name}",
                Text = option.Label,
                ToggleMode = true,
                CustomMinimumSize = OptionButtonMinimumSize,
                TooltipText = option.Tooltip
            };
            _buttonBindings.Bind(button, () => SelectByName(name));
            _row.AddChild(button);
            SetEditedOwner(button);
            _buttons[name] = button;
        }

        private void ClearChildren()
        {
            _buttonBindings.UnbindAll();
            foreach (Node child in GetChildren())
                child.QueueFree();
            _row = null;
        }

        /// <summary>The button generated/bound for a canonical option name, if any (for a subclass hook).</summary>
        protected Button? ButtonFor(string name) => _buttons.TryGetValue(name, out var button) ? button : null;

        /// <summary>Register an extra button (e.g. Apply) into the bar's binding set so it releases with the rest.</summary>
        protected void BindExtraButton(Button button, Action handler) => _buttonBindings.Bind(button, handler);
    }
}
