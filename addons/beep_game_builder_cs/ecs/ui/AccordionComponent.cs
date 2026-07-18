using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Accordion / collapsible section. Attach to a VBoxContainer with a Button header + content.
    /// Blind — works for settings panels, FAQ sections, collapsible menus.
    /// First child = header (Button), rest = content (collapses).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AccordionComponent : UIComponent
    {
        [Export] public bool StartExpanded { get; set; } = false;
        [Export] public float AnimationDuration { get; set; } = 0.3f;
        [Export] public string ExpandedIcon { get; set; } = "▼";
        [Export] public string CollapsedIcon { get; set; } = "▶";

        [Signal] public delegate void ExpandedEventHandler();
        [Signal] public delegate void CollapsedEventHandler();

        private Container? _container;
        private Button? _header;
        private bool _isExpanded;
        private readonly System.Collections.Generic.List<Tween> _activeTweens = new();

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _container = GetParent() as Container;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] AccordionComponent needs a Container parent to lay out sections; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to a VBoxContainer.");
                return;
            }

            var children = _container.GetChildren();
            if (children.Count == 0) return;

            // First child is the header button
            _header = children[0] as Button;
            if (_header != null)
            {
                _header.Pressed += Toggle;
                UpdateHeaderText();
            }
            else
            {
                GD.PushWarning($"[{Name}] AccordionComponent's first child is '{children[0].GetType().Name}', not a Button — there's no header to toggle, so the section can't be expanded/collapsed by click. Make the first child a Button.");
            }

            if (!StartExpanded) SetExpanded(false, true);
            else _isExpanded = true;
        }

        public void Toggle()
        {
            if (!IsActive) return;
            SetExpanded(!_isExpanded);
        }

        public void SetExpanded(bool expand, bool instant = false)
        {
            if (_container == null) return;
            _isExpanded = expand;
            UpdateHeaderText();

            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();

            var children = _container.GetChildren();
            for (int i = 1; i < children.Count; i++)
            {
                if (children[i] is not Godot.Control ctrl) continue;

                if (instant)
                {
                    ctrl.Visible = expand;
                    ctrl.Modulate = new Color(1, 1, 1, expand ? 1 : 0);
                }
                else
                {
                    ctrl.Visible = true;
                    var tween = ctrl.CreateTween().SetParallel(true);
                    _activeTweens.Add(tween);

                    if (expand)
                    {
                        ctrl.Modulate = new Color(1, 1, 1, 0);
                        ctrl.Scale = new Vector2(1, 0);
                        tween.TweenProperty(ctrl, "modulate:a", 1f, AnimationDuration);
                        tween.TweenProperty(ctrl, "scale", Vector2.One, AnimationDuration)
                            .SetEase(Tween.EaseType.Out);
                    }
                    else
                    {
                        tween.TweenProperty(ctrl, "modulate:a", 0f, AnimationDuration * 0.5f);
                        tween.TweenProperty(ctrl, "scale", new Vector2(1, 0), AnimationDuration)
                            .SetEase(Tween.EaseType.In);
                        tween.Finished += () => OnCollapseFinished(ctrl);
                    }
                }
            }

            EmitSignal(expand ? SignalName.Expanded : SignalName.Collapsed);
        }

        private void OnCollapseFinished(Godot.Control ctrl) => ctrl.Visible = false;

        private void UpdateHeaderText()
        {
            if (_header == null) return;
            string icon = _isExpanded ? ExpandedIcon : CollapsedIcon;
            string text = _header.Text.TrimStart('▶', '▼', ' ').Trim();
            _header.Text = $"{icon} {text}";
        }

        public override void _ExitTree()
        {
            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();

            if (_header != null)
                _header.Pressed -= Toggle;
        }
    }
}
