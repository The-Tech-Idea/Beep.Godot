using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// RPG/platform life display using drawn hearts instead of a text meter.
    /// Seen across Example_Art/gameui8.png, ui9.png, and several RPG/mobile HUD sheets.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitHeartRow : KitControl
    {
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Chip;

        [Export(PropertyHint.Range, "1,20,1")] public int MaxHearts { get => _max; set { _max = Mathf.Max(1, value); QueueRedraw(); } }
        private int _max = 5;

        [Export(PropertyHint.Range, "0,20,0.5")] public float Value { get => _value; set { _value = Mathf.Clamp(value, 0, _max); QueueRedraw(); } }
        private float _value = 5f;

        [Export(PropertyHint.Range, "10,80,1")] public float HeartSize { get => _heartSize; set { _heartSize = Mathf.Max(8f, value); QueueRedraw(); } }
        private float _heartSize = 26f;

        [Export(PropertyHint.Range, "0,24,1")] public float Spacing { get => _spacing; set { _spacing = Mathf.Max(0f, value); QueueRedraw(); } }
        private float _spacing = 5f;

        [Export] public UiSurface.Role FillRole { get => _fillRole; set { _fillRole = value; QueueRedraw(); } }
        private UiSurface.Role _fillRole = UiSurface.Role.Danger;

        [Export] public bool DrawBackplate { get => _drawBackplate; set { _drawBackplate = value; QueueRedraw(); } }
        private bool _drawBackplate;

        private void UpdateMinimumSize()
        {
            if (CustomMinimumSize != Vector2.Zero) return;
            CustomMinimumSize = new Vector2(_max * _heartSize + (_max - 1) * _spacing, _heartSize);
        }

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Ignore;
            UpdateMinimumSize();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            if (Size.X <= 4 || Size.Y <= 4) return;

            float heart = Mathf.Min(_heartSize, Size.Y);
            float total = _max * heart + (_max - 1) * _spacing;
            float x = Mathf.Max(0f, (Size.X - total) * 0.5f);
            float y = (Size.Y - heart) * 0.5f;

            Color face = FaceColor();
            Color ink = InkColor();
            Color fill = UiSurface.Semantic(this, _fillRole);
            Color empty = new Color(face.R * 0.45f, face.G * 0.45f, face.B * 0.45f, 0.78f);
            float rim = Mathf.Max(1.5f, Geo.Rim * (UiSurface.FontSize(this) / 14f));

            if (_drawBackplate)
                DrawShape(new Rect2(0, 0, Size.X, Size.Y), KitShape.Pill, new Color(face.R, face.G, face.B, 0.78f), RimColor(), rim);

            for (int i = 0; i < _max; i++)
            {
                float amount = Mathf.Clamp(_value - i, 0f, 1f);
                var r = new Rect2(x + i * (heart + _spacing), y, heart, heart);
                DrawHeart(r, empty, ink, rim);
                if (amount > 0.02f)
                {
                    Color c = amount >= 0.98f ? fill : new Color(fill.R, fill.G, fill.B, 0.45f + amount * 0.45f);
                    DrawHeart(r.Grow(-heart * 0.08f), c, new Color(0, 0, 0, 0), 0f);
                }
            }
        }

        private void DrawHeart(Rect2 r, Color fill, Color rim, float rimWidth)
        {
            Vector2 c1 = r.Position + new Vector2(r.Size.X * 0.32f, r.Size.Y * 0.34f);
            Vector2 c2 = r.Position + new Vector2(r.Size.X * 0.68f, r.Size.Y * 0.34f);
            Vector2 top = r.Position + new Vector2(r.Size.X * 0.50f, r.Size.Y * 0.28f);
            Vector2 left = r.Position + new Vector2(r.Size.X * 0.12f, r.Size.Y * 0.42f);
            Vector2 right = r.Position + new Vector2(r.Size.X * 0.88f, r.Size.Y * 0.42f);
            Vector2 bottom = r.Position + new Vector2(r.Size.X * 0.50f, r.Size.Y * 0.92f);

            DrawCircle(c1, r.Size.X * 0.24f, fill);
            DrawCircle(c2, r.Size.X * 0.24f, fill);
            DrawColoredPolygon(new[] { left, top, right, bottom }, fill);

            if (rimWidth <= 0f) return;
            DrawArc(c1, r.Size.X * 0.24f, Mathf.Pi * 0.72f, Mathf.Pi * 1.96f, 18, rim, rimWidth);
            DrawArc(c2, r.Size.X * 0.24f, Mathf.Pi * 1.04f, Mathf.Pi * 2.28f, 18, rim, rimWidth);
            DrawPolyline(new[] { left, bottom, right }, rim, rimWidth);
        }
    }
}
