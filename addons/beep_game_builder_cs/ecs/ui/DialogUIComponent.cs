using Godot;

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

        [ExportGroup("Layout")]
        [Export] public DialogPosition Position { get; set; } = DialogPosition.Bottom;
        [Export] public Vector2 DialogSize { get; set; } = new(600, 160);
        [Export] public int ContentPadding { get; set; } = 16;

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
        private PanelContainer? _panel;
        private Label? _nameLabel;
        private RichTextLabel? _textLabel;
        private VBoxContainer? _choicesBox;
        private Label? _continueIndicator;
        private DialogLine[] _lines = System.Array.Empty<DialogLine>();
        private int _lineIndex;
        private double _charTimer;
        private bool _typewriterDone;
        private bool _showingChoices;
        private double _pulseTime;
        private Color _cachedAccent;
        private Color _cachedTextPrimary;
        private bool _themeFound;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            CallDeferred(nameof(BuildLayout));
        }

        // ════════════════════════════════════════════════════════════════
        // Layout construction
        // ════════════════════════════════════════════════════════════════

        private void BuildLayout()
        {
            if (GetParent() is not Godot.Control parent) return;

            // PanelContainer — the themed frame.
            _panel = new PanelContainer { Name = "DialogPanel" };
            parent.AddChild(_panel);
            _panel.Owner = parent;

            // MarginContainer — inner padding.
            var margin = new MarginContainer { Name = "DialogMargin" };
            margin.AddThemeConstantOverride("margin_left", ContentPadding);
            margin.AddThemeConstantOverride("margin_right", ContentPadding);
            margin.AddThemeConstantOverride("margin_top", ContentPadding / 2);
            margin.AddThemeConstantOverride("margin_bottom", ContentPadding / 2);
            _panel.AddChild(margin);

            // VBox — speaker + text + choices + indicator.
            var vbox = new VBoxContainer { Name = "DialogVBox" };
            vbox.AddThemeConstantOverride("separation", 6);
            margin.AddChild(vbox);

            _nameLabel = new Label { Name = "SpeakerLabel", Text = "" };
            vbox.AddChild(_nameLabel);

            _textLabel = new RichTextLabel
            {
                Name = "TextLabel",
                BbcodeEnabled = true,
                FitContent = true,
                ScrollFollowing = true,
                SizeFlagsVertical = Godot.Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 40)
            };
            vbox.AddChild(_textLabel);

            _choicesBox = new VBoxContainer { Name = "ChoicesBox", Visible = false };
            _choicesBox.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(_choicesBox);

            _continueIndicator = new Label
            {
                Name = "ContinueIndicator",
                Text = "▼",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _continueIndicator.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(_continueIndicator);

            ApplyAnchors();
            DiscoverTheme();
            _panel.Visible = false;
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
            // No theme found — use Godot defaults.
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

            // Speaker name with accent color.
            if (_nameLabel != null)
            {
                _nameLabel.Text = line.Speaker;
                Color sc = SpeakerColorOverride.A > 0 ? SpeakerColorOverride : _cachedAccent;
                _nameLabel.AddThemeColorOverride("font_color", sc);
            }

            // Body text.
            if (_textLabel != null)
            {
                _textLabel.Text = line.Text;
                _textLabel.VisibleCharacters = 0;
            }

            _charTimer = 0;
            _typewriterDone = false;

            // Continue indicator.
            if (_continueIndicator != null)
                _continueIndicator.Visible = ShowContinueIndicator;

            // Hide choices until this line's typewriter completes.
            if (_choicesBox != null) _choicesBox.Visible = false;
            _showingChoices = false;
        }

        public override void _Process(double delta)
        {
            if (!IsActive || _panel == null || !_panel.Visible || Engine.IsEditorHint()) return;

            // Typewriter reveal.
            if (!_typewriterDone && _textLabel != null)
            {
                _charTimer += delta;
                if (_charTimer >= 1.0 / TypewriterSpeed)
                {
                    _charTimer = 0;
                    int total = _textLabel.GetTotalCharacterCount();
                    int shown = _textLabel.VisibleCharacters;
                    if (shown < total)
                    {
                        _textLabel.VisibleCharacters = shown + 1;
                    }
                    else
                    {
                        _typewriterDone = true;
                        OnTypewriterComplete();
                    }
                }
            }

            // Pulsing continue indicator.
            if (_continueIndicator != null && _continueIndicator.Visible && !_showingChoices)
            {
                _pulseTime += delta * 3.0;
                float a = (float)((Mathf.Sin(_pulseTime) + 1f) * 0.5 * 0.7 + 0.3);
                _continueIndicator.Modulate = new Color(1, 1, 1, a);
            }
        }

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
                    if (_textLabel != null) _textLabel.VisibleCharacters = -1;
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
            if (_choicesBox == null) return;
            _choicesBox.Visible = true;

            // Hide continue indicator while choices are shown.
            if (_continueIndicator != null) _continueIndicator.Visible = false;

            // Clear old buttons.
            foreach (var c in _choicesBox.GetChildren()) c.QueueFree();

            // Spawn + stagger-animate choice buttons.
            for (int i = 0; i < choices.Length; i++)
            {
                var btn = new Button
                {
                    Text = choices[i],
                    SizeFlagsHorizontal = Godot.Control.SizeFlags.Fill,
                    Modulate = new Color(1, 1, 1, 0) // start invisible for stagger
                };
                _choicesBox.AddChild(btn);

                int idx = i;
                btn.Pressed += () => OnChoiceSelected(idx);

                // Stagger fade-in.
                var tw = btn.CreateTween();
                tw.TweenInterval(i * ChoiceStaggerDelay);
                tw.TweenProperty(btn, "modulate:a", 1f, 0.15f).SetEase(Tween.EaseType.Out);
            }
        }

        private void OnChoiceSelected(int index)
        {
            _showingChoices = false;
            if (_choicesBox != null) _choicesBox.Visible = false;
            EmitSignal(SignalName.ChoiceSelected, index);
            Advance();
        }

        // ════════════════════════════════════════════════════════════════
        // Entry / exit animation
        // ════════════════════════════════════════════════════════════════

        private void AnimateIn()
        {
            if (_panel == null) return;
            _panel.Visible = true;
            _panel.Modulate = new Color(1, 1, 1, 0);

            // Slide from below.
            Vector2 targetPos = _panel.Position;
            _panel.Position = targetPos + new Vector2(0, 40);

            var tw = _panel.CreateTween().SetParallel(true);
            tw.TweenProperty(_panel, "modulate:a", 1f, EntryDuration).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(_panel, "position", targetPos, EntryDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        }

        private void AnimateOut()
        {
            if (_panel == null) return;
            var tw = _panel.CreateTween().SetParallel(true);
            tw.TweenProperty(_panel, "modulate:a", 0f, EntryDuration * 0.7f);
            tw.TweenProperty(_panel, "position:y", _panel.Position.Y + 30, EntryDuration * 0.7f)
                .SetEase(Tween.EaseType.In);
            tw.Finished += () =>
            {
                if (_panel != null) _panel.Visible = false;
                EmitSignal(SignalName.DialogFinished);
            };
        }
    }
}
