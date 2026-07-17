using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Hover tooltip component. Attach to any Control to show a tooltip on hover.
    /// Blind — works for buttons, icons, inventory slots, stats, skill trees.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TooltipComponent : UIComponent
    {
        [Export(PropertyHint.MultilineText)]
        public string TooltipText { get; set; } = "";
        [Export] public float ShowDelay { get; set; } = 0.5f;
        [Export] public Vector2 Offset { get; set; } = new(10, -10);
        [Export] public Color BgColor { get; set; } = new(0.05f, 0.05f, 0.1f, 0.9f);

        [Signal] public delegate void TooltipShownEventHandler();
        [Signal] public delegate void TooltipHiddenEventHandler();

        private Godot.Control? _control;
        private Panel? _tooltipPanel;
        private float _hoverTime;
        private bool _showing;
        private bool _hovering;

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent() as Godot.Control;
            if (_control != null)
            {
                _control.MouseEntered += OnMouseEntered;
                _control.MouseExited += HideTooltip;
            }
        }

        private void OnMouseEntered()
        {
            if (IsActive) { _hovering = true; _hoverTime = 0; }
        }

        public override void _Process(double delta)
        {
            // Gate on _hovering: without it, _hoverTime climbs from load with the mouse nowhere
            // near the control, and the tooltip pops on its own after ShowDelay seconds.
            if (!IsActive || !_hovering || _showing || string.IsNullOrEmpty(TooltipText)) return;
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
            _hovering = false;
            _hoverTime = 0;
            _showing = false;
            _tooltipPanel?.QueueFree();
            _tooltipPanel = null;
            EmitSignal(SignalName.TooltipHidden);
        }

        public override void _ExitTree()
        {
            _tooltipPanel?.QueueFree();
            if (_control != null)
            {
                _control.MouseEntered -= OnMouseEntered;
                _control.MouseExited -= HideTooltip;
            }
            base._ExitTree();
        }
    }
}
