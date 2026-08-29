using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A list row — CATALOGUE-FROM-ART.md section B's `MissionRow` and `PlayerRow`, which are one
    /// widget with different payloads: a rank or index, a title with a subtitle, a value, and an
    /// optional state chip.
    ///
    /// Selection is a FILL, per the art pass's convention-by-widget-class finding: "card
    /// carousels use an outline, tab strips use fill/elevation, <b>list rows use a fill</b>"
    /// (racing1: "fill the row with the only saturated colour"). Using the card's outline here
    /// would be the wrong mechanism for the class.
    ///
    /// Rows alternate their plate very slightly so a long list stays readable without needing a
    /// separator per row — the "tile separator 0.50 x face" note in gameui2 is the alternative,
    /// and banding is cheaper and survives a restyle.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitRow : KitControl
    {
        /// <summary>A bar: takes the theme's bar corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Bar;

        [Export] public string Rank { get => _rank; set { SetText(ref _rank, value); } }
        private string _rank = "1";

        [Export] public string Title { get => _title; set { SetText(ref _title, value); } }
        private string _title = "Recover the Cargo";

        [Export] public string Subtitle { get => _sub; set { SetText(ref _sub, value); } }
        private string _sub = "";

        [Export] public string Value { get => _value; set { SetText(ref _value, value); } }
        private string _value = "1,240";

        /// <summary>Short state word — NEW, DONE, LOCKED. Empty hides the chip.</summary>
        [Export] public string State_ { get => _state; set { SetText(ref _state, value); } }
        private string _state = "";

        [Export]
        public UiSurface.Role StateRole
        {
            get => _stateRole;
            set { if (_stateRole == value) return; _stateRole = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _stateRole = UiSurface.Role.Success;

        [Export] public bool Selected { get => _sel; set { if (_sel == value) return; _sel = value; RefreshVisualAndRedraw(); } }
        private bool _sel;

        /// <summary>Odd rows take a slightly different plate. Set by the list, not the row.</summary>
        [Export]
        public bool Alternate
        {
            get => _alternate;
            set { if (_alternate == value) return; _alternate = value; RefreshVisualAndRedraw(); }
        }
        private bool _alternate;
        private bool _hover;
        private bool _eventsHooked;

        [Signal] public delegate void ActivatedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            if (!_eventsHooked)
            {
                MouseEntered += () => { _hover = true; QueueRedraw(); };
                MouseExited += () => { _hover = false; QueueRedraw(); };
                _eventsHooked = true;
            }
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearHover();
        }

        private void ClearHover()
        {
            if (!_hover) return;
            _hover = false;
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent @event)
        {
            KitChrome.ActivateOnClickOrConfirm(this, @event, () =>
            {
                Selected = true;
                EmitSignal(SignalName.Activated);
            });
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float pad = fs * 0.8f;
            float rankW = string.IsNullOrEmpty(_rank)
                ? 0f
                : Mathf.Max(fs * 1.4f, TextWidth(KitCase(_rank), UiSurface.TextRole.Body));
            float titleW = Mathf.Max(
                TextWidth(KitCase(_title), UiSurface.TextRole.Body),
                TextWidth(KitCase(_sub), UiSurface.TextRole.Caption));
            titleW = Mathf.Clamp(titleW, fs * 6f, fs * 14f);
            float valueW = string.IsNullOrEmpty(_value)
                ? 0f
                : Mathf.Max(fs * 2f, TextWidth(KitCase(_value), UiSurface.TextRole.Value));
            float stateW = string.IsNullOrEmpty(_state)
                ? 0f
                : TextWidth(KitCase(_state), UiSurface.TextRole.Small) + fs * 1.1f;
            float width = pad * 2f + titleW;
            if (rankW > 0f) width += rankW + pad;
            if (stateW > 0f) width += stateW + pad;
            if (valueW > 0f) width += valueW + pad;
            return new Vector2(Mathf.Max(fs * 18f, width), fs * 3f);
        }

        private void SetText(ref string target, string? value)
        {
            string next = value ?? "";
            if (target == next) return;
            target = next;
            RefreshMinimumAndRedraw();
        }

        private void RefreshMinimumAndRedraw()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private float TextWidth(string text, UiSurface.TextRole role)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            Font? font = KitFont();
            int fs = UiSurface.FontSize(this, role);
            return font?.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X ?? text.Length * fs * 0.56f;
        }

        public override void _Draw()
        {
            if (Size.X < 24f || Size.Y < 10f) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            var r = new Rect2(Vector2.Zero, Size);

            Color plate = _sel
                ? UiSurface.Semantic(this, UiSurface.Role.Accent)          // fill: the row class's cue
                : Alternate
                    ? new Color(face.R * 0.86f, face.G * 0.86f, face.B * 0.90f, 1f)
                    : new Color(face.R * 0.94f, face.G * 0.94f, face.B * 0.97f, 1f);
            if (_hover && !_sel)
                plate = new Color(Mathf.Lerp(plate.R, UiSurface.Semantic(this, UiSurface.Role.Info).R, 0.18f),
                                  Mathf.Lerp(plate.G, UiSurface.Semantic(this, UiSurface.Role.Info).G, 0.18f),
                                  Mathf.Lerp(plate.B, UiSurface.Semantic(this, UiSurface.Role.Info).B, 0.18f), 1f);

            DrawShape(r, ActiveShape, plate, ink, Mathf.Max(1f, g.Rim * 0.5f * (fs / 14f)));
            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), r, ActiveShape, 0.75f);
            if (font == null) return;

            Color txt = _sel && UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f)
                : UiSurface.Text(this);
            string rank = KitCase(_rank);
            string title = KitCase(_title);
            string subtitle = KitCase(_sub);
            string value = KitCase(_value);
            string stateText = KitCase(_state);

            float pad = fs * 0.8f;
            float x = pad;

            if (!string.IsNullOrEmpty(rank))
            {
                float rankW = Mathf.Max(fs * 1.4f, Size.X * 0.10f);
                rank = KitChrome.EllipsizeText(font, rank, fs, rankW);
                Vector2 m = font.GetStringSize(rank, HorizontalAlignment.Left, -1, fs);
                DrawText(font, new Vector2(x, (Size.Y + m.Y * 0.6f) * 0.5f),
                           rank, fs, txt with { A = 0.7f });
                x += Mathf.Max(m.X, fs * 1.4f) + pad;
            }

            // Value hugs the right edge; the state chip sits just inside it.
            float rx = Size.X - pad;
            if (!string.IsNullOrEmpty(value))
            {
                float valueBoxW = Mathf.Max(fs * 2f, Size.X * 0.24f);
                int vf = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                           new Vector2(valueBoxW, Size.Y * 0.62f),
                                           value, font, min: 8);
                value = KitChrome.EllipsizeText(font, value, vf, valueBoxW);
                Vector2 vm = font.GetStringSize(value, HorizontalAlignment.Left, -1, vf);
                DrawText(font, new Vector2(rx - vm.X, (Size.Y + vm.Y * 0.6f) * 0.5f),
                           value, vf, txt);
                rx -= vm.X + pad;
            }

            if (!string.IsNullOrEmpty(stateText))
            {
                float stateBoxW = Mathf.Max(fs * 2f, Size.X * 0.18f);
                int cs = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                           new Vector2(stateBoxW, Size.Y * 0.48f),
                                           stateText, font, min: 8);
                stateText = KitChrome.EllipsizeText(font, stateText, cs, stateBoxW);
                Vector2 cm = font.GetStringSize(stateText, HorizontalAlignment.Left, -1, cs);
                float cw = cm.X + cs * 1.1f, ch = cs * 1.5f;
                var chip = new Rect2(rx - cw, (Size.Y - ch) * 0.5f, cw, ch);
                Color cc = UiSurface.Semantic(this, StateRole);
                DrawShape(chip, KitShape.Pill, cc, ink, 1.5f);
                DrawText(font, new Vector2(chip.Position.X + (cw - cm.X) * 0.5f, chip.Position.Y + (ch + cm.Y * 0.6f) * 0.5f),
                           stateText, cs, UiSurface.Luminance(cc) > 0.5f
                               ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
                rx = chip.Position.X - pad;
            }

            // Title, with the subtitle beneath it when there is room for two lines.
            float titleW = Mathf.Max(fs * 2f, rx - x);
            bool twoLine = !string.IsNullOrEmpty(subtitle) && Size.Y > fs * 2.6f;
            int tf = UiSurface.FitRole(this, UiSurface.TextRole.Body,
                                       new Vector2(titleW, Size.Y * (twoLine ? 0.34f : 0.62f)),
                                       title, font, min: 8);
            title = KitChrome.EllipsizeText(font, title, tf, titleW);
            Vector2 tm = font.GetStringSize(title, HorizontalAlignment.Left, -1, tf);
            float ty = twoLine ? Size.Y * 0.44f : (Size.Y + tm.Y * 0.6f) * 0.5f;
            DrawText(font, new Vector2(x, ty), title, tf, txt);
            if (twoLine)
            {
                int ss = UiSurface.FitRole(this, UiSurface.TextRole.Caption,
                                           new Vector2(titleW, Size.Y * 0.28f),
                                           subtitle, font, min: 8);
                subtitle = KitChrome.EllipsizeText(font, subtitle, ss, titleW);
                DrawText(font, new Vector2(x, Size.Y * 0.78f), subtitle, ss, txt with { A = 0.65f });
            }
        }
    }
}
