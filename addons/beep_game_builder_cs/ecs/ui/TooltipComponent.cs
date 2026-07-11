using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Hover tooltip component. Attach to any Control to show a tooltip on hover.
    /// Blind — works for buttons, icons, inventory slots, stats, skill trees.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TooltipComponent : EntityComponent
    {
        [Export(PropertyHint.MultilineText)]
        public string TooltipText { get; set; } = "";
        [Export] public float ShowDelay { get; set; } = 0.5f;
        [Export] public Vector2 Offset { get; set; } = new(10, -10);
        [Export] public Color BgColor { get; set; } = new(0.05f, 0.05f, 0.1f, 0.9f);

        [Signal] public delegate void TooltipShownEventHandler();
        [Signal] public delegate void TooltipHiddenEventHandler();

        private Control? _control;
        private Panel? _tooltipPanel;
        private float _hoverTime;
        private bool _showing;

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent<Control>();
            if (_control != null)
            {
                _control.MouseEntered += () => { if (IsActive) _hoverTime = 0; };
                _control.MouseExited += HideTooltip;
            }
        }

        public override void _Process(double delta)
        {
            if (!IsActive || _showing || string.IsNullOrEmpty(TooltipText)) return;
            if (_hoverTime < ShowDelay) { _hoverTime += (float)delta; return; }

            ShowTooltip();
        }

        private void ShowTooltip()
        {
            if (_control == null) return;
            _showing = true;

            _tooltipPanel = new Panel();
            var label = new Label { Text = TooltipText, AutowrapMode = TextServer.AutowrapMode.Word };
            label.AddThemeColorOverride("font_color", Colors.White);
            label.AddThemeFontSizeOverride("font_size", 12);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            margin.AddChild(label);
            _tooltipPanel.AddChild(margin);

            var sb = new StyleBoxFlat { BgColor = BgColor };
            sb.SetCornerRadiusAll(4);
            _tooltipPanel.AddThemeStyleboxOverride("panel", sb);

            _tooltipPanel.Position = _control.GetGlobalMousePosition() + Offset + new Vector2(0, 20);
            _control.GetParent()?.AddChild(_tooltipPanel);
            _tooltipPanel.ZIndex = 100;

            EmitSignal(SignalName.TooltipShown);
        }

        private void HideTooltip()
        {
            _hoverTime = 0;
            _showing = false;
            _tooltipPanel?.QueueFree();
            _tooltipPanel = null;
            EmitSignal(SignalName.TooltipHidden);
        }

        public override void _ExitTree()
        {
            _tooltipPanel?.QueueFree();
            base._ExitTree();
        }
    }
}
