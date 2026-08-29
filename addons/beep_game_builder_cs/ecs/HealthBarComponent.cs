using Godot;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

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
        private KitMeter? _bar;
        private float _hideTimer;

        public Vector2 EffectiveSize => new(PositiveFinite(Size.X, 40f), PositiveFinite(Size.Y, 6f));
        public float EffectiveHideDelay => NonNegativeFinite(HideDelay);

        public override void _Ready()
        {
            base._Ready();
            // SetupBar spawns a ProgressBar into the parent. This class is [Tool], so
            // without the guard, opening a scene that uses it would litter the scene with
            // runtime-only nodes in the editor.
            if (Engine.IsEditorHint()) return;
            Callable.From(SetupBar).CallDeferred();
        }

        private void SetupBar()
        {
            if (_bar != null && GodotObject.IsInstanceValid(_bar)) return;
            _health = GetSiblingComponent<HealthComponent>();
            if (_health == null)
            {
                GD.PushWarning($"[{Name}] HealthBarComponent found no sibling HealthComponent — the bar will not appear. Add it beside a HealthComponent on the same entity.");
                return;
            }

            _bar = new KitMeter();
            Vector2 size = EffectiveSize;
            float max = Mathf.Max(1f, _health.MaxHealth);
            _bar.CustomMinimumSize = size;
            _bar.MaxValue = max;
            _bar.Value = Mathf.Clamp(_health.CurrentHealth, 0f, max);
            _bar.ShowPercentage = false;
            _bar.Position = BarOffset - size / 2f;
            _bar.Segments = 6;
            _bar.Fill = UiSurface.Role.Success;

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
            if (_bar == null || !GodotObject.IsInstanceValid(_bar)) return;
            float safeMax = Mathf.Max(1f, max);
            _bar.MaxValue = safeMax;
            _bar.Value = Mathf.Clamp(cur, 0f, safeMax);
            float pct = Mathf.Clamp(cur / safeMax, 0f, 1f);
            _bar.Fill = pct > 0.5f ? UiSurface.Role.Success : pct > 0.25f ? UiSurface.Role.Warning : UiSurface.Role.Danger;
            _bar.Visible = true;
            _hideTimer = EffectiveHideDelay;
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive || _bar == null || !ShowOnlyWhenDamaged || !_bar.Visible) return;
            _hideTimer -= DeltaSeconds(delta);
            if (_hideTimer <= 0) _bar.Visible = false;
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_health != null && GodotObject.IsInstanceValid(_health))
                _health.HealthChanged -= OnHealthChanged;
            if (_bar != null && GodotObject.IsInstanceValid(_bar))
                _bar.QueueFree();
        }

        private static float DeltaSeconds(double delta)
            => double.IsFinite(delta) && delta > 0.0 ? (float)delta : 0f;

        private static float PositiveFinite(float value, float fallback)
            => float.IsFinite(value) && value > 0f ? value : fallback;

        private static float NonNegativeFinite(float value)
            => float.IsFinite(value) && value > 0f ? value : 0f;
    }
}
