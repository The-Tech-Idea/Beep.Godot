using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Makes a HUD panel collapsible via a small floating toggle pinned to the panel's corner.
    ///
    /// Why every HUD panel needs this, not just the big ones: a HUD competes with the game for
    /// the same pixels. A build toolbar, a quest log, a minimap and a resource strip are each
    /// useful *some* of the time, and permanently occupying the screen for the rest of it is
    /// how HUDs end up feeling cluttered. Letting the player fold what they are not using is
    /// the standard answer across every genre — and it costs the developer nothing here,
    /// because this attaches to a panel that already exists rather than replacing it.
    ///
    /// Attach as a CHILD of the panel you want to collapse. The component finds its parent
    /// <see cref="Godot.Control"/> and floats a chevron button over its top-right corner.
    ///
    /// Floating rather than a title bar, for two reasons. It is what games use — a HUD panel
    /// folds from a corner chevron, not from a desktop-style header row. And it is the only
    /// form that works everywhere: a full-width header only lands correctly inside a
    /// VBoxContainer, whereas a floating button is positioned from the panel's own rect and so
    /// works over a panel in an HBox, a bare Control, or anything else. The button lives on the
    /// CanvasLayer's top-level Control so no container can reflow it and the panel's node path
    /// is never disturbed.
    ///
    /// State persists through <see cref="ISaveable"/>: a player who folded the build bar
    /// expects it folded after a reload, and a HUD that silently reopens everything on load
    /// is the thing this is meant to avoid.
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

        /// <summary>Floating toggle size and its inset from the panel corner, in DESIGN pixels
        /// (see BeepProjectDefaults.DesignWidth) — the canvas_items stretch scales them to the
        /// player's resolution, so these stay correct at 720p and 4K alike.</summary>
        /// <summary>Chevron size, from the theme font: a fixed 22px button is a thumbnail
        /// beside 24pt type and oversized beside 14pt.</summary>
        private float ButtonSize => UiSurface.FontSize(this) * 1.6f;
        private const float Inset = 4f;

        private Godot.Control? _panel;      // the parent being folded
        private Button? _header;            // the floating toggle
        private VBoxContainer? _wrapper;
        private bool _collapsed;
        private float _expandedMin = -1f;
        private Rect2 _anchor;              // last known panel rect, so the toggle survives the fold
        private Tween? _tween;

        public bool IsCollapsed => _collapsed;

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

            // Remember the natural height BEFORE folding, or expanding restores to zero.
            _expandedMin = _panel.CustomMinimumSize.Y > 0 ? _panel.CustomMinimumSize.Y : _panel.Size.Y;

            BuildHeader();
            SetCollapsed(StartCollapsed, animate: false);
        }

        /// <summary>Build the floating toggle. It must NOT be a child of the panel — a child is
        /// folded away with everything else, leaving no way to unfold.</summary>
        private void BuildHeader()
        {
            if (_panel?.GetParent() is not Godot.Control host) return;

            _header = new Button
            {
                Name = $"{_panel.Name}Toggle",
                Text = HeaderText(false),
                ToggleMode = false,
                FocusMode = Godot.Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(ButtonSize, ButtonSize),
                Size = new Vector2(ButtonSize, ButtonSize),
                Alignment = HorizontalAlignment.Center,
                TooltipText = string.IsNullOrEmpty(Title) ? "Collapse this panel" : $"Collapse {Title}",
            };
            _header.AddThemeFontSizeOverride("font_size", UiSurface.FontSize(this, 0.86f));
            _header.Pressed += () => SetCollapsed(!_collapsed, animate: true);

            // Floating, positioned over the panel's own corner each frame — not a row in the
            // host's layout. That is what games actually use, and it also removes the host
            // constraint the previous header bar had: a full-width title bar only lands
            // correctly in a VBoxContainer, whereas a floating button works over a panel in an
            // HBox, a bare Control or anything else, because the host never lays it out.
            //
            // It is added to the CanvasLayer's top-level Control rather than to the host, so no
            // container can reflow it and the panel's own node path is untouched. (Reparenting
            // the panel under a wrapper was tried and reverted — it silently broke
            // CityBuilderHudComponent.DemandMeterPath.)
            var layer = TopLevelControl(host);
            (layer ?? host).AddChild(_header);
            _header.TopLevel = layer == null;
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
                box.ContentMarginLeft = box.ContentMarginRight = 2;
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

            // While expanded, track the live rect. While collapsed the panel may be hidden and
            // its rect stale or zero, so the button holds the last good position — otherwise it
            // would jump to the origin and the player would lose the way to unfold.
            if (!_collapsed && _panel.Size.Y > 1f)
                _anchor = new Rect2(_panel.GlobalPosition, _panel.Size);

            // Centred ON the panel's top-right corner, so it hangs half outside the border.
            // Every kit in Example_Art/gameui4,5,7 places the close chip this way, and it is
            // what distinguishes a game UI chip from a toolbar button tucked inside a rect: the
            // overlap is what makes the two pieces read as one assembled object.
            var pos = new Vector2(_anchor.Position.X + _anchor.Size.X - ButtonSize * 0.5f,
                                  _anchor.Position.Y - ButtonSize * 0.5f);
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
            if (_header != null) _header.Text = HeaderText(collapsed);

            float target = collapsed ? 0f : _expandedMin;

            _tween?.Kill();
            if (!animate || AnimSeconds <= 0f)
            {
                Apply(target);
                EmitSignal(SignalName.Toggled, collapsed);
                return;
            }

            // Height is tweened rather than toggling Visible, so neighbouring controls in the
            // container reflow smoothly instead of snapping.
            _tween = CreateTween();
            _tween.TweenMethod(Callable.From<float>(Apply),
                               _panel.CustomMinimumSize.Y, target, AnimSeconds)
                  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            _tween.TweenCallback(Callable.From(() => EmitSignal(SignalName.Toggled, collapsed)));
        }

        private void Apply(float height)
        {
            if (_panel == null || !GodotObject.IsInstanceValid(_panel)) return;
            _panel.CustomMinimumSize = new Vector2(_panel.CustomMinimumSize.X, height);

            // Hide the CONTENT rather than the panel wherever the panel has children, so a
            // folded panel leaves a thin plate the floating toggle can sit on — which is how a
            // collapsed HUD panel reads in a game, and what tells the player where it went.
            // A self-drawing panel (a meter, a map) has no child controls to hide, so there the
            // panel itself goes; the toggle keeps its last position via _anchor.
            bool showContent = height > 0.5f;
            bool hasChildControls = false;
            foreach (var child in _panel.GetChildren())
            {
                if (child is not Godot.Control cc || cc == _header) continue;
                hasChildControls = true;
                cc.Visible = showContent;
            }
            _panel.Visible = showContent || hasChildControls;
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
