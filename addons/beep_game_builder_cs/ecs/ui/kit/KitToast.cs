using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// One transient notification: a role glyph beside a wrapped message, on a plate coloured by
    /// what kind of news it is.
    ///
    /// It draws ONE toast and knows nothing about queues, stacking or expiry — that policy lives in
    /// <see cref="ToastNotificationComponent"/>, which caps how many are visible, shifts the
    /// existing ones down and tweens each away. Keeping the two apart is why a screen can show a
    /// single fixed banner using the same widget the notification stack uses.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitToast : KitControl
    {
        /// <summary>A message surface, not a pressable. It had been falling through to the base's
        /// Button default, which decided its corner radius and its selection cue as well as -- once
        /// the genre had artwork -- which sprite it was cut from, and it rendered as a glossy button
        /// with a raised lip.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Panel;

        [Export(PropertyHint.MultilineText)] public string Message { get => _message; set { string next = value ?? ""; if (_message == next) return; _message = next; RefreshMinimumAndRedraw(); } }
        [Export] public string IconGlyph { get => _icon; set { string next = value ?? ""; if (_icon == next) return; _icon = next; RefreshMinimumAndRedraw(); } }
        [Export] public UiSurface.Role Role { get => _role; set { if (_role == value) return; _role = value; RefreshVisualAndRedraw(); } }

        // Opinionated defaults, like KitRow's "Recover the Cargo" and KitInputHint's "[E] Gather
        // Wood". A toast with an empty message draws as a plain coloured rectangle — in the
        // inspector, in the browser and in a scene the moment it is dropped in — which reads as a
        // broken widget rather than as a notification. Every widget in this kit should look like a
        // game control before it is configured.
        private string _message = "Objective complete";
        private string _icon = "!";
        private UiSurface.Role _role = UiSurface.Role.Info;

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Ignore);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            int textFs = UiSurface.FontSize(this, UiSurface.TextRole.Caption);
            Font? font = KitFont();
            string text = KitCase(ToastText());
            float pad = Mathf.Max(6f, textFs * 0.65f);
            float textW = Mathf.Clamp(LongestLineWidth(font, text, textFs), fs * 10f, fs * 24f);
            int lines = EstimateWrappedLineCount(font, text, textFs, textW);
            float lineH = font?.GetHeight(textFs) * 1.08f ?? textFs * 1.25f;
            return new Vector2(Mathf.Max(fs * 18f, textW + pad * 2f),
                               Mathf.Max(fs * 3f, lineH * lines + pad * 2f));
        }

        private string ToastText()
            => string.IsNullOrEmpty(_icon) ? _message : $"{_icon}  {_message}";

        private static float LongestLineWidth(Font? font, string text, int fs)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fs * 8f;

            float width = 0f;
            foreach (string line in text.Replace("\r", "").Split('\n'))
                width = Mathf.Max(width, TextWidth(font, line, fs));
            return width;
        }

        private static int EstimateWrappedLineCount(Font? font, string text, int fs, float width)
        {
            if (string.IsNullOrWhiteSpace(text) || width <= 1f)
                return 1;

            int count = 0;
            foreach (string line in text.Replace("\r", "").Split('\n'))
                count += Mathf.Max(1, Mathf.CeilToInt(TextWidth(font, line, fs) / width));
            return Mathf.Clamp(count, 1, 2);
        }

        private static float TextWidth(Font? font, string text, int fs)
            => font?.GetStringSize(text ?? "", HorizontalAlignment.Left, -1, fs).X ?? (text ?? "").Length * fs * 0.55f;

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            Color fill = UiSurface.Semantic(this, Role);
            if (fill.A < 0.02f) fill = UiSurface.Of(this);
            var r = new Rect2(Vector2.Zero, Size);
            DrawPlate(r, ActiveShape, fill, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim));

            var font = KitFont();
            if (font == null) return;
            string text = KitCase(ToastText());

            // Fit to the box the text is actually drawn into, and use the same padding
            // _GetMinimumSize reserved. Fitting to 38% of the toast asked the message to survive in
            // a third of the room it gets, so it shrank to near-illegible on a toast that had
            // plenty of space — the widget looked like a coloured bar with something written on it.
            int baseFs = UiSurface.FontSize(this, UiSurface.TextRole.Caption);
            float pad = Mathf.Max(6f, baseFs * 0.65f);
            Rect2 textBox = r.Grow(-pad);
            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Caption, textBox.Size, text, font);

            Color ink = UiSurface.Luminance(fill) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f)
                : new Color(0.98f, 0.96f, 0.92f);
            KitChrome.DrawWrappedText(this, Genre, font, textBox, text, fs, ink,
                                      HorizontalAlignment.Center, maxLines: 2);
        }
    }
}
