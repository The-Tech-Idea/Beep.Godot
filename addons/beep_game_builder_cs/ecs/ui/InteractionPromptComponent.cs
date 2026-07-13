using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// "Press E to interact" prompt. Shows/hides a child Label via Show(text)/Hide().
    /// Place on a HUD CanvasLayer. An InteractableComponent (or any caller) drives
    /// it: when the player enters an interaction zone, call Show("Press E: Open Door").
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class InteractionPromptComponent : UIComponent
    {
        [Export] public string DefaultText { get; set; } = "Press E";
        [Export] public int FontSize { get; set; } = 16;
        [Export] public float FadeDuration { get; set; } = 0.15f;

        private Label? _label;
        private Tween? _fade;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(EnsureLabel));
            _label!.Visible = false;
            _label.Modulate = new Color(1, 1, 1, 0);
        }

        private void EnsureLabel()
        {
            if (GetParent() is Label existing) { _label = existing; return; }
            _label = new Label
            {
                Name = "PromptLabel",
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = DefaultText
            };
            _label.AddThemeFontSizeOverride("font_size", FontSize);
            var parent = GetParent() as Godot.Control;
            if (parent != null) parent.AddChild(_label);
            if (parent.IsInsideTree()) _label.Owner = parent.Owner;
        }

        public void Show(string text = "")
        {
            if (!IsActive || _label == null) return;
            if (text.Length > 0) _label.Text = text;
            _label.Visible = true;
            _fade?.Kill();
            _fade = CreateTween();
            _fade.TweenProperty(_label, "modulate:a", 1f, FadeDuration);
        }

        public void Hide()
        {
            if (_label == null) return;
            _fade?.Kill();
            _fade = CreateTween();
            _fade.TweenProperty(_label, "modulate:a", 0f, FadeDuration);
            _fade.Finished += () => _label.Visible = false;
        }
    }
}
