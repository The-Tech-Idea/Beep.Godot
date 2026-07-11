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
            BuildBadge();
            UpdateBadge();
        }

        private void BuildBadge()
        {
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
        }

        public void SetCount(int count)
        {
            Count = count;
            UpdateBadge();
        }

        public void Increment(int amount = 1) { Count += amount; UpdateBadge(); }

        private void UpdateBadge()
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

            EmitSignal(SignalName.CountChanged, Count);
        }
    }
}
