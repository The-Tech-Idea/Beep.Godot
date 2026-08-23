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

        [Export(PropertyHint.MultilineText)] public string Text { get => _text; set { _text = value ?? ""; QueueRedraw(); } }
        private string _text = "Wow! Look over there!";

        [Export] public KitBubbleTail Tail { get => _tail; set { _tail = value; QueueRedraw(); } }
        private KitBubbleTail _tail = KitBubbleTail.Bottom;

        [Export(PropertyHint.Range, "0,1,0.01")] public float TailOffset { get => _tailOffset; set { _tailOffset = Mathf.Clamp(value, 0.05f, 0.95f); QueueRedraw(); } }
        private float _tailOffset = 0.72f;

        [Export(PropertyHint.Range, "4,32,1")] public float Padding { get => _padding; set { _padding = Mathf.Max(2f, value); QueueRedraw(); } }
        private float _padding = 12f;

        [Export] public UiSurface.Role Accent { get => _accent; set { _accent = value; QueueRedraw(); } }
        private UiSurface.Role _accent = UiSurface.Role.Neutral;

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Ignore;
            if (CustomMinimumSize == Vector2.Zero)
                CustomMinimumSize = new Vector2(UiSurface.FontSize(this) * 14f, UiSurface.FontSize(this) * 5f);
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 14f, fs * 5f);
        }

        public override void _Draw()
        {
            if (Size.X <= 12 || Size.Y <= 12) return;

            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Body);
            float tail = Tail == KitBubbleTail.None ? 0f : Mathf.Clamp(fs * 0.9f, 10f, 20f);
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
