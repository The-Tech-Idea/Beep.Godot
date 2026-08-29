using Godot;
using Beep.ECS.UI.Kit;

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
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color BgColor => UiSurface.Of(this);
        [Signal] public delegate void TooltipShownEventHandler();
        [Signal] public delegate void TooltipHiddenEventHandler();

        private Godot.Control? _control;
        private KitTooltip? _tooltipPanel;
        private float _hoverTime;
        private bool _showing;
        private bool _hovering;

        public override void _Ready()
        {
            base._Ready();
            SetProcess(false);
            if (Engine.IsEditorHint())
                return;

            _control = GetParent() as Godot.Control;
            if (_control == null)
                GD.PushWarning($"[{Name}] TooltipComponent needs a Control parent to show a tooltip for; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the hovered Control.");
            if (_control != null)
            {
                _control.MouseEntered += OnMouseEntered;
                _control.MouseExited += HideTooltip;
            }
        }

        private void OnMouseEntered()
        {
            if (!IsActive || string.IsNullOrEmpty(TooltipText))
                return;

            _hovering = true;
            _hoverTime = 0;
            SetProcess(true);
        }

        public override void _Process(double delta)
        {
            if (_showing)
            {
                if (!IsActive || _control == null || !GodotObject.IsInstanceValid(_control) || !_control.IsVisibleInTree())
                    HideTooltip();
                return;
            }

            // Gate on _hovering: without it, _hoverTime climbs from load with the mouse nowhere
            // near the control, and the tooltip pops on its own after ShowDelay seconds.
            if (!IsActive || !_hovering || _control == null || !GodotObject.IsInstanceValid(_control) || !_control.IsVisibleInTree() || string.IsNullOrEmpty(TooltipText))
            {
                SetProcess(false);
                return;
            }
            if (_hoverTime < ShowDelay) { _hoverTime += (float)delta; return; }

            ShowTooltip();
        }

        private void ShowTooltip()
        {
            if (_control == null) return;
            _showing = true;
            SetProcess(true);

            // TopLevel so an absolutely-positioned popup isn't re-laid-out (and mispositioned) when
            // the control's parent is a Container.
            int fs = UiSurface.FontSize(this);
            _tooltipPanel = new KitTooltip
            {
                Text = TooltipText,
                Tail = KitTooltip.TailSide.Top,
                TailOffset = 0.18f,
                TopLevel = true,
                CustomMinimumSize = new Vector2(Mathf.Max(fs * 8f, TooltipText.Length * fs * 0.42f), fs * 2.8f),
                Size = new Vector2(Mathf.Max(fs * 8f, TooltipText.Length * fs * 0.42f), fs * 2.8f)
            };

            _tooltipPanel.Position = _control.GetGlobalMousePosition() + Offset + new Vector2(0, 20);
            _control.GetParent()?.AddChild(_tooltipPanel);
            _tooltipPanel.ZIndex = 100;

            EmitSignal(SignalName.TooltipShown);
        }

        private void HideTooltip()
        {
            bool hadTooltipState = _hovering || _showing;
            _hovering = false;
            _hoverTime = 0;
            _showing = false;
            SetProcess(false);
            _tooltipPanel?.QueueFree();
            _tooltipPanel = null;
            if (hadTooltipState)
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
