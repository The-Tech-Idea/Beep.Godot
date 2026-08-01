using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Two plates of OPPOSITE POLARITY welded by a thin keyline — a label plate and a value
    /// plate, sharing one silhouette.
    ///
    /// Measured from Example_Art/rpgui3.png (plans/game-ui-kit/art/rpgui3.md, widget 1), the
    /// densest reference in the folder:
    ///
    ///     +----------------------------+--+--------------+
    ///     |  ATTACK                    |##|      7       |
    ///     +----------------------------+--+--------------+
    ///       dark plate  L=0.19          2px  PURE WHITE L=1.00
    ///       light text                keyline  dark text
    ///            92px                            46px
    ///
    /// "**Proportion to take: 2 : 1 label to value, 2px weld, value plate at maximum
    /// lightness.**" — the art document's own conclusion, and 92:46 is exactly 2:1.
    ///
    /// The art pass called this "the single most reusable widget in the folder for dense
    /// information, and the kit does not have it": ATTACK/DEFENSE/COMBO/TYPE all use it, and so
    /// do ATK/DEF/STR/INT/DEX/VIT in the inventory screen at half the size. Every stat row in
    /// every genre HUD is this widget.
    ///
    /// The polarity inversion is the whole point and is why this is not a Label pair in an
    /// HBoxContainer: the value is the only maximum-contrast element, so the eye lands on the
    /// number without needing size, colour or an alignment guide to lead it there.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitLabelValue : KitControl
    {
        /// <summary>A chip: takes the theme's chip corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Chip;

        [Export] public string Label { get => _label; set { _label = value ?? ""; QueueRedraw(); } }
        private string _label = "ATTACK";

        [Export] public string Value { get => _value; set { _value = value ?? ""; QueueRedraw(); } }
        private string _value = "7";

        /// <summary>Label : value width. 2.0 is the measured proportion; a longer value (a
        /// timestamp, a large number) is the only reason to lower it.</summary>
        [Export(PropertyHint.Range, "0.5,6.0,0.1")] public float LabelValueRatio { get; set; } = 2f;

        /// <summary>Which element carries the palette. The settled rule from five independent
        /// references is that the palette goes on ONE element and the other stays neutral, so
        /// this is a choice of which — not an invitation to colour both.</summary>
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Neutral;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 9.5f, fs * Mathf.Max(1.7f, Geo.HeightRatio * 0.68f));
            }
        }

        public override void _Draw()
        {
            if (Size.X <= 4 || Size.Y <= 4) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);

            // 2px weld at 14pt, scaled with the type so the joint stays a hairline rather than
            // becoming a gap on a large row.
            float weld = Mathf.Max(1f, 2f * (fs / 14f));
            float valueW = (Size.X - weld) / (LabelValueRatio + 1f);
            float labelW = Size.X - weld - valueW;

            var labelRect = new Rect2(0, 0, labelW, Size.Y);
            var valueRect = new Rect2(labelW + weld, 0, valueW, Size.Y);

            // Opposite polarity. The label plate is the dark one (L=0.19 measured) and the value
            // plate is driven to maximum lightness (L=1.00 measured) — the brightest thing in
            // the widget, which is what makes the number readable at a glance.
            Color labelPlate = new Color(face.R * 0.34f, face.G * 0.34f, face.B * 0.36f, face.A);
            Color valuePlate = Accent == UiSurface.Role.Neutral
                ? new Color(Mathf.Lerp(face.R, 1f, 0.88f),
                            Mathf.Lerp(face.G, 1f, 0.88f),
                            Mathf.Lerp(face.B, 1f, 0.90f), face.A)
                : UiSurface.Semantic(this, Accent);

            if (State is KitState.Disabled or KitState.Locked)
            {
                // Settled rule, seven independent references: unavailable DRAINS SATURATION
                // rather than dimming. Lightness may even rise.
                labelPlate = Desaturate(labelPlate);
                valuePlate = Desaturate(valuePlate);
            }

            float rimPx = Mathf.Max(1f, g.Rim * 0.5f * (fs / 14f));
            DrawShape(labelRect, ActiveShape, labelPlate, ink, rimPx);
            DrawShape(valueRect, ActiveShape, valuePlate, ink, rimPx);

            if (font == null) return;

            // Label text reads light on its dark plate; value text reads dark on its light one.
            DrawTextIn(font, labelRect, _label, TextOn(labelPlate), fs, HorizontalAlignment.Left,
                       fs * 0.6f);
            DrawTextIn(font, valueRect, _value, TextOn(valuePlate), fs, HorizontalAlignment.Center, 0f);

            DrawAttachments();
        }

        private static Color Desaturate(Color c)
        {
            float l = UiSurface.Luminance(c);
            return new Color(Mathf.Lerp(c.R, l, 0.95f), Mathf.Lerp(c.G, l, 0.95f),
                             Mathf.Lerp(c.B, l, 0.95f), c.A);
        }

        /// <summary>Ink that reads on a given plate. Derived from the plate rather than from the
        /// theme's text colour, because this widget deliberately inverts polarity between its
        /// two halves — one theme colour cannot serve both.</summary>
        private static Color TextOn(Color plate)
            => UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f, 1f)
                : new Color(0.97f, 0.95f, 0.90f, 1f);

        private void DrawTextIn(Font font, Rect2 r, string text, Color col, int fs,
                                HorizontalAlignment align, float padLeft)
        {
            if (string.IsNullOrEmpty(text)) return;
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            float x = align == HorizontalAlignment.Center
                ? r.Position.X + (r.Size.X - m.X) * 0.5f
                : r.Position.X + padLeft;
            float y = r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f;
            DrawText(font, new Vector2(x, y), text, fs, col);
        }
    }
}
