using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Fully themed dialog box. Attach as a child of any Control (typically on a
    /// CanvasLayer). Discovers the nearest <see cref="ThemePresetComponent"/> ancestor
    /// and uses its colors (AccentSecondary for speaker name, TextPrimary for body text,
    /// themed PanelContainer for the frame). If no theme is found, falls back to Godot
    /// defaults.
    ///
    /// Features:
    /// • Themed panel frame (BgPanel/BorderNormal/ShadowColor via PanelContainer).
    /// • Speaker name in AccentSecondary; body text in TextPrimary.
    /// • Typewriter text reveal with configurable speed.
    /// • Choice buttons (full button theming — hover/press/ripple from the theme).
    /// • Choice-stagger entrance animation (each button fades in sequentially).
    /// • Slide-in/fade entry; slide-out/fade exit.
    /// • Pulsing ▼ continue indicator.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DialogUIComponent : UIComponent
    {
        public struct DialogLine
        {
            public string Speaker;
            public string Text;
            public string[] Choices; // null/empty = no choice, advance on input
        }

        public enum DialogPosition { Bottom, Center, Top }

        // ── Exports ──
        [ExportGroup("Behavior")]
        [Export] public string AdvanceAction { get; set; } = "interact";
        [Export] public float TypewriterSpeed { get; set; } = 30f;
        /// <summary>Optional DialogComponent whose <c>DialogStarted</c> signal drives this UI.
        /// When set, the two halves auto-connect (DialogStarted → StartFromDialogComponent) so
        /// the engine and the box work together with no hand-wiring — this was the missing link
        /// that left dialog_template.tscn showing nothing when its engine ran.</summary>
        [Export] public NodePath DialogEnginePath { get; set; } = new("");

        [ExportGroup("Layout")]
        [Export] public DialogPosition Position { get; set; } = DialogPosition.Bottom;
        [Export] public Vector2 DialogSize { get; set; } = new(600, 160);
        [Export] public int ContentPadding { get; set; } = 16;
        [Export] public NodePath DialogPanelPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [ExportGroup("Animation")]
        [Export] public float EntryDuration { get; set; } = 0.3f;
        [Export] public float ChoiceStaggerDelay { get; set; } = 0.06f;
        [Export] public bool ShowContinueIndicator { get; set; } = true;

        [ExportGroup("Colors")]
        /// <summary>Override for speaker-name color. If unset (alpha=0), uses AccentSecondary from theme.</summary>
        [Export] public Color SpeakerColorOverride { get; set; } = new(0, 0, 0, 0);

        [Signal] public delegate void DialogFinishedEventHandler();
        [Signal] public delegate void ChoiceSelectedEventHandler(int index);

        // ── Internal state ──
        private KitDialogBox? _panel;
        private bool _createdPanel;
        private DialogLine[] _lines = System.Array.Empty<DialogLine>();
        private int _lineIndex;
        private double _charTimer;
        private bool _typewriterDone;
        private bool _showingChoices;
        private double _pulseTime;
        private Color _cachedAccent;
        private Color _cachedTextPrimary;
        private bool _themeFound;
        private Tween? _animationTween;
        private readonly System.Collections.Generic.List<Tween> _choiceTweens = new();

        // The DialogComponent driving this UI (resolved from DialogEnginePath), so we can
        // unsubscribe on exit.
        private Beep.ECS.DialogComponent? _engine;

        public override void _Ready()
        {
            base._Ready();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(SetupPanel));
            if (!Engine.IsEditorHint())
                ConnectDialogEngine();
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && FindDialogPanel() == null)
                return new[] { "Add an authored KitDialogBox named DialogPanel, set DialogPanelPath, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        private void ConnectDialogEngine()
        {
            if (DialogEnginePath.IsEmpty) return;
            _engine = GetNodeOrNull<Beep.ECS.DialogComponent>(DialogEnginePath);
            if (_engine == null)
            {
                GD.PushWarning($"[{Name}] DialogEnginePath '{DialogEnginePath}' did not resolve to a DialogComponent — the box won't be driven by an engine.");
                return;
            }
            // Signal shapes match: DialogStarted(string speaker, string[] lines) → StartFromDialogComponent.
            _engine.DialogStarted += StartFromDialogComponent;
        }

        // ════════════════════════════════════════════════════════════════
        // Layout construction
        // ════════════════════════════════════════════════════════════════

        private void SetupPanel()
        {
            if (!BindExistingPanel())
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                BuildGeneratedPanel();
            }

            if (_panel == null)
                return;

            _panel.ChoiceSelected += OnChoiceSelected;
            ApplyAnchors();
            DiscoverTheme();
            if (Engine.IsEditorHint())
            {
                _panel.Speaker = "Speaker";
                _panel.Body = "Dialog preview text";
                _panel.VisibleCharacters = -1;
                _panel.ChoicesVisible = false;
                _panel.ContinueVisible = ShowContinueIndicator;
                _panel.Visible = true;
            }
            else
            {
                _panel.Visible = false;
            }
        }

        private void BuildGeneratedPanel()
        {
            if (GetParent() is not Godot.Control parent)
            {
                // Same silent-cast trap as ThemePresetComponent/AnimatedMenuComponent: this
                // builds its UI under GetParent(), so a non-Control parent leaves _panel null
                // and every entry point (Start, _Process, _UnhandledInput) quietly bails.
                // dialog_template.tscn parents it at its CanvasLayer root, so it is inert.
                if (!Engine.IsEditorHint())
                    GD.PushWarning($"[{Name}] DialogUIComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a Control — no dialog UI will be built. Reparent it under a Control.");
                return;
            }

            _createdPanel = true;
            _panel = new KitDialogBox { Name = "DialogPanel" };
            parent.AddChild(_panel);
            SetEditedOwner(_panel);

            // The entry animation tweens _panel.position, which a CONTAINER parent would
            // overwrite on its next layout pass — the dialog would simply snap into place with no
            // animation and nothing logged. The parent is checked for Control above; a Container
            // IS a Control, so that check passes and this one is still needed.
            if (parent is Godot.Container)
                GD.PushWarning($"[{Name}] DialogUIComponent's parent '{parent.Name}' is a "
                             + $"{parent.GetType().Name}, which positions its own children — the "
                             + "entry animation tweens position and will be overwritten every "
                             + "layout pass. Parent the dialog to a plain Control, or animate it "
                             + "with offset_transform_position instead.");

            // AnimateIn/AnimateOut tween _panel.Position; a layout Container host re-sorts its
            // children every layout pass and would overwrite the slide. Warn once (this runs
            // just once, at build) — recommend a CanvasLayer/free Control host.
            if (!Engine.IsEditorHint() && parent is Container)
                GD.PushWarning($"[{Name}] DialogUIComponent's host is a {parent.GetType().Name} — it will re-sort the dialog panel and overwrite the slide-in/out animation. Host the dialog under a CanvasLayer or a free (non-Container) Control.");
        }

        private bool BindExistingPanel()
        {
            _createdPanel = false;
            _panel = FindDialogPanel();
            return _panel != null;
        }

        public bool UsesSceneControls()
            => FindDialogPanel() != null;

        private KitDialogBox? FindDialogPanel()
        {
            if (!DialogPanelPath.IsEmpty && GetNodeOrNull<KitDialogBox>(DialogPanelPath) is { } pathPanel)
                return pathPanel;

            if (FindChild("DialogPanel", recursive: true, owned: false) is KitDialogBox childPanel)
                return childPanel;

            return GetParent()?.FindChild("DialogPanel", recursive: true, owned: false) as KitDialogBox;
        }

        private void ApplyAnchors()
        {
            if (_panel == null) return;
            _panel.CustomMinimumSize = DialogSize;
            switch (Position)
            {
                case DialogPosition.Bottom:
                    _panel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
                    _panel.OffsetBottom = -20;
                    break;
                case DialogPosition.Center:
                    _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
                    break;
                case DialogPosition.Top:
                    _panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
                    _panel.OffsetTop = 20;
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Theme discovery
        // ════════════════════════════════════════════════════════════════

        private void DiscoverTheme()
        {
            // Walk up ancestors to find a ThemePresetComponent.
            Node? n = GetParent();
            while (n != null)
            {
                foreach (var child in n.GetChildren())
                {
                    if (child is ThemePresetComponent tpc)
                    {
                        _themeFound = true;
                        // Read colors from the file-based skin catalog.
                        var themeDef = Beep.ECS.UI.SkinCatalog.GetTheme(tpc.GenreName, tpc.PresetName);
                        if (themeDef != null)
                        {
                            _cachedAccent = themeDef.Colors.AccentSecondary;
                            _cachedTextPrimary = themeDef.Colors.TextPrimary;
                        }
                        return;
                    }
                }
                n = n.GetParent();
            }
            // No theme found — use Godot defaults (plain white). Announce it, so an unthemed
            // dialog box reads as a wiring gap rather than an intentional look.
            if (!Engine.IsEditorHint())
                GD.PushWarning($"[{Name}] DialogUIComponent found no ThemePresetComponent up the tree — the dialog falls back to unthemed white. Add a ThemePresetComponent to the dialog's content Control to theme it.");
            _themeFound = false;
            _cachedAccent = Colors.White;
            _cachedTextPrimary = Colors.White;
        }

        // ════════════════════════════════════════════════════════════════
        // Public API
        // ════════════════════════════════════════════════════════════════

        public void Start(DialogLine[] lines)
        {
            if (!IsActive || _panel == null) return;
            _lines = lines;
            _lineIndex = 0;
            AnimateIn();
            CallDeferred(nameof(ShowLineDeferred));
        }

        /// <summary>Convenience adapter: start from a DialogComponent's (speaker, lines).</summary>
        public void StartFromDialogComponent(string speaker, string[] lines)
        {
            var dialogLines = new DialogLine[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                dialogLines[i] = new DialogLine { Speaker = speaker, Text = lines[i] };
            Start(dialogLines);
        }

        private void ShowLineDeferred() => ShowLine();

        // ════════════════════════════════════════════════════════════════
        // Line display + typewriter
        // ════════════════════════════════════════════════════════════════

        private void ShowLine()
        {
            if (_lineIndex >= _lines.Length)
            {
                AnimateOut();
                return;
            }

            var line = _lines[_lineIndex];

            if (_panel != null)
            {
                _panel.Speaker = line.Speaker;
                _panel.Body = line.Text;
                _panel.VisibleCharacters = 0;
                _panel.ChoicesVisible = false;
                _panel.ContinueVisible = ShowContinueIndicator;
            }

            _charTimer = 0;
            _typewriterDone = false;

            // Continue indicator.
            _showingChoices = false;
        }

        public override void _Process(double delta)
        {
            if (!IsActive || _panel == null || !_panel.Visible || Engine.IsEditorHint()) return;

            // Typewriter reveal.
            if (!_typewriterDone && _panel != null)
            {
                _charTimer += delta;
                if (_charTimer >= 1.0 / TypewriterSpeed)
                {
                    _charTimer = 0;
                    int total = CurrentTextLength();
                    int shown = _panel.VisibleCharacters;
                    if (shown < total)
                    {
                        _panel.VisibleCharacters = shown + 1;
                    }
                    else
                    {
                        _typewriterDone = true;
                        OnTypewriterComplete();
                    }
                }
            }

            _pulseTime += delta * 3.0;
        }

        private int CurrentTextLength()
            => _lineIndex >= 0 && _lineIndex < _lines.Length ? _lines[_lineIndex].Text.Length : 0;

        private void OnTypewriterComplete()
        {
            var line = _lines[_lineIndex];
            if (line.Choices != null && line.Choices.Length > 0)
            {
                ShowChoices(line.Choices);
            }
            // Otherwise: wait for advance input. Continue indicator is already visible.
        }

        // ════════════════════════════════════════════════════════════════
        // Input + advance
        // ════════════════════════════════════════════════════════════════

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!IsActive || _panel == null || !_panel.Visible || Engine.IsEditorHint()) return;
            if (@event.IsActionPressed(AdvanceAction))
            {
                if (!_typewriterDone)
                {
                    // Fast-complete the typewriter.
                    if (_panel != null) _panel.VisibleCharacters = -1;
                    _typewriterDone = true;
                    OnTypewriterComplete();
                }
                else if (!_showingChoices)
                {
                    Advance();
                }
                GetViewport().SetInputAsHandled();
            }
        }

        private void Advance()
        {
            _lineIndex++;
            ShowLine();
        }

        // ════════════════════════════════════════════════════════════════
        // Choices
        // ════════════════════════════════════════════════════════════════

        private void ShowChoices(string[] choices)
        {
            _showingChoices = true;
            if (_panel == null) return;

            foreach (var t in _choiceTweens)
                t?.Kill();
            _choiceTweens.Clear();

            _panel.ContinueVisible = false;
            _panel.SetChoices(choices);
        }

        private void OnChoiceSelected(int index)
        {
            _showingChoices = false;
            if (_panel != null) _panel.ChoicesVisible = false;
            EmitSignal(SignalName.ChoiceSelected, index);
            Advance();
        }

        // ════════════════════════════════════════════════════════════════
        // Entry / exit animation
        // ════════════════════════════════════════════════════════════════

        private void AnimateIn()
        {
            if (_panel == null) return;
            _animationTween?.Kill();

            _panel.Visible = true;
            _panel.Modulate = new Color(1, 1, 1, 0);

            // Slide from below.
            Vector2 targetPos = _panel.Position;
            _panel.Position = targetPos + new Vector2(0, 40);

            _animationTween = _panel.CreateTween().SetParallel(true);
            _animationTween.TweenProperty(_panel, "modulate:a", 1f, EntryDuration).SetEase(Tween.EaseType.Out);
            _animationTween.TweenProperty(_panel, "position", targetPos, EntryDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        }

        private void AnimateOut()
        {
            if (_panel == null) return;
            _animationTween?.Kill();

            _animationTween = _panel.CreateTween().SetParallel(true);
            _animationTween.TweenProperty(_panel, "modulate:a", 0f, EntryDuration * 0.7f);
            _animationTween.TweenProperty(_panel, "position:y", _panel.Position.Y + 30, EntryDuration * 0.7f)
                .SetEase(Tween.EaseType.In);
            _animationTween.Finished += OnAnimateOutFinished;
        }

        private void OnAnimateOutFinished()
        {
            if (_panel != null) _panel.Visible = false;
            EmitSignal(SignalName.DialogFinished);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _animationTween?.Kill();
            foreach (var t in _choiceTweens)
                t?.Kill();
            _choiceTweens.Clear();
            if (_engine != null && GodotObject.IsInstanceValid(_engine))
                _engine.DialogStarted -= StartFromDialogComponent;
            if (_panel != null && GodotObject.IsInstanceValid(_panel))
                _panel.ChoiceSelected -= OnChoiceSelected;
            if (_createdPanel && _panel != null && GodotObject.IsInstanceValid(_panel)) _panel.QueueFree();
            _panel = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}
