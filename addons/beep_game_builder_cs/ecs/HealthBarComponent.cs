using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Health bar component. Blind — auto-locates a sibling HealthComponent and renders a bar.
    /// Works for any entity with health — players, enemies, bosses.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HealthBarComponent : GameplayComponent
    {
        [Export] public Vector2 Size { get; set; } = new(40, 6);
        [Export] public Vector2 BarOffset { get; set; } = new(0, -20);
        [Export] public Color HealthyColor { get; set; } = Colors.Green;
        [Export] public Color WarningColor { get; set; } = Colors.Yellow;
        [Export] public Color DangerColor { get; set; } = Colors.Red;
        [Export] public Color BgColor { get; set; } = new(0, 0, 0, 0.5f);
        [Export] public bool ShowOnlyWhenDamaged { get; set; } = true;
        [Export] public float HideDelay { get; set; } = 3f;

        private HealthComponent? _health;
        private ProgressBar? _bar;
        private float _hideTimer;

        public override void _Ready()
        {
            base._Ready();
            // SetupBar spawns a ProgressBar into the parent. This class is [Tool], so
            // without the guard, opening a scene that uses it would litter the scene with
            // runtime-only nodes in the editor.
            if (Engine.IsEditorHint()) return;
            CallDeferred(nameof(SetupBar));
        }

        private void SetupBar()
        {
            _health = GetSiblingComponent<HealthComponent>();
            if (_health == null) return;

            _bar = new ProgressBar();
            _bar.CustomMinimumSize = Size;
            _bar.MaxValue = _health.MaxHealth;
            _bar.Value = _health.CurrentHealth;
            _bar.ShowPercentage = false;
            _bar.Position = BarOffset - Size / 2f;
            _bar.AddThemeStyleboxOverride("fill", CreateStyleBox(HealthyColor));
            _bar.AddThemeStyleboxOverride("background", CreateStyleBox(BgColor));

            var parent = GetParent();
            if (parent != null)
            {
                parent.AddChild(_bar);
                if (parent.IsInsideTree())
                    _bar.Owner = parent.Owner;
            }

            _health.HealthChanged += OnHealthChanged;

            _bar.Visible = !ShowOnlyWhenDamaged;
        }

        private void OnHealthChanged(float cur, float max)
        {
            if (_bar == null) return;
            _bar.MaxValue = max;
            _bar.Value = cur;
            float pct = cur / (float)max;
            Color fillColor = pct > 0.5f ? HealthyColor : pct > 0.25f ? WarningColor : DangerColor;
            _bar.AddThemeStyleboxOverride("fill", CreateStyleBox(fillColor));
            _bar.Visible = true;
            _hideTimer = HideDelay;
        }

        public override void _Process(double delta)
        {
            if (_bar == null || !ShowOnlyWhenDamaged || !_bar.Visible) return;
            _hideTimer -= (float)delta;
            if (_hideTimer <= 0) _bar.Visible = false;
        }

        private static StyleBoxFlat CreateStyleBox(Color c)
        {
            var sb = new StyleBoxFlat { BgColor = c };
            sb.SetCornerRadiusAll(2);
            return sb;
        }

        public override void _ExitTree()
        {
            if (_health != null)
                _health.HealthChanged -= OnHealthChanged;
            if (_bar != null && GodotObject.IsInstanceValid(_bar))
                _bar.QueueFree();
        }
    }
}
