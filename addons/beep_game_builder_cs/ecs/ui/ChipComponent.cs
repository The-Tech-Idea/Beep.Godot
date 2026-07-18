using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Chip/tag component. Attach to a Container to create styled tag chips with remove button.
    /// Blind — works for filters, categories, player positions, selected items.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ChipComponent : UIComponent
    {
        [Export] public string Label { get; set; } = "Tag";
        [Export] public Color ChipColor { get; set; } = new(0.2f, 0.4f, 0.7f, 1f);
        [Export] public bool Removable { get; set; } = true;
        [Export] public int FontSize { get; set; } = 12;

        [Signal] public delegate void RemovedEventHandler(string label);
        [Signal] public delegate void ClickedEventHandler(string label);

        private Container? _container;
        private Panel? _chip;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] ChipComponent needs a Container parent to hold chips; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to an HFlowContainer.");
                return;
            }
            BuildChip();
        }

        private void BuildChip()
        {
            if (Engine.IsEditorHint()) return;
            _chip = new Panel();
            _chip.CustomMinimumSize = new Vector2(0, 28);
            var sb = new StyleBoxFlat { BgColor = ChipColor };
            sb.SetCornerRadiusAll(14);
            _chip.AddThemeStyleboxOverride("panel", sb);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 4);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_right", Removable ? 4 : 12);
            margin.AddThemeConstantOverride("margin_top", 4);
            margin.AddThemeConstantOverride("margin_bottom", 4);

            var label = new Label { Text = Label, VerticalAlignment = VerticalAlignment.Center };
            label.AddThemeFontSizeOverride("font_size", FontSize);
            label.AddThemeColorOverride("font_color", Colors.White);
            margin.AddChild(label);
            hbox.AddChild(margin);

            if (Removable)
            {
                var closeBtn = new Button { Text = "×", Flat = true, CustomMinimumSize = new Vector2(24, 24) };
                closeBtn.AddThemeFontSizeOverride("font_size", 14);
                closeBtn.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
                closeBtn.Pressed += () =>
                {
                    EmitSignal(SignalName.Removed, Label);
                    _chip?.QueueFree();
                };
                hbox.AddChild(closeBtn);
            }

            _chip.AddChild(hbox);
            _container?.AddChild(_chip);

            _chip.GuiInput += e =>
            {
                if (e is InputEventMouseButton mb && mb.Pressed)
                    EmitSignal(SignalName.Clicked, Label);
            };
        }
    }
}
