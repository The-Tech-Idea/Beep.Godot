using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Tab group component. Attach to a Container with Button children as tabs.
    /// First button = tab headers, each maps to a sibling content panel.
    /// Content panels match tab order in the parent's children list.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TabGroupComponent : UIComponent
    {
        [Export] public int ActiveTab { get; set; } = 0;
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color ActiveTabColor => UiSurface.Semantic(this, UiSurface.Role.Accent);
        public Color InactiveTabColor => UiSurface.Ink(UiSurface.Of(this));
        [Export] public float SwitchDuration { get; set; } = 0.2f;

        [Signal] public delegate void TabChangedEventHandler(int tabIndex, string tabName);

        private Container? _tabBar;
        private Container? _contentArea;
        private readonly List<Button> _tabs = new();
        private readonly Dictionary<Button, Action> _tabHandlers = new();
        private readonly List<Godot.Control> _panels = new();
        private readonly List<Tween> _activeTweens = new();
        private int _currentTab = -1;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            var parent = GetParent();
            if (parent == null) return;

            // Find tab bar (first HBoxContainer child) and content area
            foreach (var child in parent.GetChildren())
            {
                if (child is Container c && _tabBar == null) _tabBar = c;
                else if (child is Container c2 && _tabBar != null) { _contentArea = c2; break; }
            }

            if (_tabBar == null)
            {
                GD.PushWarning($"[{Name}] TabGroupComponent needs its parent to hold two Container children (a tab bar then a content area); found none. Nothing will be tabbed.");
                return;
            }

            // Collect tab buttons and their content panels
            foreach (var child in _tabBar.GetChildren())
            {
                if (child is Button btn)
                    _tabs.Add(btn);
            }

            WireTabPressHandlers();

            if (_contentArea != null)
                foreach (var child in _contentArea.GetChildren())
                    if (child is Godot.Control ctrl) _panels.Add(ctrl);

            SwitchToTab(ActiveTab, true);
        }

        private void WireTabPressHandlers()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                int idx = i;
                Action handler = () => OnTabIndexPressed(idx);
                _tabHandlers[_tabs[i]] = handler;
                _tabs[i].Pressed += handler;
            }
        }

        private void OnTabIndexPressed(int idx) => SwitchToTab(idx);

        public void SwitchToTab(int index, bool instant = false)
        {
            if (index == _currentTab || index < 0 || index >= _tabs.Count || !IsActive) return;

            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();

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
                    _activeTweens.Add(t);
                    t.TweenProperty(newPanel, "modulate:a", 1f, SwitchDuration);
                }
                StyleTab(_tabs[index], true);
            }

            EmitSignal(SignalName.TabChanged, index, _tabs[index].Text);
        }

        private void AnimateOut(Godot.Control panel)
        {
            var t = panel.CreateTween();
            _activeTweens.Add(t);
            t.TweenProperty(panel, "modulate:a", 0f, SwitchDuration * 0.5f);
            t.Finished += () => OnPanelHideFinished(panel);
        }

        private void OnPanelHideFinished(Godot.Control panel) => panel.Visible = false;

        private void StyleTab(Button btn, bool active)
        {
            var sb = new StyleBoxFlat { BgColor = active ? ActiveTabColor : InactiveTabColor };
            sb.SetCornerRadiusAll(0);
            sb.BorderWidthBottom = active ? 3 : 0;
            sb.BorderColor = ActiveTabColor;
            KitChrome.SetStyleboxOverrideIfChanged(btn, "normal", sb);
            KitChrome.SetStyleboxOverrideIfChanged(btn, "hover", sb);
        }

        public override void _ExitTree()
        {
            base._ExitTree();

            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();

            foreach (var kv in _tabHandlers)
                if (GodotObject.IsInstanceValid(kv.Key))
                    kv.Key.Pressed -= kv.Value;
            _tabHandlers.Clear();
        }
    }
}
