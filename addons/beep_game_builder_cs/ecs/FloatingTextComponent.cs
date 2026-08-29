using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Damage numbers / floating text component. Blind — attach to any entity.
    /// Spawns a Label that floats up and fades out. Works for damage, heals, XP, crits.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class FloatingTextComponent : GameplayComponent
    {
        [Export] public Color NormalColor { get; set; } = Colors.White;
        [Export] public Color CritColor { get; set; } = Colors.Orange;
        [Export] public Color HealColor { get; set; } = Colors.Green;
        [Export] public float FloatSpeed { get; set; } = 60f;
        [Export] public float Duration { get; set; } = 1.2f;
        [Export] public int FontSize { get; set; } = 20;
        [Export] public int CritFontSize { get; set; } = 28;
        [Export] public float RandomOffset { get; set; } = 15f;

        [Signal] public delegate void TextSpawnedEventHandler(string text, Color color);

        public float EffectiveFloatSpeed => Mathf.Max(0f, FloatSpeed);
        public float EffectiveDuration => Mathf.Max(0.05f, Duration);
        public int EffectiveFontSize => Mathf.Max(1, FontSize);
        public int EffectiveCritFontSize => Mathf.Max(1, CritFontSize);
        public float EffectiveRandomOffset => Mathf.Max(0f, RandomOffset);

        public void ShowText(string text, string type = "normal")
        {
            var parent = GetParent();
            if (Engine.IsEditorHint() || !IsActive || parent == null || !GodotObject.IsInstanceValid(parent) || !parent.IsInsideTree()) return;
            text ??= string.Empty;
            type ??= "normal";

            Color color = type switch
            {
                "crit" => CritColor,
                "heal" => HealColor,
                _ => NormalColor
            };

            int size = type == "crit" ? EffectiveCritFontSize : EffectiveFontSize;
            float randomOffset = EffectiveRandomOffset;
            float duration = EffectiveDuration;

            var label = new Label();
            label.Text = text;
            KitChrome.SetColorOverrideIfChanged(label, "font_color", color);
            KitChrome.SetFontSizeOverrideIfChanged(label, "font_size", size);
            KitChrome.SetColorOverrideIfChanged(label, "font_shadow_color", new Color(0, 0, 0, 0.72f));
            KitChrome.SetConstantOverrideIfChanged(label, "shadow_offset_x", 1);
            KitChrome.SetConstantOverrideIfChanged(label, "shadow_offset_y", 1);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.CustomMinimumSize = new Vector2(Mathf.Max(48, size * Mathf.Max(2, text.Length)), size * 1.6f);
            label.Position = new Vector2(
                (GD.Randf() * 2f - 1f) * randomOffset,
                -(GD.Randf() * randomOffset / 2f));

            parent.AddChild(label);

            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(label, "position:y", label.Position.Y - EffectiveFloatSpeed, duration)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(label, "modulate:a", 0f, duration * 0.3f)
                .SetDelay(duration * 0.7f)
                .SetEase(Tween.EaseType.In);
            tween.Finished += () =>
            {
                if (GodotObject.IsInstanceValid(label)) label.QueueFree();
            };

            EmitSignal(SignalName.TextSpawned, text, color);
        }
    }
}
