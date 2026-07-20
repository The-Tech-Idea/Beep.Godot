using Godot;

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
        [Export] public Color BadgeColor { get; set; } = new(0.9f, 0.2f, 0.2f, 1f);
        [Export] public Vector2 Position { get; set; } = new(0, -8);
        [Export] public int MaxDisplay { get; set; } = 99;

        [Signal] public delegate void CountChangedEventHandler(int count);

        private Godot.Control? _control;
        private Label? _badgeLabel;
        private Panel? _badgePanel;
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

            _badgePanel = new Panel();
            _badgePanel.CustomMinimumSize = new Vector2(22, 22);
            _badgePanel.Size = new Vector2(22, 22);
            _badgePanel.Position = Position;

            var sb = new StyleBoxFlat { BgColor = BadgeColor };
            sb.SetCornerRadiusAll(11);
            _badgePanel.AddThemeStyleboxOverride("panel", sb);

            _badgeLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _badgeLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _badgePanel.AddChild(_badgeLabel);

            _control.AddChild(_badgePanel);
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
            if (_badgePanel == null || _badgeLabel == null) return;
            bool show = Count > 0;
            _badgePanel.Visible = show;

            if (show)
            {
                _badgeLabel.Text = Count > MaxDisplay ? $"{MaxDisplay}+" : Count.ToString();
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
