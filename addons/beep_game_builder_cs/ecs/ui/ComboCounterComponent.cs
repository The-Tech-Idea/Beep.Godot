using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Combo counter display. Call Increment() each time the player lands a hit
    /// or chains an action. The combo number grows with font size + shake, and
    /// auto-resets to 0 after ResetTime seconds of inactivity.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ComboCounterComponent : UIComponent
    {
        [Export] public float ResetTime { get; set; } = 2f;
        // Scale of the theme's body font, not a fixed size. The themes run 14-24, so a
        // literal renders a genre's larger type out of a control built for 14.
        [Export(PropertyHint.Range, "0.3,6.0,0.05")] public float BaseFontScale { get; set; } = 1.9f;
        private int BaseFontSize => UiSurface.FontSize(this, BaseFontScale);
        [Export(PropertyHint.Range, "0.3,6.0,0.05")] public float MaxFontScale { get; set; } = 3.2f;
        private int MaxFontSize => UiSurface.FontSize(this, MaxFontScale);
        [Export] public NodePath ComboLabelPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color ComboColor => UiSurface.Semantic(this, UiSurface.Role.Warning);
        [Signal] public delegate void ComboChangedEventHandler(int count);
        [Signal] public delegate void ComboResetEventHandler();

        private KitHudText? _label;
        private bool _createdLabel;
        private int _count;
        private float _resetTimer;
        private Tween? _punchTween;

        public override void _Ready()
        {
            base._Ready();
            SetProcess(false);
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(SetupLabel));
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && FindComboLabel() == null)
                return new[] { "Set ComboLabelPath, add a scene-authored KitHudText named ComboLabel, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        private void SetupLabel()
        {
            if (BindExistingLabel())
            {
                StyleLabel();
                if (Engine.IsEditorHint())
                {
                    _label!.Text = "3x";
                    _label.Visible = true;
                }
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            if (GetParent() is not Node parent)
                return;

            _createdLabel = true;
            _label = new KitHudText
            {
                Name = "ComboLabel",
                Text = Engine.IsEditorHint() ? "3x" : "",
                Visible = Engine.IsEditorHint(),
                Role = UiSurface.TextRole.Title,
                Accent = UiSurface.Role.Warning,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            StyleLabel();
            parent.AddChild(_label);
            SetEditedOwner(_label);
        }

        private bool BindExistingLabel()
        {
            _createdLabel = false;
            _label = FindComboLabel();
            return _label != null;
        }

        public bool UsesSceneControls()
            => !ComboLabelPath.IsEmpty || FindComboLabel() != null;

        private KitHudText? FindComboLabel()
        {
            if (!ComboLabelPath.IsEmpty && GetNodeOrNull<KitHudText>(ComboLabelPath) is { } pathLabel)
                return pathLabel;

            if (FindChild("ComboLabel", recursive: true, owned: false) is KitHudText childLabel)
                return childLabel;

            return GetParent()?.FindChild("ComboLabel", recursive: true, owned: false) as KitHudText;
        }

        private void StyleLabel()
        {
            if (_label == null) return;
            _label.Role = UiSurface.TextRole.Title;
            _label.Accent = UiSurface.Role.Warning;
            _label.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
            // Punch on the offset_transform layer so a container parent can't overwrite the
            // scale (matches the other migrated effects).
            _label.OffsetTransformEnabled = true;
            if (!Engine.IsEditorHint())
                _label.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (_count == 0 || !IsActive)
            {
                UpdateProcessing();
                return;
            }
            _resetTimer -= (float)delta;
            if (_resetTimer <= 0) ResetCombo();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _punchTween?.Kill();   // consistency with the repo's tween-owning components
            _punchTween = null;
            // Free only fallback labels this component created; authored labels belong to the scene.
            if (_createdLabel && _label != null && GodotObject.IsInstanceValid(_label)) _label.QueueFree();
            _label = null;
        }

        /// <summary>Add one to the combo counter and reset the timer.</summary>
        public void Increment()
        {
            if (!IsActive) return;
            _count++;
            _resetTimer = ResetTime;
            if (_label == null) return;
            _label.Text = $"{_count}x";
            _label.Visible = true;

            // Punch: scale up briefly then settle.
            int fontSize = Mathf.Clamp(BaseFontSize + _count * 2, BaseFontSize, MaxFontSize);
            _label.CustomMinimumSize = new Vector2(fontSize * 4f, fontSize * 1.8f);
            _punchTween?.Kill();
            _label.PivotOffset = _label.Size / 2f;   // punch from the center, not the corner
            _label.OffsetTransformScale = new Vector2(1.3f, 1.3f);
            _punchTween = CreateTween();
            _punchTween.TweenProperty(_label, "offset_transform_scale", Vector2.One, 0.15f).SetEase(Tween.EaseType.Out);
            EmitSignal(SignalName.ComboChanged, _count);
            UpdateProcessing();
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }

        /// <summary>Reset combo to 0 and hide the label.</summary>
        public void ResetCombo()
        {
            _count = 0;
            if (_label != null) _label.Visible = false;
            EmitSignal(SignalName.ComboReset);
            UpdateProcessing();
        }

        private void UpdateProcessing()
            => SetProcess(!Engine.IsEditorHint() && IsActive && _count > 0);
    }
}
