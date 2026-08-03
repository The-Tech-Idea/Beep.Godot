using Godot;
using Beep.ECS.UI.Kit;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Makes a screen-edge HUD block collapsible with a floating kit toggle.
    /// Collapse moves the whole widget off the nearest screen edge. It does not resize the
    /// widget and it does not hide the widget's child content.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CollapsiblePanelComponent : UIComponent, ISaveable
    {
        /// <summary>Text shown on the header bar. Empty uses the parent panel's node name.</summary>
        [Export] public string Title { get; set; } = "";

        /// <summary>Start folded. Overridden by saved state when a save is loaded.</summary>
        [Export] public bool StartCollapsed { get; set; } = false;

        /// <summary>Input action that toggles this panel. Empty = click only.</summary>
        [Export] public string ToggleAction { get; set; } = "";

        /// <summary>Fold animation duration. 0 snaps.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float AnimSeconds { get; set; } = 0.18f;

        /// <summary>Persist the folded state into the save file.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = true;

        /// <summary>Key under which the state is saved. Empty derives one from the node path,
        /// which is stable as long as the scene structure is.</summary>
        [Export] public string SaveKey { get; set; } = "";

        [Signal] public delegate void ToggledEventHandler(bool collapsed);

        /// <summary>Chevron size, from the theme font: a fixed 22px button is a thumbnail
        /// beside 24pt type and oversized beside 14pt.</summary>
        private float ButtonSize => Mathf.Clamp(UiSurface.FontSize(this) * 1.28f, 18f, 26f);

        private Godot.Control? _panel;      // the parent being folded
        private Button? _header;            // the floating toggle
        private bool _collapsed;
        private Rect2 _anchor;              // last known panel rect, so the toggle survives the fold
        private Tween? _tween;
        private Vector2 _expandedPosition;
        private Vector2 _collapsedPosition;
        private CollapseEdge _edge = CollapseEdge.Left;
        private Godot.Control.MouseFilterEnum _expandedMouseFilter;
        private readonly List<Godot.Control> _linkedFrames = new();

        public bool IsCollapsed => _collapsed;

        private enum CollapseEdge
        {
            Left,
            Right,
            Top,
            Bottom
        }

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            // Saves are built from this group, not from a tree walk — without joining it the
            // component implements ISaveable and is never asked to save anything.
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
            // Deferred for the same reason as the other HUD components: AddChild against a
            // parent still inside its own _Ready fails with "parent node is busy setting up
            // children" and silently yields an empty widget.
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            // `as`, not GetParent<Control>(): the generic form THROWS on a mismatch, so a
            // component attached under a Node-derived host (ToastNotificationComponent, for
            // one) killed Setup with an unhandled InvalidCastException instead of taking the
            // warning path five lines below that exists for exactly this case.
            _panel = GetParent() as Godot.Control;
            if (_panel == null)
            {
                GD.PushWarning($"[{Name}] CollapsiblePanelComponent's parent "
                             + $"('{GetParent()?.Name}', {GetParent()?.GetType().Name}) is not a Control — "
                             + "there is no panel rect to fold, so this component does nothing. "
                             + "Attach it under the Control that draws the panel.");
                return;
            }

            _anchor = new Rect2(_panel.GlobalPosition, _panel.Size);
            _expandedPosition = _panel.Position;
            _expandedMouseFilter = _panel.MouseFilter;
            ResolveLinkedFrames();
            BuildHeader();
            SetCollapsed(StartCollapsed, animate: false);
        }

        private void ResolveLinkedFrames()
        {
            _linkedFrames.Clear();
            if (_panel?.GetParent() is not Node parent) return;
            foreach (Node child in parent.GetChildren())
            {
                if (child == _panel || child is not KitPanel frame || frame.TargetPath.IsEmpty) continue;
                if (frame.GetNodeOrNull<Godot.Control>(frame.TargetPath) == _panel)
                    _linkedFrames.Add(frame);
            }
        }

        /// <summary>Build the floating toggle. It must not be a child of the panel; otherwise it
        /// disappears with the panel and there is no way to unfold.</summary>
        private void BuildHeader()
        {
            if (_panel == null) return;
            Node? parent = _panel.GetParent();
            if (parent == null) return;

            _header = new KitIconButton
            {
                Name = $"{_panel.Name}Toggle",
                Glyph = HeaderText(false),
                ToggleMode = false,
                FocusMode = Godot.Control.FocusModeEnum.None,
                MouseFilter = Godot.Control.MouseFilterEnum.Stop,
                CustomMinimumSize = new Vector2(ButtonSize, ButtonSize),
                Size = new Vector2(ButtonSize, ButtonSize),
                Alignment = HorizontalAlignment.Center,
                TooltipText = string.IsNullOrEmpty(Title) ? "Collapse this panel" : $"Collapse {Title}",
            };
            _header.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(ButtonSize * 0.58f));
            _header.Pressed += () => SetCollapsed(!_collapsed, animate: true);

            var layer = TopLevelControl(parent);
            (layer ?? parent).AddChild(_header);
            _header.TopLevel = layer == null && parent is not CanvasLayer;
            _header.ZIndex = Mathf.Max(_panel.ZIndex + 200, 200);
            CallDeferred(nameof(CompactToggleStyle));
        }

        /// <summary>Strip the panel-button padding off the floating toggle.
        ///
        /// It inherits the HUD Button theme, whose content margins are sized for a labelled
        /// panel button (14px sides, plus the extra top margin that clears the sci-fi art's
        /// baked header band). On a 22px square that leaves no room for the glyph at all — the
        /// button drew and the chevron did not. Deferred so the node is in the tree and its
        /// theme has resolved.</summary>
        private void CompactToggleStyle()
        {
            if (_header == null || !GodotObject.IsInstanceValid(_header)) return;
            foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            {
                if (!_header.HasThemeStylebox(state, "Button")) continue;
                if (_header.GetThemeStylebox(state, "Button").Duplicate() is not StyleBox box) continue;
                box.ContentMarginLeft = box.ContentMarginRight = 1;
                box.ContentMarginTop = box.ContentMarginBottom = 1;
                _header.AddThemeStyleboxOverride(state, box);
            }
        }

        /// <summary>The outermost Control under this HUD's CanvasLayer — a float host that no
        /// container will lay out.</summary>
        private static Godot.Control? TopLevelControl(Node from)
        {
            Godot.Control? best = null;
            for (Node? n = from; n != null; n = n.GetParent())
            {
                if (n is Godot.Control c) best = c;
                if (n is CanvasLayer) break;
            }
            return best;
        }

        /// <summary>Icon only. A floating toggle carries no title — the panel beneath it already
        /// says what it is, and a label would force the button wide enough to cover content.</summary>
        private static string HeaderText(bool collapsed) => collapsed ? "▸" : "▾";

        /// <summary>Keep the floating toggle pinned to the panel's top-right corner.
        ///
        /// Driven per-frame from the panel's rect rather than set once: the panel is
        /// container-managed and anchored, so its rect moves whenever the window resizes, a
        /// neighbour folds, or the canvas rescales for a different resolution.</summary>
        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || _header == null || !GodotObject.IsInstanceValid(_header)) return;
            if (_panel == null || !GodotObject.IsInstanceValid(_panel)) return;

            // While expanded, track the live rect. While collapsed the panel is off-screen, so
            // the toggle holds the last good on-screen anchor.
            if (!_collapsed && _panel.Size.X > 1f && _panel.Size.Y > 1f)
            {
                _anchor = WidgetRect();
                _expandedPosition = _panel.Position;
                _edge = NearestEdge(_anchor);
            }

            var pos = TogglePosition();
            _header.GlobalPosition = pos;
            _header.Size = new Vector2(ButtonSize, ButtonSize);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Engine.IsEditorHint() || string.IsNullOrEmpty(ToggleAction)) return;
            if (!InputMap.HasAction(ToggleAction) || !@event.IsActionPressed(ToggleAction)) return;
            SetCollapsed(!_collapsed, animate: true);
            GetViewport()?.SetInputAsHandled();
        }

        /// <summary>Fold or unfold. Public so a hotkey, a tutorial step or a screen-space
        /// budget rule can drive it.</summary>
        public void SetCollapsed(bool collapsed, bool animate = true)
        {
            if (_panel == null) return;
            _collapsed = collapsed;
            if (_header is KitIconButton icon) icon.Glyph = HeaderText(collapsed);
            else if (_header != null) _header.Text = HeaderText(collapsed);

            _tween?.Kill();
            if (collapsed)
            {
                _anchor = WidgetRect();
                _expandedPosition = _panel.Position;
                _edge = NearestEdge(_anchor);
                _collapsedPosition = CollapsedPosition();
            }

            Vector2 target = collapsed ? _collapsedPosition : _expandedPosition;
            if (!animate || AnimSeconds <= 0f)
            {
                Apply(target, collapsed);
                EmitSignal(SignalName.Toggled, collapsed);
                return;
            }

            _tween = CreateTween();
            _tween.TweenMethod(Callable.From<Vector2>(p => Apply(p, collapsed)),
                               _panel.Position, target, AnimSeconds)
                  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            _tween.TweenCallback(Callable.From(() =>
            {
                Apply(target, collapsed);
                EmitSignal(SignalName.Toggled, collapsed);
            }));
        }

        private void Apply(Vector2 position, bool collapsed)
        {
            if (_panel == null || !GodotObject.IsInstanceValid(_panel)) return;
            Vector2 delta = position - _panel.Position;
            _panel.Position = position;
            _panel.Visible = true;
            _panel.Modulate = _panel.Modulate with { A = 1f };
            _panel.MouseFilter = collapsed ? Godot.Control.MouseFilterEnum.Ignore : _expandedMouseFilter;
            foreach (var linked in _linkedFrames)
            {
                if (!GodotObject.IsInstanceValid(linked)) continue;
                linked.Position += delta;
                linked.Visible = true;
                linked.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
            }
        }

        private Rect2 WidgetRect()
        {
            if (_panel == null) return _anchor;
            Rect2 rect = new(_panel.GlobalPosition, _panel.Size);
            foreach (var linked in _linkedFrames)
            {
                if (!GodotObject.IsInstanceValid(linked) || linked.Size.X <= 1f || linked.Size.Y <= 1f) continue;
                rect = rect.Merge(new Rect2(linked.GlobalPosition, linked.Size));
            }
            return rect;
        }

        private CollapseEdge NearestEdge(Rect2 rect)
        {
            Vector2 viewport = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
            Vector2 center = rect.GetCenter();
            float left = center.X;
            float right = Mathf.Max(0f, viewport.X - center.X);
            float top = center.Y;
            float bottom = Mathf.Max(0f, viewport.Y - center.Y);
            float nearest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));

            if (Mathf.IsEqualApprox(nearest, left)) return CollapseEdge.Left;
            if (Mathf.IsEqualApprox(nearest, right)) return CollapseEdge.Right;
            if (Mathf.IsEqualApprox(nearest, bottom)) return CollapseEdge.Bottom;
            return CollapseEdge.Top;
        }

        private Vector2 CollapsedPosition()
        {
            if (_panel == null) return _expandedPosition;
            Vector2 viewport = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
            Vector2 delta = _edge switch
            {
                CollapseEdge.Left => new Vector2(-_anchor.End.X - ButtonSize, 0f),
                CollapseEdge.Right => new Vector2(viewport.X - _anchor.Position.X + ButtonSize, 0f),
                CollapseEdge.Top => new Vector2(0f, -_anchor.End.Y - ButtonSize),
                CollapseEdge.Bottom => new Vector2(0f, viewport.Y - _anchor.Position.Y + ButtonSize),
                _ => Vector2.Zero,
            };
            return _panel.Position + delta;
        }

        private Vector2 TogglePosition()
        {
            if (_header == null) return _anchor.Position;
            Vector2 viewport = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
            Vector2 center = _anchor.GetCenter();
            if (_collapsed)
            {
                return _edge switch
                {
                    CollapseEdge.Left => new Vector2(2f, center.Y - ButtonSize * 0.5f),
                    CollapseEdge.Right => new Vector2(viewport.X - ButtonSize - 2f, center.Y - ButtonSize * 0.5f),
                    CollapseEdge.Top => new Vector2(center.X - ButtonSize * 0.5f, 2f),
                    CollapseEdge.Bottom => new Vector2(center.X - ButtonSize * 0.5f, viewport.Y - ButtonSize - 2f),
                    _ => _anchor.Position,
                };
            }

            return _edge switch
            {
                CollapseEdge.Left => new Vector2(_anchor.Position.X - ButtonSize * 0.5f, center.Y - ButtonSize * 0.5f),
                CollapseEdge.Right => new Vector2(_anchor.End.X - ButtonSize * 0.5f, center.Y - ButtonSize * 0.5f),
                CollapseEdge.Bottom => new Vector2(center.X - ButtonSize * 0.5f, _anchor.End.Y - ButtonSize * 0.5f),
                _ => new Vector2(center.X - ButtonSize * 0.5f, _anchor.Position.Y - ButtonSize * 0.5f),
            };
        }

        public void Toggle() => SetCollapsed(!_collapsed, animate: true);

        // ── ISaveable ────────────────────────────────────────────────────────────────
        // Keyed on the PANEL, not on this component: one collapsible per panel, and the panel's
        // name is what stays stable if the component is renamed or re-added.
        private string Key => string.IsNullOrEmpty(SaveKey)
            ? $"hud.collapsed.{_panel?.Name.ToString() ?? Name.ToString()}"
            : $"hud.collapsed.{SaveKey}";

        public void Save(GameBuilder.GameStateData state)
        {
            if (!ParticipatesInSave) return;
            state.GameData[Key] = _collapsed;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (!ParticipatesInSave) return;
            if (state.GameData.TryGetValue(Key, out var v))
                SetCollapsed(v.AsBool(), animate: false);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _tween?.Kill();
            _tween = null;
        }
    }
}
