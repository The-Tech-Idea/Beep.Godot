using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Notification badge component. Attach to any Control to show a red badge.
    /// Blind — works for buttons, tabs, icons, mail indicators.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BadgeComponent : UIComponent
    {
        [Export] public int Count { get; set; } = 0;
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color BadgeColor => UiSurface.Semantic(this, UiSurface.Role.Danger);
        /// <summary>
        /// Nudge from the host's TOP-RIGHT corner, in pixels. Zero pins the badge flush into that
        /// corner, inside the host.
        ///
        /// It used to be an absolute position defaulting to (0, -8), which put the badge against
        /// the host's LEFT edge and eight pixels ABOVE it — outside the control on the one side a
        /// layout has no room to give, so it overlapped whatever sat above and sat on the wrong
        /// corner besides. The kit's own art notes call the top-right straddle "the attention
        /// anchor"; a badge that leaves its host's rect is a badge that collides with the row.
        /// </summary>
        [Export] public Vector2 Position { get; set; } = Vector2.Zero;
        [Export] public int MaxDisplay { get; set; } = 99;
        [Export] public NodePath BadgePath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [Signal] public delegate void CountChangedEventHandler(int count);

        private Godot.Control? _control;
        private KitChip? _badgePanel;
        private bool _createdBadge;
        private Tween? _tween;

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent() as Godot.Control;
            if (_control == null && GenerateControlsWhenPathsEmpty)
                GD.PushWarning($"[{Name}] BadgeComponent needs a Control parent to anchor the badge to; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the Control being badged.");
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(SetupBadge));
            UpdateBadge(emit: false);   // seed visuals without a spurious startup CountChanged
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && FindBadge() == null)
                return new[] { "Add an authored KitChip named Badge, set BadgePath, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        private void SetupBadge()
        {
            if (BindExistingBadge())
            {
                StyleBadge();
                UpdateBadge(emit: false);
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            BuildGeneratedBadge();
        }

        private void BuildGeneratedBadge()
        {
            if (_control == null) return;

            _createdBadge = true;
            _badgePanel = new KitChip
            {
                Name = "Badge",
                Kind = KitChip.ChipKind.Count,
                Role = UiSurface.Role.Danger
            };
            StyleBadge();
            _control.AddChild(_badgePanel);
            SetEditedOwner(_badgePanel);

            // Same trap as the dialog: the pop animation tweens the badge's transform, and a
            // sorting parent re-fits its children every layout pass. Loud beats silent — a badge
            // that never pops looks like a badge that was never wired.
            if (_control is Godot.Container)
                GD.PushWarning($"[{Name}] BadgeComponent's host '{_control.Name}' is a "
                             + $"{_control.GetType().Name}, which lays out its own children — the "
                             + "badge's pop animation may be overwritten. Host it on a plain "
                             + "Control, or animate with offset_transform_scale.");

            UpdateBadge(emit: false);
        }

        private bool BindExistingBadge()
        {
            _createdBadge = false;
            _badgePanel = FindBadge();
            return _badgePanel != null;
        }

        public bool UsesSceneControls()
            => FindBadge() != null;

        private KitChip? FindBadge()
        {
            if (!BadgePath.IsEmpty && GetNodeOrNull<KitChip>(BadgePath) is { } pathBadge)
                return pathBadge;

            if (FindChild("Badge", recursive: true, owned: false) is KitChip childBadge)
                return childBadge;

            return GetParent()?.FindChild("Badge", recursive: true, owned: false) as KitChip;
        }

        private void StyleBadge()
        {
            if (_badgePanel == null) return;
            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            float d = Mathf.Max(fs * 2.0f, 18f);
            _badgePanel.Kind = KitChip.ChipKind.Count;
            _badgePanel.Role = UiSurface.Role.Danger;
            // A floor, not a fixed size: the chip measures itself from its text and must be free
            // to be wider than the square. Assigning Size here fought the anchors set below.
            _badgePanel.CustomMinimumSize = new Vector2(d, d);

            PlaceBadge();
        }

        /// <summary>
        /// Anchor the badge into the host's top-right corner, INSIDE it.
        ///
        /// Anchors and GROW DIRECTION, not a measured rect. Measuring was the first attempt and it
        /// is unfixable in principle: a Count chip sizes itself from its text, so the size read
        /// here is the size from before the count changed, and the badge escaped the host's right
        /// edge by 2.8px the moment "1" became "7". Godot already solves this — a control anchored
        /// to a corner with zero offsets grows to its own minimum in whichever direction
        /// grow_horizontal/grow_vertical name, every layout pass, with nothing to keep in step.
        ///
        /// Growing LEFT and DOWN from the top-right corner is what keeps the badge inside the host
        /// however wide its text gets.
        /// </summary>
        private void PlaceBadge()
        {
            if (_badgePanel == null) return;

            _badgePanel.SetAnchorsPreset(Godot.Control.LayoutPreset.TopRight, keepOffsets: false);
            _badgePanel.GrowHorizontal = Godot.Control.GrowDirection.Begin;
            _badgePanel.GrowVertical = Godot.Control.GrowDirection.End;
            _badgePanel.OffsetLeft = Position.X;
            _badgePanel.OffsetRight = Position.X;
            _badgePanel.OffsetTop = Position.Y;
            _badgePanel.OffsetBottom = Position.Y;
        }

        public void SetCount(int count)
        {
            Count = count;
            UpdateBadge();
        }

        public void Increment(int amount = 1) { Count += amount; UpdateBadge(); }

        private void UpdateBadge(bool emit = true)
        {
            if (_badgePanel == null) return;
            bool show = Count > 0;
            _badgePanel.Visible = show;

            if (show)
            {
                // TEXT FIRST, then take the corner. A Count chip measures itself from its text, so
                // anchoring before the assignment anchors against the PREVIOUS count's width and
                // the badge hangs off the host's edge by the difference.
                _badgePanel.Text = Count > MaxDisplay ? $"{MaxDisplay}+" : Count.ToString();
                // Pop animation
                _tween?.Kill();
                _tween = _badgePanel.CreateTween();
                _badgePanel.Scale = new Vector2(1.3f, 1.3f);
                _tween.TweenProperty(_badgePanel, "scale", Vector2.One, 0.2f)
                    .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            }

            PlaceBadge();

            if (emit) EmitSignal(SignalName.CountChanged, Count);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _tween?.Kill();
            if (_createdBadge && _badgePanel != null && GodotObject.IsInstanceValid(_badgePanel)) _badgePanel.QueueFree();
            _badgePanel = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}
