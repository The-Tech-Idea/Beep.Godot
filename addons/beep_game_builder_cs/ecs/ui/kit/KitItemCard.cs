using Godot;

namespace Beep.ECS.UI.Kit
{
    public enum KitItemCardLayout
    {
        Row,
        Tile,
    }

    /// <summary>
    /// Reusable shop, quest, inventory, and equipment card.
    /// The reference art repeats this as horizontal shop rows, mission rows, compact item tiles,
    /// and equipment cells with badges.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitItemCard : KitControl
    {
        protected override KitWidgetClass WidgetClass => Layout == KitItemCardLayout.Tile ? KitWidgetClass.Slot : KitWidgetClass.Panel;

        [Export] public KitItemCardLayout Layout { get => _layout; set { _layout = value; ApplyMinimumSize(); QueueRedraw(); } }
        private KitItemCardLayout _layout = KitItemCardLayout.Row;

        [Export] public string Title { get => _title; set { _title = value ?? ""; QueueRedraw(); } }
        private string _title = "Iron Sword";

        [Export(PropertyHint.MultilineText)] public string Description { get => _description; set { _description = value ?? ""; QueueRedraw(); } }
        private string _description = "A sturdy weapon.";

        [Export] public string PriceText { get => _price; set { _price = value ?? ""; QueueRedraw(); } }
        private string _price = "100";

        [Export] public string CountText { get => _count; set { _count = value ?? ""; QueueRedraw(); } }
        private string _count = "";

        [Export] public string BadgeText { get => _badge; set { _badge = value ?? ""; QueueRedraw(); } }
        private string _badge = "";

        [Export] public Texture2D? Icon { get => _icon; set { _icon = value; QueueRedraw(); } }
        private Texture2D? _icon;

        [Export] public UiSurface.Role Accent { get => _accent; set { _accent = value; QueueRedraw(); } }
        private UiSurface.Role _accent = UiSurface.Role.Warning;

        [Export] public bool Selected { get => _selected; set { _selected = value; QueueRedraw(); } }
        private bool _selected;

        [Export] public bool Locked { get => _locked; set { _locked = value; SetState(value ? KitState.Locked : KitState.Normal); QueueRedraw(); } }
        private bool _locked;

        [Signal] public delegate void PressedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
            MouseEntered += () => { if (!_locked) { SetState(KitState.Hover); } };
            MouseExited += () => { if (!_locked) { SetState(KitState.Normal); } };
            ApplyMinimumSize();
        }

