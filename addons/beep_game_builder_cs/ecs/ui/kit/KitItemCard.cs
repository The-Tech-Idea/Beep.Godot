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
            if (CustomMinimumSize != Vector2.Zero) return;
            int fs = UiSurface.FontSize(this);
            CustomMinimumSize = _layout == KitItemCardLayout.Tile
                ? new Vector2(Mathf.Clamp(fs * 5.2f, 64f, 96f), Mathf.Clamp(fs * 6.4f, 78f, 120f))
                : new Vector2(Mathf.Clamp(fs * 18f, 220f, 360f), Mathf.Clamp(fs * 5.2f, 66f, 94f));
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

            float pad = Mathf.Clamp(Size.Y * 0.12f, 7f, 14f);
            Rect2 icon = new(pad, pad, Size.Y - pad * 2f, Size.Y - pad * 2f);
            DrawIconWell(icon, ink);

            float priceW = string.IsNullOrEmpty(_price) ? 0f : Mathf.Clamp(Size.X * 0.22f, 54f, 92f);
            Rect2 textBox = new(icon.End.X + pad, pad, Size.X - icon.Size.X - pad * 3f - priceW, Size.Y - pad * 2f);
            DrawTitleAndDescription(textBox);

            if (priceW > 0f)
            {
                Rect2 price = new(Size.X - pad - priceW, Size.Y * 0.26f, priceW, Size.Y * 0.48f);
                DrawBadge(price, _price, _accent);
            }
            if (!string.IsNullOrEmpty(_badge))
                DrawBadge(new Rect2(Size.X - pad - Size.Y * 0.50f, -Size.Y * 0.05f, Size.Y * 0.55f, Size.Y * 0.34f), _badge, UiSurface.Role.Info);
        }

        private void DrawTile()
        {
            int fs = UiSurface.FontSize(this);
            float rim = Mathf.Max(1.5f, Geo.Rim * (fs / 14f));
            Rect2 r = new(0, 0, Size.X, Size.Y);
            Color face = _locked ? Desaturate(FaceColor(), 0.90f) : FaceColor();
            Color ink = InkColor();

            DrawShape(r, ActiveShape, face, _selected ? UiSurface.Semantic(this, UiSurface.Role.Info) : RimColor(), _selected ? rim * 1.5f : rim);

            float pad = Mathf.Clamp(Mathf.Min(Size.X, Size.Y) * 0.12f, 6f, 12f);
            Rect2 icon = new(pad, pad, Size.X - pad * 2f, Size.Y * 0.56f);
            DrawIconWell(icon, ink);

            if (!string.IsNullOrEmpty(_title))
            {
                Font? font = KitFont();
                if (font != null)
                {
                    Rect2 tb = new(pad, icon.End.Y + pad * 0.4f, Size.X - pad * 2f, Size.Y * 0.18f);
                    int tf = UiSurface.FitRole(this, UiSurface.TextRole.Caption, tb.Size, _title, font, min: 8);
                    Vector2 m = font.GetStringSize(_title, HorizontalAlignment.Left, -1, tf);
                    DrawText(font, tb.Position + new Vector2((tb.Size.X - m.X) * 0.5f, m.Y * 0.80f), _title, tf, UiSurface.Text(this));
                }
            }

            if (!string.IsNullOrEmpty(_price))
                DrawBadge(new Rect2(pad, Size.Y - pad - Size.Y * 0.20f, Size.X - pad * 2f, Size.Y * 0.20f), _price, _accent);
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
            if (!string.IsNullOrEmpty(_title))
            {
                int tf = UiSurface.FitRole(this, UiSurface.TextRole.Body, new Vector2(box.Size.X, box.Size.Y * 0.42f), _title, font, min: 8);
                DrawText(font, box.Position + new Vector2(0, font.GetHeight(tf) * 0.80f), _title, tf, ink);
            }
            if (!string.IsNullOrEmpty(_description))
            {
                int df = UiSurface.FitRole(this, UiSurface.TextRole.Caption, new Vector2(box.Size.X, box.Size.Y * 0.32f), _description, font, min: 7);
                DrawText(font, box.Position + new Vector2(0, box.Size.Y * 0.72f), _description, df, ink with { A = 0.78f });
            }
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
            DrawText(font, r.Position + new Vector2((r.Size.X - m.X) * 0.5f, (r.Size.Y + m.Y * 0.62f) * 0.5f), text, fs, ink);
        }

        private static Color Desaturate(Color c, float amount)
        {
            float l = UiSurface.Luminance(c);
            return new Color(Mathf.Lerp(c.R, l, amount), Mathf.Lerp(c.G, l, amount), Mathf.Lerp(c.B, l, amount), c.A);
        }
    }
}
