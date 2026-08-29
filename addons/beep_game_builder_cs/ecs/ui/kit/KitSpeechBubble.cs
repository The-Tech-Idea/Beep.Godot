using Godot;
namespace Beep.ECS.UI.Kit
{
    public enum KitBubbleTail
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
    }

    /// <summary>
    /// Game-facing dialogue/callout bubble with a drawn tail.
    /// Covers RPG dialogue, city-builder world callouts, tutorials, and quest bubbles.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitSpeechBubble : KitControl
    {
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Panel;

        [Export(PropertyHint.MultilineText)] public string Text { get => _text; set { string next = value ?? ""; if (_text == next) return; _text = next; RefreshMinimumAndRedraw(); } }
        private string _text = "Wow! Look over there!";

        [Export] public KitBubbleTail Tail { get => _tail; set { if (_tail == value) return; _tail = value; RefreshMinimumAndRedraw(); } }
        private KitBubbleTail _tail = KitBubbleTail.Bottom;

        [Export(PropertyHint.Range, "0,1,0.01")] public float TailOffset { get => _tailOffset; set { float next = Mathf.Clamp(value, 0.05f, 0.95f); if (Mathf.IsEqualApprox(_tailOffset, next)) return; _tailOffset = next; RefreshVisualAndRedraw(); } }
        private float _tailOffset = 0.72f;

        [Export(PropertyHint.Range, "4,32,1")] public float Padding { get => _padding; set { float next = Mathf.Max(2f, value); if (Mathf.IsEqualApprox(_padding, next)) return; _padding = next; RefreshMinimumAndRedraw(); } }
        private float _padding = 12f;

        [Export] public UiSurface.Role Accent { get => _accent; set { if (_accent == value) return; _accent = value; RefreshVisualAndRedraw(); } }
        private UiSurface.Role _accent = UiSurface.Role.Neutral;

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Ignore);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Body);
            Font? font = KitFont();
            string text = KitCase(_text);
            float longest = LongestLineWidth(font, text, fs);
            float textW = Mathf.Clamp(longest, fs * 10f, fs * 24f);
            int wrappedLines = EstimateWrappedLineCount(font, text, fs, textW);
            float lineH = font?.GetHeight(fs) * 1.08f ?? fs * 1.25f;
            float tail = TailSizeFor(fs);

            float w = Mathf.Max(fs * 14f, textW + _padding * 2f);
            float h = Mathf.Max(fs * 5f, lineH * Mathf.Clamp(wrappedLines, 1, 4) + _padding * 2f);
            if (Tail is KitBubbleTail.Left or KitBubbleTail.Right)
                w += tail;
            else if (Tail is KitBubbleTail.Top or KitBubbleTail.Bottom)
                h += tail;
            return new Vector2(w, h);
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

        private static float TailSizeFor(int fs) => Mathf.Clamp(fs * 0.9f, 10f, 20f);

        private static float LongestLineWidth(Font? font, string text, int fs)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float width = 0f;
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
                width = Mathf.Max(width, TextWidth(font, line, fs));
            return width;
        }

        private static int EstimateWrappedLineCount(Font? font, string text, int fs, float width)
        {
            if (string.IsNullOrWhiteSpace(text) || width <= 1f) return 1;
            int count = 0;
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
                count += Mathf.Max(1, Mathf.CeilToInt(TextWidth(font, line, fs) / width));
            return Mathf.Max(1, count);
        }

        private static float TextWidth(Font? font, string text, int fs)
            => string.IsNullOrEmpty(text)
                ? 0f
                : font?.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X ?? text.Length * fs * 0.56f;

        public override void _Draw()
        {
            if (Size.X <= 12 || Size.Y <= 12) return;

            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Body);
            float tail = Tail == KitBubbleTail.None ? 0f : TailSizeFor(fs);
            Rect2 body = Tail switch
            {
                KitBubbleTail.Top => new Rect2(0, tail, Size.X, Size.Y - tail),
                KitBubbleTail.Bottom => new Rect2(0, 0, Size.X, Size.Y - tail),
                KitBubbleTail.Left => new Rect2(tail, 0, Size.X - tail, Size.Y),
                KitBubbleTail.Right => new Rect2(0, 0, Size.X - tail, Size.Y),
                _ => new Rect2(0, 0, Size.X, Size.Y),
            };

            Color face = _accent == UiSurface.Role.Neutral ? FaceColor() : UiSurface.Semantic(this, _accent);
            Color ink = InkColor();
            float rim = Mathf.Max(1.5f, Geo.Rim * (fs / 14f));

            DrawShape(body, KitShape.Round, face, RimColor(), rim);
            DrawTail(body, tail, face, RimColor(), rim);
            Font? font = KitFont();
            if (font != null)
                KitChrome.DrawWrappedText(this, KitChrome.GenreOf(this), font, body.Grow(-_padding), _text,
                                          fs,
                                          UiSurface.Luminance(face) > 0.55f
                                              ? new Color(0.10f, 0.08f, 0.06f) : UiSurface.Text(this),
                                          ellipsize: true);
        }

        private void DrawTail(Rect2 body, float tail, Color face, Color rim, float rimWidth)
        {
            if (Tail == KitBubbleTail.None || tail <= 0f) return;
            Vector2[] p = Tail switch
            {
                KitBubbleTail.Bottom => new[]
                {
                    body.Position + new Vector2(body.Size.X * TailOffset - tail * 0.65f, body.Size.Y - 1f),
                    body.Position + new Vector2(body.Size.X * TailOffset + tail * 0.65f, body.Size.Y - 1f),
                    body.Position + new Vector2(body.Size.X * TailOffset + tail * 0.25f, body.Size.Y + tail),
                },
                KitBubbleTail.Top => new[]
                {
                    body.Position + new Vector2(body.Size.X * TailOffset - tail * 0.65f, 1f),
                    body.Position + new Vector2(body.Size.X * TailOffset + tail * 0.65f, 1f),
                    body.Position + new Vector2(body.Size.X * TailOffset - tail * 0.25f, -tail),
                },
                KitBubbleTail.Left => new[]
                {
                    body.Position + new Vector2(1f, body.Size.Y * TailOffset - tail * 0.65f),
                    body.Position + new Vector2(1f, body.Size.Y * TailOffset + tail * 0.65f),
                    body.Position + new Vector2(-tail, body.Size.Y * TailOffset + tail * 0.25f),
                },
                _ => new[]
                {
                    body.Position + new Vector2(body.Size.X - 1f, body.Size.Y * TailOffset - tail * 0.65f),
                    body.Position + new Vector2(body.Size.X - 1f, body.Size.Y * TailOffset + tail * 0.65f),
                    body.Position + new Vector2(body.Size.X + tail, body.Size.Y * TailOffset + tail * 0.25f),
                },
            };
            DrawColoredPolygon(p, face);
            DrawPolyline(new[] { p[0], p[2], p[1] }, rim, rimWidth);
        }

    }
}