        private void ApplyMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            Vector2 wanted = _layout == KitItemCardLayout.Tile
                ? new Vector2(Mathf.Clamp(fs * 5.4f, 76f, 108f), Mathf.Clamp(fs * 6.8f, 92f, 132f))
                : new Vector2(Mathf.Clamp(fs * 17.0f, 224f, 340f), Mathf.Clamp(fs * 4.25f, 58f, 76f));
            if (CustomMinimumSize != Vector2.Zero && CustomMinimumSize.X >= wanted.X && CustomMinimumSize.Y >= wanted.Y) return;
            CustomMinimumSize = wanted;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (_locked) return;
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                EmitSignal(SignalName.Pressed);
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8) return;
            if (_layout == KitItemCardLayout.Tile) DrawTile();
            else DrawRow();
        }

        private void DrawRow()
        {
            int fs = UiSurface.FontSize(this);
            float rim = Mathf.Max(1.5f, Geo.Rim * (fs / 14f));
            Rect2 r = new(0, 0, Size.X, Size.Y);
            Color face = _locked ? Desaturate(FaceColor(), 0.90f) : FaceColor();
            Color ink = InkColor();

            DrawShape(r, KitShape.Round, face, _selected ? UiSurface.Semantic(this, UiSurface.Role.Info) : RimColor(), _selected ? rim * 1.6f : rim);

            float pad = Mathf.Clamp(Size.Y * 0.11f, 6f, 10f);
            float iconSide = Mathf.Clamp(Size.Y - pad * 2f, 38f, 54f);
            Rect2 icon = new(pad, (Size.Y - iconSide) * 0.5f, iconSide, iconSide);
            DrawIconWell(icon, ink);

            float priceW = string.IsNullOrEmpty(_price) ? 0f : Mathf.Clamp(Size.X * 0.16f, 48f, 74f);
            float gap = Mathf.Clamp(Size.Y * 0.10f, 6f, 10f);
            Rect2 textBox = new(icon.End.X + gap, pad,
                                Mathf.Max(12f, Size.X - icon.End.X - gap - pad - priceW - (priceW > 0f ? gap : 0f)),
                                Size.Y - pad * 2f);
            DrawTitleAndDescription(textBox);

            if (priceW > 0f)
            {
                float priceH = Mathf.Clamp(Size.Y * 0.42f, 24f, 32f);
                Rect2 price = new(Size.X - pad - priceW, (Size.Y - priceH) * 0.5f, priceW, priceH);
                DrawBadge(price, _price, _accent);
            }
            if (!string.IsNullOrEmpty(_badge))
                DrawBadge(new Rect2(Size.X - pad - Size.Y * 0.44f, pad * 0.35f, Size.Y * 0.44f, Size.Y * 0.28f), _badge, UiSurface.Role.Info);
        }

        private void DrawTile()
        {
            int fs = UiSurface.FontSize(this);
            float rim = Mathf.Max(1.5f, Geo.Rim * (fs / 14f));
            Rect2 r = new(0, 0, Size.X, Size.Y);
            Color face = _locked ? Desaturate(FaceColor(), 0.90f) : FaceColor();
            Color ink = InkColor();

            DrawShape(r, ActiveShape, face, _selected ? UiSurface.Semantic(this, UiSurface.Role.Info) : RimColor(), _selected ? rim * 1.5f : rim);

            float pad = Mathf.Clamp(Mathf.Min(Size.X, Size.Y) * 0.11f, 7f, 12f);

            // Reserve the price band BEFORE the icon takes its share. The icon used a flat 0.56 of
            // the height and the title band started right under it, while the price badge was
            // pinned to the bottom — at the tile's own minimum (96x120) the title occupied
            // 83.3-104.9 and the price 84.5-108.5, i.e. the badge sat on top of the title. Three
            // stacked bands that each know what the others took cannot overlap.
            float priceH = string.IsNullOrEmpty(_price) ? 0f : Mathf.Clamp(Size.Y * 0.18f, 20f, 28f);
            float titleH = Mathf.Clamp(Size.Y * 0.22f, 20f, 34f);
            float iconH = Mathf.Max(24f, Size.Y - pad * 2f - priceH - titleH - pad * 0.8f);

            Rect2 icon = new(pad, pad, Size.X - pad * 2f, iconH);
            DrawIconWell(icon, ink);

            float titleTop = icon.End.Y + pad * 0.45f;
            float titleBottom = Mathf.Min(titleTop + titleH, priceH > 0f ? Size.Y - pad - priceH - pad * 0.25f : Size.Y - pad);

            if (!string.IsNullOrEmpty(_title) && titleBottom - titleTop > 4f)
            {
                Font? font = KitFont();
                if (font != null)
                {
                    Rect2 tb = new(pad, titleTop, Size.X - pad * 2f, titleBottom - titleTop);
                    DrawFittedText(font, tb, _title, UiSurface.TextRole.Caption, UiSurface.Text(this), HorizontalAlignment.Center, 8);
                }
            }

            if (priceH > 0f)
                DrawBadge(new Rect2(pad, Size.Y - pad - priceH, Size.X - pad * 2f, priceH), _price, _accent);
            if (!string.IsNullOrEmpty(_count))
                DrawBadge(new Rect2(Size.X - pad - Size.X * 0.30f, pad * 0.4f, Size.X * 0.30f, Size.Y * 0.18f), _count, UiSurface.Role.Info);
        }

        private void DrawIconWell(Rect2 r, Color ink)
        {
            Color well = UiSurface.Of(this);
            well = new Color(well.R * Geo.WellShade, well.G * Geo.WellShade, well.B * Geo.WellShade, 1f);
            DrawShape(r, KitShape.Round, well, ink, Mathf.Max(1f, Geo.Rim * 0.55f));
            if (_icon != null)
                DrawTextureRect(_icon, r.Grow(-Mathf.Min(r.Size.X, r.Size.Y) * 0.16f), false, _locked ? new Color(0.6f, 0.6f, 0.62f) : Colors.White);
            else
            {
                Color accent = _locked ? Desaturate(UiSurface.Semantic(this, _accent), 0.90f) : UiSurface.Semantic(this, _accent);
                Vector2 c = r.Position + r.Size * 0.5f;
                float rr = Mathf.Min(r.Size.X, r.Size.Y) * 0.23f;
                DrawCircle(c, rr, accent);
                DrawArc(c, rr, 0, Mathf.Tau, 24, ink, Mathf.Max(1.2f, rr * 0.10f));
            }
        }

        private void DrawTitleAndDescription(Rect2 box)
        {
            Font? font = KitFont();
            if (font == null) return;
            Color ink = UiSurface.Text(this);

            float gap = Mathf.Clamp(box.Size.Y * 0.06f, 2f, 4f);
            bool hasTitle = !string.IsNullOrEmpty(_title);
            bool hasDescription = !string.IsNullOrEmpty(_description);
            if (!hasTitle && !hasDescription) return;

            Rect2 titleBox = hasDescription
                ? new Rect2(box.Position, new Vector2(box.Size.X, (box.Size.Y - gap) * 0.52f))
                : box;
            Rect2 descBox = new(box.Position.X, titleBox.End.Y + gap, box.Size.X, Mathf.Max(1f, box.End.Y - titleBox.End.Y - gap));

            if (!string.IsNullOrEmpty(_title))
                DrawFittedText(font, titleBox, _title, UiSurface.TextRole.Small, ink, HorizontalAlignment.Left, 8);
            if (!string.IsNullOrEmpty(_description))
                DrawFittedText(font, descBox, _description, UiSurface.TextRole.Small, ink with { A = 0.78f }, HorizontalAlignment.Left, 7);
        }

        private void DrawBadge(Rect2 r, string text, UiSurface.Role role)
        {
            Font? font = KitFont();
            Color fill = _locked ? Desaturate(UiSurface.Semantic(this, role), 0.88f) : UiSurface.Semantic(this, role);
            Color ink = UiSurface.Luminance(fill) > 0.52f ? new Color(0.10f, 0.08f, 0.06f) : new Color(0.98f, 0.96f, 0.92f);
            DrawShape(r, KitShape.Pill, fill, InkColor(), Mathf.Max(1f, Geo.Rim * 0.55f));
            if (font == null || string.IsNullOrEmpty(text)) return;
            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Small, r.Size * 0.76f, text, font, min: 7);
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            float y = r.Position.Y + (r.Size.Y - font.GetHeight(fs)) * 0.5f + font.GetAscent(fs);
            DrawText(font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, y), text, fs, ink);
        }

        private void DrawFittedText(Font font, Rect2 r, string text, UiSurface.TextRole role, Color color,
                                    HorizontalAlignment align, int min)
        {
            if (string.IsNullOrEmpty(text) || r.Size.X <= 1f || r.Size.Y <= 1f) return;
            int fs = UiSurface.FitRole(this, role, r.Size, text, font, min: min);
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            float x = align == HorizontalAlignment.Center
                ? r.Position.X + (r.Size.X - m.X) * 0.5f
                : align == HorizontalAlignment.Right
                    ? r.End.X - m.X
                    : r.Position.X;
            float y = r.Position.Y + (r.Size.Y - font.GetHeight(fs)) * 0.5f + font.GetAscent(fs);
            DrawText(font, new Vector2(x, y), text, fs, color);
        }

        private static Color Desaturate(Color c, float amount)
        {
            float l = UiSurface.Luminance(c);
            return new Color(Mathf.Lerp(c.R, l, amount), Mathf.Lerp(c.G, l, amount), Mathf.Lerp(c.B, l, amount), c.A);
        }
    }
}
