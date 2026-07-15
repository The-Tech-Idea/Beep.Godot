using Godot;

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
        [Export] public float MaxScreenDistance { get; set; } = 300f;

        [Signal] public delegate void TextSpawnedEventHandler(string text, Color color);

        public void ShowText(string text, string type = "normal")
        {
            var parent = GetParent();
            if (!IsActive || parent == null || !GodotObject.IsInstanceValid(parent)) return;

            Color color = type switch
            {
                "crit" => CritColor,
                "heal" => HealColor,
                _ => NormalColor
            };

            int size = type == "crit" ? CritFontSize : FontSize;

            var label = new Label();
            label.Text = text;
            label.AddThemeColorOverride("font_color", color);
            label.AddThemeFontSizeOverride("font_size", size);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Position = new Vector2(
                (GD.Randf() * 2f - 1f) * RandomOffset,
                -(GD.Randf() * RandomOffset / 2f));

            parent.AddChild(label);

            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(label, "position:y", label.Position.Y - FloatSpeed, Duration)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(label, "modulate:a", 0f, Duration * 0.3f)
                .SetDelay(Duration * 0.7f)
                .SetEase(Tween.EaseType.In);
            tween.Finished += () => label.QueueFree();

            EmitSignal(SignalName.TextSpawned, text, color);
        }
    }
}
