using Godot;
using Beep.ECS.UI.Kit;

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
        // Scale of the theme's body font, not a fixed size. The themes run 14-24, so a
        // literal renders a genre's larger type out of a control built for 14.
        [Export(PropertyHint.Range, "0.3,6.0,0.05")] public float FontScale { get; set; } = 1.0f;
        private int FontSize => UiSurface.FontSize(this, FontScale);
        [Export] public float FadeDuration { get; set; } = 0.15f;

        private Godot.Control? _label;
        private bool _createdLabel;   // true only when we new'd the label (vs adopting a parent Label)
        private Tween? _fade;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(SetupLabel));
        }

        private void SetupLabel()
        {
            if (Engine.IsEditorHint()) return;
            EnsureLabel();
            if (_label != null)
            {
                _label.Visible = false;
                _label.Modulate = new Color(1, 1, 1, 0);
            }
        }

        private void EnsureLabel()
        {
            if (GetParent() is Label existing) { _label = existing; StyleLabel(); return; }
            _createdLabel = true;
            _label = new KitHudText
            {
                Name = "PromptLabel",
                Text = DefaultText,
                Role = UiSurface.TextRole.Caption,
                ShowPlate = true,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            StyleLabel();
            // Parent may be a CanvasLayer (the documented HUD usage) — not a Control — so
            // child it as a plain Node, not `as Control` (which was null → AddChild skipped
            // and the next line NRE'd on parent.IsInsideTree()).
            var parent = GetParent();
            if (parent != null)
            {
                parent.AddChild(_label);
                if (parent.IsInsideTree()) _label.Owner = parent.Owner;
            }
        }

        private void StyleLabel()
        {
            if (_label == null) return;
            _label.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
            if (_label is Label label)
            {
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                label.AutowrapMode = TextServer.AutowrapMode.Off;
                label.ClipText = true;
                label.AddThemeFontSizeOverride("font_size", FontSize);
                label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
                label.AddThemeConstantOverride("shadow_offset_x", 1);
                label.AddThemeConstantOverride("shadow_offset_y", 2);
            }
        }

        public void Show(string text = "")
        {
            if (!IsActive || _label == null) return;
            if (text.Length > 0) SetText(text);
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
            _fade.Finished += OnHideFinished;
        }

        private void OnHideFinished()
        {
            if (_label != null) _label.Visible = false;
        }

        private void SetText(string text)
        {
            if (_label is KitHudText hud) hud.Text = text;
            else if (_label is Label label) label.Text = text;
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _fade?.Kill();
            // Free the label only if WE created it (parent-hosted). When the parent itself is a
            // Label we adopted it — don't free someone else's node.
            if (_createdLabel && _label != null && GodotObject.IsInstanceValid(_label)) _label.QueueFree();
            _label = null;
        }
    }
}
