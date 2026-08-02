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
        [Export] public Vector2 Position { get; set; } = new(0, -8);
        [Export] public int MaxDisplay { get; set; } = 99;

        [Signal] public delegate void CountChangedEventHandler(int count);

        private Godot.Control? _control;
        private KitChip? _badgePanel;
        private Tween? _tween;

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent() as Godot.Control;
            if (_control == null)
                GD.PushWarning($"[{Name}] BadgeComponent needs a Control parent to anchor the badge to; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the Control being badged.");
            CallDeferred(nameof(BuildBadge));
            UpdateBadge(emit: false);   // seed visuals without a spurious startup CountChanged
        }

        private void BuildBadge()
        {
            if (Engine.IsEditorHint()) return;
            if (_control == null) return;

            _badgePanel = new KitChip
            {
                Kind = KitChip.ChipKind.Count,
                Role = UiSurface.Role.Danger
            };
            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            float d = Mathf.Max(fs * 2.0f, 18f);
            _badgePanel.CustomMinimumSize = new Vector2(d, d);
            _badgePanel.Size = new Vector2(d, d);
            _badgePanel.Position = Position;
            _badgePanel.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;

            _control.AddChild(_badgePanel);

            // Same trap as the dialog: the pop animation tweens the badge's transform, and a
            // sorting parent re-fits its children every layout pass. Loud beats silent — a badge
            // that never pops looks like a badge that was never wired.
            if (_control is Godot.Container)
                GD.PushWarning($"[{Name}] BadgeComponent's host '{_control.Name}' is a "
                             + $"{_control.GetType().Name}, which lays out its own children — the "
                             + "badge's pop animation may be overwritten. Host it on a plain "
                             + "Control, or animate with offset_transform_scale.");
            _badgePanel.ZIndex = 10;

            // Render the scene-authored Count now. _Ready's UpdateBadge ran before this deferred
            // build, so the panel existed to draw into only from here — an initial Count showed blank.
            UpdateBadge(emit: false);
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
                _badgePanel.Text = Count > MaxDisplay ? $"{MaxDisplay}+" : Count.ToString();
                // Pop animation
                _tween?.Kill();
                _tween = _badgePanel.CreateTween();
                _badgePanel.Scale = new Vector2(1.3f, 1.3f);
                _tween.TweenProperty(_badgePanel, "scale", Vector2.One, 0.2f)
                    .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            }

            if (emit) EmitSignal(SignalName.CountChanged, Count);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _tween?.Kill();
            // _badgePanel was AddChild'd to the parent Control, so freeing this component doesn't
            // take it along — free it, or it's orphaned onscreen.
            if (_badgePanel != null && GodotObject.IsInstanceValid(_badgePanel)) _badgePanel.QueueFree();
            _badgePanel = null;
        }
    }
}
