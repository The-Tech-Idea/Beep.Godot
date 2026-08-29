using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// A resource readout as games actually draw it: a circular icon frame overhanging a rounded
    /// capsule plate that carries the number, with a chunky dark outline and a drop shadow.
    ///
    /// This exists because a resource strip made of Label pairs — which is what this HUD had —
    /// is the single thing that makes a game HUD read as an application toolbar. Every reference
    /// in `Example_Art/citybuilder*.png` uses discrete badges clustered in a corner, never a
    /// full-width bar of text. See docs/hud/citybuilder.md section 10.
    ///
    /// Anatomy:
    /// <code>
    ///    ( ◉ )──────────────╮   circular icon frame, overhanging the plate's left edge
    ///    │        4 750     │   capsule plate, optional capacity fill behind the value
    ///    ╰──────────────────╯
    /// </code>
    ///
    /// Drawn with <see cref="StyleBoxFlat"/> rather than DrawRect/DrawCircle: StyleBoxFlat gives
    /// corner radius, border width AND drop shadow natively, which is exactly the chunky
    /// outlined look the references share and which hand-drawn primitives can only approximate.
    /// A square box whose corner radius is half its size renders as a circle, so the icon frame
    /// and the plate come from the same primitive and stay visually consistent.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ResourceBadgeComponent : Godot.Control
    {
        [Export] public Texture2D? Icon { get => _icon; set { if (_icon == value) return; _icon = value; RefreshVisualAndRedraw(); } }
        private Texture2D? _icon;

        /// <summary>The number, already formatted. Kept as a string so the owner controls
        /// grouping, sign and units ("4 750", "-1,040", "60 / 140").</summary>
        [Export] public string Value { get => _value; set { string next = value ?? ""; if (_value == next) return; _value = next; RefreshMinimumAndRedraw(); } }
        private string _value = "0";

        /// <summary>0..1 capacity fill drawn behind the value, as in references 1 and 5 where the
        /// badge doubles as a capacity meter. Negative disables it.</summary>
        [Export(PropertyHint.Range, "-1,1,0.01")]
        public float Fill { get => _fill; set { if (Mathf.IsEqualApprox(_fill, value)) return; _fill = value; RefreshVisualAndRedraw(); } }
        private float _fill = -1f;

        /// <summary>What this readout MEANS — the badge's only colour input. The palette decides
        /// the actual value, so the same HUD reskins with the theme.
        ///
        /// There are deliberately NO exported Colors here. There used to be five, and
        /// citybuilder_main.tscn set IconRingColor on each of its badges, which put a palette
        /// inside a scene file where no skin could reach it: the badges stayed the same blue,
        /// gold, orange, green and violet in all 50 themes. A colour is either the theme's or
        /// the texture's; a component does not get to own one.</summary>
        [Export] public UiSurface.Role Accent
        {
            get => _accent;
            set { if (_accent == value) return; _accent = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _accent = UiSurface.Role.Accent;

        /// <summary>Transient alert role (budget in deficit, power over capacity). Null shows
        /// the declared <see cref="Accent"/>. Kept separate from Accent so clearing an alert
        /// restores the badge's identity colour instead of leaving it stuck on red.</summary>
        public UiSurface.Role? Alert { get => _alert; set { if (_alert == value) return; _alert = value; RefreshVisualAndRedraw(); } }
        private UiSurface.Role? _alert;

        private Color _plate, _ring, _outline, _text, _fillColor;

        /// <summary>Resolve once per draw, through the same helper the framed panels use, so a
        /// badge and the panel behind it cannot disagree about what the surface colour is.</summary>
        private void ResolveColors()
        {
            Color surface = UiSurface.Of(this);
            _plate = surface;
            _outline = UiSurface.Ink(surface);
            _text = GetThemeColor("font_color", "Label");

            // The icon frame carries the role colour — it is the one element that identifies
            // WHICH resource this is, which is exactly what a semantic colour is for.
            _ring = UiSurface.Semantic(this, _alert ?? _accent);

            // The capacity fill is the same role held back, so a full bar reads as more of the
            // same thing rather than as a second unrelated colour.
            _fillColor = _ring with { A = 0.55f };
        }

        /// <summary>Outline thickness. The references are consistently heavy here — a hairline
        /// is what makes UI read as a document rather than an object.</summary>
        [Export] public int OutlineWidth { get => _outlineWidth; set { int next = Mathf.Max(0, value); if (_outlineWidth == next) return; _outlineWidth = next; RefreshVisualAndRedraw(); } }
        private int _outlineWidth = 3;
        /// <summary>Value text size as a multiple of the theme's body font. A readout is
        /// slightly larger than body text; it is NOT a fixed 17px, because the themes run from
        /// 14 to 24 and a fixed size renders 24pt text out of a plate built for 17.</summary>
        [Export(PropertyHint.Range, "0.5,3.0,0.05")] public float FontScale { get => _fontScale; set { float next = Mathf.Clamp(value, 0.5f, 3.0f); if (Mathf.IsEqualApprox(_fontScale, next)) return; _fontScale = next; RefreshMinimumAndRedraw(); } }
        private float _fontScale = 1.18f;

        /// <summary>Resolved value-text size for this draw.</summary>
        private int FontSize => UiSurface.FontSize(this, FontScale);

        /// <summary>Icon diameter as a multiple of the value text, so the frame, the plate and
        /// the number scale together instead of the text outgrowing its own badge.</summary>
        [Export(PropertyHint.Range, "1.0,4.0,0.05")] public float IconScale { get => _iconScale; set { float next = Mathf.Clamp(value, 1.0f, 4.0f); if (Mathf.IsEqualApprox(_iconScale, next)) return; _iconScale = next; RefreshMinimumAndRedraw(); } }
        private float _iconScale = 2.35f;

        private int IconSize => Mathf.RoundToInt(FontSize * IconScale);

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;   // chrome, never a click target
            RefreshMinimumAndRedraw();
        }

        public override Vector2 _GetMinimumSize() => NaturalMinimumSize();

        /// <summary>Set both halves at once — the common case, and it avoids a frame where a new
        /// value is drawn against the previous fill.</summary>
        public void Set(string value, float fill = -1f)
        {
            string nextValue = value ?? "";
            bool valueChanged = _value != nextValue;
            bool fillChanged = !Mathf.IsEqualApprox(_fill, fill);
            if (!valueChanged && !fillChanged) return;

            _value = nextValue;
            _fill = fill;
            if (valueChanged) RefreshMinimumAndRedraw();
            else RefreshVisualAndRedraw();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationThemeChanged) RefreshMinimumAndRedraw(force: true);
        }

        private Vector2 NaturalMinimumSize()
        {
            int fs = FontSize;
            int icon = IconSize;
            float valueWidth = fs * 4.5f;
            if (GetThemeDefaultFont() is { } font && !string.IsNullOrEmpty(_value))
                valueWidth = font.GetStringSize(_value, HorizontalAlignment.Left, -1, fs).X;
            float width = Mathf.Max(fs * 7.5f, icon * 0.78f + valueWidth + fs * 2.2f);
            return new Vector2(width, icon + fs * 0.5f);
        }

        private void RefreshMinimumAndRedraw(bool force = false)
        {
            KitChrome.RefreshAutoMinimumSize(this, NaturalMinimumSize(), force);
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw() => QueueRedraw();

        private StyleBoxFlat Box(Color bg, int radius, bool shadow)
        {
            var b = new StyleBoxFlat { BgColor = bg, BorderColor = _outline };
            b.SetBorderWidthAll(OutlineWidth);
            b.SetCornerRadiusAll(radius);
            if (shadow)
            {
                b.ShadowColor = new Color(0, 0, 0, 0.45f);
                b.ShadowSize = 4;
                b.ShadowOffset = new Vector2(0, 3);
            }
            return b;
        }

        public override void _Draw()
        {
            Vector2 s = Size;
            if (s.X <= 0 || s.Y <= 0) return;
            ResolveColors();

            float d = Mathf.Min(IconSize, s.Y);          // icon diameter
            float plateH = d * 0.78f;                     // plate is shorter, so the icon overhangs
            float plateTop = (s.Y - plateH) * 0.5f;
            float plateLeft = d * 0.45f;                  // icon covers the plate's left end

            var plateRect = new Rect2(plateLeft, plateTop, s.X - plateLeft, plateH);
            DrawStyleBox(Box(_plate, Mathf.RoundToInt(plateH * 0.5f), true), plateRect);

            // Capacity fill sits INSIDE the plate, clipped to its rounded shape by reusing the
            // same corner radius — a square fill would poke out of the capsule ends.
            if (_fill >= 0f)
            {
                float inset = OutlineWidth + 1;
                float usable = plateRect.Size.X - inset * 2;
                var fillRect = new Rect2(plateRect.Position.X + inset, plateRect.Position.Y + inset,
                                         Mathf.Max(0f, usable * Mathf.Clamp(_fill, 0f, 1f)),
                                         plateRect.Size.Y - inset * 2);
                if (fillRect.Size.X > 1f)
                {
                    var fb = new StyleBoxFlat { BgColor = _fillColor };
                    fb.SetCornerRadiusAll(Mathf.RoundToInt(fillRect.Size.Y * 0.5f));
                    DrawStyleBox(fb, fillRect);
                }
            }

            // Value: right-aligned, clear of the icon, vertically centred on the plate.
            var font = GetThemeDefaultFont();
            if (font != null && !string.IsNullOrEmpty(_value))
            {
                float pad = plateH * 0.45f;
                float textAreaW = Mathf.Max(1f, plateRect.Size.X - pad * 2f - d * 0.18f);
                int fs = UiSurface.FitText(this, new Vector2(textAreaW, plateH), 0.58f,
                                           _value, font, min: 7, themeMax: FontScale);
                float textW = font.GetStringSize(_value, HorizontalAlignment.Left, -1, fs).X;
                float x = plateRect.Position.X + plateRect.Size.X - pad - textW;
                x = Mathf.Max(plateRect.Position.X + pad + d * 0.08f, x);
                float y = plateRect.Position.Y + plateRect.Size.Y * 0.5f + fs * 0.36f;
                // Outline first so the number stays legible over any fill or world behind it.
                DrawString(font, new Vector2(x, y), _value, HorizontalAlignment.Left, -1,
                           fs, new Color(0, 0, 0, 0.85f));
                DrawString(font, new Vector2(x - 1, y - 1), _value, HorizontalAlignment.Left, -1,
                           fs, _text);
            }

            // Icon frame LAST so it sits on top of the plate it overhangs — that overlap is what
            // makes the badge read as one object rather than a circle next to a box.
            var iconRect = new Rect2(0, (s.Y - d) * 0.5f, d, d);
            DrawStyleBox(Box(_ring, Mathf.RoundToInt(d * 0.5f), true), iconRect);
            if (_icon != null)
            {
                float pad = d * 0.22f;
                DrawTextureRect(_icon, new Rect2(iconRect.Position.X + pad, iconRect.Position.Y + pad,
                                                 d - pad * 2, d - pad * 2), false);
            }
        }
    }
}
