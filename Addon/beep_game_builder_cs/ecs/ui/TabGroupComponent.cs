using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Tab group component. Attach to a Container with Button children as tabs.
    /// First button = tab headers, each maps to a sibling content panel.
    /// Content panels match tab order in the parent's children list.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TabGroupComponent : EntityComponent
    {
        [Export] public int ActiveTab { get; set; } = 0;
        [Export] public Color ActiveTabColor { get; set; } = new(0.2f, 0.4f, 0.8f, 1f);
        [Export] public Color InactiveTabColor { get; set; } = new(0.12f, 0.12f, 0.18f, 1f);
        [Export] public float SwitchDuration { get; set; } = 0.2f;

        [Signal] public delegate void TabChangedEventHandler(int tabIndex, string tabName);

        private Container? _tabBar;
        private Container? _contentArea;
        private readonly List<Button> _tabs = new();
        private readonly List<Control> _panels = new();
        private int _currentTab = -1;

        public override void _Ready()
        {
            base._Ready();
            var parent = GetParent();
            if (parent == null) return;

            // Find tab bar (first HBoxContainer child) and content area
            foreach (var child in parent.GetChildren())
            {
                if (child is Container c && _tabBar == null) _tabBar = c;
                else if (child is Container c2 && _tabBar != null) { _contentArea = c2; break; }
            }

            if (_tabBar == null) return;

            // Collect tab buttons and their content panels
            foreach (var child in _tabBar.GetChildren())
            {
                if (child is Button btn)
                {
                    int idx = _tabs.Count;
                    btn.Pressed += () => SwitchToTab(idx);
                    _tabs.Add(btn);
                }
            }

            if (_contentArea != null)
                foreach (var child in _contentArea.GetChildren())
                    if (child is Control ctrl) _panels.Add(ctrl);

            SwitchToTab(ActiveTab, true);
        }

        public void SwitchToTab(int index, bool instant = false)
        {
            if (index == _currentTab || index < 0 || index >= _tabs.Count || !IsActive) return;

            // Deactivate old
            if (_currentTab >= 0 && _currentTab < _panels.Count && _panels[_currentTab] != null)
            {
                var oldPanel = _panels[_currentTab];
                if (instant) oldPanel.Visible = false;
                else AnimateOut(oldPanel);
                StyleTab(_tabs[_currentTab], false);
            }

            // Activate new
            _currentTab = index;
            if (index < _panels.Count && _panels[index] != null)
            {
                var newPanel = _panels[index];
                newPanel.Visible = true;
                if (!instant)
                {
                    newPanel.Modulate = new Color(1, 1, 1, 0);
                    var t = newPanel.CreateTween();
                    t.TweenProperty(newPanel, "modulate:a", 1f, SwitchDuration);
                }
                StyleTab(_tabs[index], true);
            }

            EmitSignal(SignalName.TabChanged, index, _tabs[index].Text);
        }

        private void AnimateOut(Control panel)
        {
            var t = panel.CreateTween();
            t.TweenProperty(panel, "modulate:a", 0f, SwitchDuration * 0.5f);
            t.Finished += () => panel.Visible = false;
        }

        private void StyleTab(Button btn, bool active)
        {
            var sb = new StyleBoxFlat { BgColor = active ? ActiveTabColor : InactiveTabColor };
            sb.SetCornerRadiusAll(0);
            sb.BorderWidthBottom = active ? 3 : 0;
            sb.BorderColor = ActiveTabColor;
            btn.AddThemeStyleboxOverride("normal", sb);
            btn.AddThemeStyleboxOverride("hover", sb);
        }
    }
}
