using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A conversation panel: a speaker name, a wrapped body, and an optional list of replies.
    ///
    /// <c>VisibleCharacters</c> is what makes it a dialogue box rather than a panel with text in
    /// it — a negative value shows everything, and a caller walking it upward types the line out.
    /// The panel measures itself from the FULL body, so the box does not grow line by line while
    /// the text appears, which is the tell that ruins the effect.
    ///
    /// Choices and the continue marker are mutually exclusive in practice: a line either waits for
    /// acknowledgement or asks a question. Both are exposed rather than inferred, because a screen
    /// that wants a prompt beneath its choices is a legitimate design.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitDialogBox : KitControl
    {
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Panel;

        [Export] public string Speaker { get => _speaker; set { string next = value ?? ""; if (_speaker == next) return; _speaker = next; RefreshMinimumAndRedraw(); } }
        // Opinionated defaults for the same reason KitRow and KitInputHint carry them: an empty
        // dialogue box draws as a bare dark rectangle, which looks like a widget that failed rather
        // than one waiting for its line.
        private string _speaker = "Quartermaster";

        [Export(PropertyHint.MultilineText)] public string Body { get => _body; set { string next = value ?? ""; if (_body == next) return; _body = next; RefreshMinimumAndRedraw(); } }
        // Short enough that the box's natural width still fits a phone column — the default text
        // feeds _GetMinimumSize, so a long one silently makes the widget too wide to place.
        private string _body = "Take the west road.";

        [Export] public int VisibleCharacters { get => _visibleCharacters; set { if (_visibleCharacters == value) return; _visibleCharacters = value; RefreshVisualAndRedraw(); } }
        private int _visibleCharacters = -1;

        [Export] public bool ContinueVisible { get => _continueVisible; set { if (_continueVisible == value) return; _continueVisible = value; RefreshVisualAndRedraw(); } }
        private bool _continueVisible = true;

        [Export] public string[] Choices { get => _choices; set { if (SetStringArray(ref _choices, value)) RefreshChoiceLayout(); } }
        private string[] _choices = System.Array.Empty<string>();

        [Export] public bool ChoicesVisible { get => _choicesVisible; set { if (_choicesVisible == value) return; _choicesVisible = value; RefreshChoiceLayout(); } }
        private bool _choicesVisible;

        [Signal] public delegate void ChoiceSelectedEventHandler(int index);

        private int _hoverChoice = -1;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            if (!_eventsHooked)
            {
                MouseExited += ClearHover;
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

        public void SetChoices(string[]? choices)
        {
            string[] next = NormalizeStrings(choices);
            bool nextVisible = next.Length > 0;
            if (SameStrings(_choices, next) && _choicesVisible == nextVisible) return;
            _choices = next;
            _choicesVisible = nextVisible;
            RefreshChoiceLayout();
        }

        public void AddChoice(string choice)
        {
            string[] next = new string[_choices.Length + 1];
            _choices.CopyTo(next, 0);
            next[^1] = choice ?? "";
            SetChoices(next);
        }

        /// <summary>Drop one choice, reporting whether the index named one. The highlight follows
        /// the removal so it cannot point past the end of a shortened list.</summary>
        public bool RemoveChoice(int index)
        {
            if (index < 0 || index >= _choices.Length) return false;

            string[] next = new string[_choices.Length - 1];
            for (int i = 0, w = 0; i < _choices.Length; i++)
                if (i != index) next[w++] = _choices[i];

            if (index <= _hoverChoice) _hoverChoice = Mathf.Max(-1, _hoverChoice - 1);
            SetChoices(next);
            return true;
        }

        public void ClearChoices() => SetChoices(System.Array.Empty<string>());

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            int bodyFs = UiSurface.FontSize(this, UiSurface.TextRole.Body);
            float pad = DialogPad(fs);
            float rowH = ChoiceRowHeight(fs);
            int choiceCount = ChoicesVisible ? Choices.Length : 0;
            float choiceH = choiceCount > 0 ? choiceCount * rowH + pad : 0f;

            float w = Mathf.Clamp(fs * 26f, 280f, 340f);
            Font? font = KitFont();
            string speaker = KitCase(_speaker);
            string body = KitCase(_body);
            w = Mathf.Max(w, TextWidth(font, speaker, UiSurface.FontSize(this, UiSurface.TextRole.Caption)) + pad * 6f);
            foreach (string choice in Choices)
                w = Mathf.Max(w, TextWidth(font, KitCase(choice ?? ""), UiSurface.FontSize(this, UiSurface.TextRole.Caption)) + pad * 4.4f);

            float bodyW = Mathf.Clamp(LongestLineWidth(font, body, bodyFs), fs * 14f, fs * 23f);
            w = Mathf.Max(w, bodyW + pad * 2f);
            int bodyLines = EstimateWrappedLineCount(font, body, bodyFs, Mathf.Max(1f, w - pad * 2f));
            float lineH = font?.GetHeight(bodyFs) * 1.08f ?? bodyFs * 1.25f;
            float bodyH = Mathf.Max(fs * 5.2f, lineH * Mathf.Clamp(bodyLines, 1, 6));
            float top = string.IsNullOrEmpty(_speaker) ? pad : pad + fs * 0.8f;
            float h = Mathf.Max(fs * 11f, top + bodyH + pad + choiceH);
            return new Vector2(w, h);
        }

        private void RefreshChoiceLayout()
        {
            if (_hoverChoice >= Choices.Length)
                _hoverChoice = Choices.Length - 1;
            if (!ChoicesVisible || Choices.Length == 0)
                _hoverChoice = -1;
            RefreshMinimumAndRedraw();
        }

        private static bool SetStringArray(ref string[] target, string[]? value)
        {
            string[] next = NormalizeStrings(value);
            if (SameStrings(target, next)) return false;
            target = next;
            return true;
        }

        private static string[] NormalizeStrings(string[]? values)
        {
            if (values == null || values.Length == 0)
                return System.Array.Empty<string>();

            var next = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                next[i] = values[i] ?? "";
            return next;
        }

        private static bool SameStrings(string[] a, string[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if ((a[i] ?? "") != (b[i] ?? ""))
                    return false;
            return true;
        }

        private static float DialogPad(int fs) => Mathf.Max(12f, fs * 1.1f);

        private static float ChoiceRowHeight(int fs) => Mathf.Max(fs * 1.85f, 28f);

        private static float TextWidth(Font? font, string text, int fs)
            => string.IsNullOrEmpty(text)
                ? 0f
                : font?.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X ?? text.Length * fs * 0.56f;

        private static float LongestLineWidth(Font? font, string text, int fs)
        {
            if (string.IsNullOrWhiteSpace(text)) return fs * 18f;
            float width = 0f;
            foreach (string line in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
                width = Mathf.Max(width, TextWidth(font, line, fs));
            return width;
        }

        private static int EstimateWrappedLineCount(Font? font, string text, int fs, float width)
        {
            if (string.IsNullOrWhiteSpace(text) || width <= 1f) return 1;
            int count = 0;
            foreach (string line in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
                count += Mathf.Max(1, Mathf.CeilToInt(TextWidth(font, line, fs) / width));
            return Mathf.Max(1, count);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (!ChoicesVisible) return;
            if (KitChrome.NavigateOrRelease(this, @event, dir => dir.Y != 0 && MoveChoice(dir.Y)))
                return;

            if (KitChrome.IsConfirm(@event) && _hoverChoice >= 0)
            {
                EmitSignal(SignalName.ChoiceSelected, _hoverChoice);
                AcceptEvent();
                return;
            }

            if (@event is InputEventMouseMotion mm)
            {
                int hit = HitChoice(mm.Position);
                if (_hoverChoice != hit) { _hoverChoice = hit; QueueRedraw(); }
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            {
                int hit = HitChoice(mb.Position);
                if (hit >= 0)
                {
                    GrabFocus();
                    EmitSignal(SignalName.ChoiceSelected, hit);
                    AcceptEvent();
                }
            }
        }

        /// <summary>Move the highlighted choice, reporting whether it moved. Released at the first
        /// and last choice so focus can leave the dialog.</summary>
        private bool MoveChoice(int delta)
        {
            if (!ChoicesVisible || Choices.Length == 0) return false;
            int next = Mathf.Clamp(_hoverChoice < 0 ? 0 : _hoverChoice + delta, 0, Choices.Length - 1);
            if (next == _hoverChoice) return false;
            _hoverChoice = next;
            QueueRedraw();
            return true;
        }

        private void ClearHover()
        {
            if (_hoverChoice < 0) return;
            _hoverChoice = -1;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Size.X < 16f || Size.Y < 16f) return;

            var host = new Rect2(Vector2.Zero, Size);
            DrawMaterial(host, ActiveShape);
            if (!string.IsNullOrEmpty(Speaker)) DrawBanner(host, Speaker, KitShape.Ribbon, 0.18f, 0.42f, 0.72f);

            var font = KitFont();
            if (font == null) return;

            int fs = UiSurface.FontSize(this);
            float pad = DialogPad(fs);
            float top = string.IsNullOrEmpty(Speaker) ? pad : pad + fs * 0.8f;
            float choiceArea = ChoicesVisible ? Mathf.Min(Size.Y * 0.42f, Choices.Length * ChoiceRowHeight(fs) + pad) : 0f;
            var textBox = new Rect2(pad, top, Size.X - pad * 2f, Size.Y - top - pad - choiceArea);
            DrawBodyText(font, textBox);

            if (ChoicesVisible) DrawChoices(font, fs, pad);
            else if (ContinueVisible)
            {
                string mark = "v";
                int mfs = UiSurface.FitRole(this, UiSurface.TextRole.Caption, new Vector2(fs * 2f, fs * 1.5f), mark, font);
                Vector2 m = font.GetStringSize(mark, HorizontalAlignment.Left, -1, mfs);
                DrawText(font, new Vector2(Size.X - pad - m.X, Size.Y - pad * 0.55f), mark, mfs,
                         UiSurface.Semantic(this, UiSurface.Role.Accent));
            }

            KitChrome.DrawFocusRing(this, Genre, host, ActiveShape, 0.8f);
        }

        private void DrawBodyText(Font font, Rect2 box)
        {
            string text = _visibleCharacters >= 0 && _visibleCharacters < _body.Length
                ? _body[.._visibleCharacters]
                : _body;
            if (string.IsNullOrEmpty(text)) return;

            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Body);
            KitChrome.DrawWrappedText(this, Genre, font, box, text, fs,
                                      UiSurface.Text(this));
        }

        private void DrawChoices(Font font, int fs, float pad)
        {
            float rowH = ChoiceRowHeight(fs);
            float total = Choices.Length * rowH;
            float y = Size.Y - pad - total;
            for (int i = 0; i < Choices.Length; i++)
            {
                var r = new Rect2(pad, y + i * rowH, Size.X - pad * 2f, rowH - fs * 0.25f);
                Color fill = UiSurface.Semantic(this, i == _hoverChoice ? UiSurface.Role.Info : UiSurface.Role.Accent);
                DrawPlate(r, ActiveShape, fill, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim * 0.6f));
                string choice = KitCase(Choices[i]);
                int cfs = UiSurface.FitRole(this, UiSurface.TextRole.Caption, r.Size - new Vector2(pad, 0), choice, font, min: 8);
                choice = KitChrome.EllipsizeText(font, choice, cfs, r.Size.X - pad * 1.3f);
                Vector2 m = font.GetStringSize(choice, HorizontalAlignment.Left, -1, cfs);
                DrawText(font, new Vector2(r.Position.X + pad * 0.65f, r.Position.Y + (r.Size.Y + m.Y * 0.60f) * 0.5f),
                         choice, cfs, UiSurface.Text(this));
            }
        }

        private int HitChoice(Vector2 p)
        {
            if (!ChoicesVisible || Choices.Length == 0) return -1;
            int fs = UiSurface.FontSize(this);
            float pad = DialogPad(fs);
            float rowH = ChoiceRowHeight(fs);
            float y = Size.Y - pad - Choices.Length * rowH;
            int hit = Mathf.FloorToInt((p.Y - y) / rowH);
            return hit >= 0 && hit < Choices.Length && p.X >= pad && p.X <= Size.X - pad ? hit : -1;
        }
    }
}
