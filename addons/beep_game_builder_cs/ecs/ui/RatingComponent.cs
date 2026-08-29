using Godot;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Star rating component. Attach to any Container to display 1-5 stars.
    /// Blind — works for reviews, player ratings, difficulty, quality.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class RatingComponent : UIComponent
    {
        [Export] public int MaxStars { get; set; } = 5;
        [Export] public float Value { get; set; } = 3.5f;
        [Export] public float StarSize { get; set; } = 24f;
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color FilledColor => UiSurface.Semantic(this, UiSurface.Role.Warning);
        public Color EmptyColor => UiSurface.Semantic(this, UiSurface.Role.Neutral);
        [Export] public bool Interactive { get; set; } = false;
        [Export] public NodePath RatingPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [Signal] public delegate void RatingChangedEventHandler(float newValue);

        private Container? _container;
        private readonly List<Godot.Control> _starLabels = new();
        private KitStarRating? _kitRating;
        private bool _createdRating;
        // The committed rating. Value is only the DISPLAYED value and shows a preview while hovering;
        // _committed is the truth, so moving the mouse away restores it instead of keeping the preview.
        private float _committed;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            if (_container == null && GenerateControlsWhenPathsEmpty)
            {
                GD.PushWarning($"[{Name}] parent is not a Container — the star row cannot be built.");
                return;
            }
            _committed = Value;
            // Focusable when interactive, so a keyboard/gamepad player can adjust it (ui_left/right).
            if (Interactive && _container != null) _container.FocusMode = Godot.Control.FocusModeEnum.All;
            if (!Engine.IsEditorHint() || BuildInEditor)
                SetupRating();
            UpdateDisplay();
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && FindRating() == null)
                return new[] { "Add an authored KitStarRating named Rating, set RatingPath, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Interactive || _container == null || !_container.HasFocus()) return;
            if (@event.IsActionPressed("ui_right")) { SetValue(Mathf.Min(_committed + 1, MaxStars)); GetViewport().SetInputAsHandled(); }
            else if (@event.IsActionPressed("ui_left")) { SetValue(Mathf.Max(_committed - 1, 0)); GetViewport().SetInputAsHandled(); }
        }

        private void SetupRating()
        {
            if (BindExistingRating())
            {
                StyleRating();
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            BuildGeneratedRating();
        }

        private void BuildGeneratedRating()
        {
            if (_container == null)
                return;

            _createdRating = true;
            _kitRating = new KitStarRating
            {
                Name = "Rating",
                Total = MaxStars,
                Earned = Mathf.RoundToInt(Value),
                Role = UiSurface.Role.Warning,
                CustomMinimumSize = new Vector2((StarSize + 8f) * MaxStars, StarSize + 12f),
                MouseFilter = Interactive ? Godot.Control.MouseFilterEnum.Stop : Godot.Control.MouseFilterEnum.Ignore
            };
            StyleRating();
            _starLabels.Add(_kitRating);
            _container.AddChild(_kitRating);
            SetEditedOwner(_kitRating);
        }

        private bool BindExistingRating()
        {
            _createdRating = false;
            _kitRating = FindRating();
            return _kitRating != null;
        }

        public bool UsesSceneControls()
            => FindRating() != null;

        private KitStarRating? FindRating()
        {
            if (!RatingPath.IsEmpty && GetNodeOrNull<KitStarRating>(RatingPath) is { } pathRating)
                return pathRating;

            if (FindChild("Rating", recursive: true, owned: false) is KitStarRating childRating)
                return childRating;

            return GetParent()?.FindChild("Rating", recursive: true, owned: false) as KitStarRating;
        }

        private void StyleRating()
        {
            if (_kitRating == null) return;
            _kitRating.Total = MaxStars;
            _kitRating.Earned = Mathf.RoundToInt(Value);
            _kitRating.Role = UiSurface.Role.Warning;
            _kitRating.CustomMinimumSize = new Vector2((StarSize + 8f) * MaxStars, StarSize + 12f);
            _kitRating.MouseFilter = Interactive ? Godot.Control.MouseFilterEnum.Stop : Godot.Control.MouseFilterEnum.Ignore;
            if (!_kitRating.IsConnected(KitStarRating.SignalName.ValueChanged, Callable.From<double>(OnKitRatingChanged)))
                _kitRating.ValueChanged += OnKitRatingChanged;
        }

        private void OnKitRatingChanged(double value)
        {
            if (_kitRating == null) return;
            Value = (float)value;
            _committed = Value;
            EmitSignal(SignalName.RatingChanged, Value);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_kitRating != null && GodotObject.IsInstanceValid(_kitRating))
                _kitRating.ValueChanged -= OnKitRatingChanged;
            foreach (var s in _starLabels) if (GodotObject.IsInstanceValid(s)) s.QueueFree();
            _starLabels.Clear();
            _kitRating = null;
        }

        public void UpdateDisplay()
        {
            if (_container == null) return;
            if (_kitRating != null && GodotObject.IsInstanceValid(_kitRating))
            {
                _kitRating.Total = MaxStars;
                _kitRating.Earned = Mathf.RoundToInt(Value);
                return;
            }
            var children = _container.GetChildren();
            for (int i = 0; i < children.Count && i < MaxStars; i++)
            {
                if (children[i] is Label label)
                {
                    float fill = Mathf.Clamp(Value - i, 0f, 1f);
                    Color color = fill >= 1f ? FilledColor :
                        fill > 0f ? FilledColor.Lerp(EmptyColor, 1f - fill) : EmptyColor;
                    KitChrome.SetColorOverrideIfChanged(label, "font_color", color);
                }
            }
        }

        public void SetValue(float value)
        {
            Value = value;
            _committed = value;
            UpdateDisplay();
            // Emit for programmatic changes too — an interactive click already emits (BuildStars),
            // so a listener saw user clicks but not code-driven updates.
            EmitSignal(SignalName.RatingChanged, Value);
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}
