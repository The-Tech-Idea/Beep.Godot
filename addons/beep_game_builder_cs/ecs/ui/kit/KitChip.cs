using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// The small-parts family from CATALOGUE-FROM-ART.md section C, as ONE widget with variants:
    /// <b>RarityChip, FlashBadge, CountBubble, NotificationDot, LockOverlay</b>.
    ///
    /// They are one widget because they are one shape with different payloads — a small plate
    /// pinned to or straddling something larger, carrying at most a few characters. Building five
    /// classes would have five bevels drifting apart, which is the exact failure mode
    /// <see cref="KitMaterial"/> exists to prevent.
    ///
    /// The pentagon status chip (a tick or a cross) is called out separately in PLAN.md phase D;
    /// it is <see cref="ChipKind.Status"/> here, cut to <see cref="KitShape.Pentagon"/>.
    ///
    /// Settled rules honoured:
    ///  - <b>Top-right corner straddle is the attention anchor</b> (8 independent references), so
    ///    that is the default anchor when this chip is attached to a host.
    ///  - <b>Badge colour carries a ROLE</b> (ui8: green = new content, red = action required),
    ///    so the colour comes from <see cref="UiSurface.Role"/> and never from a literal.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitChip : KitControl
    {
        /// <summary>A chip: takes the theme's chip corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Chip;

        public enum ChipKind
        {
            /// <summary>A word or short label — rarity, tier, "NEW".</summary>
            Rarity,
            /// <summary>A number in a pill — stack counts, unread counts.</summary>
            Count,
            /// <summary>A bare dot with no text: something changed here.</summary>
            Dot,
            /// <summary>Pentagon with a tick or cross.</summary>
            Status,
            /// <summary>A padlock plate over its host, WITH its requirement in words.</summary>
            Lock,
            /// <summary>
            /// A COMPARISON delta — art-pass file 46, and nothing else in the folder does it.
            ///
            /// Stat chips turn green with an up-arrow to show the change against what is
            /// currently equipped. It is not a Status (that is a yes/no) and not a Count (that is
            /// a quantity): it is a SIGNED difference, and it colours itself from the sign rather
            /// than from a role, because "+3 armour" is good and "-3" is not regardless of what
            /// palette role the caller had in mind.
            /// </summary>
            Delta,
        }

        [Export] public ChipKind Kind { get => _kind; set { _kind = value; QueueRedraw(); } }
        private ChipKind _kind = ChipKind.Rarity;

        [Export] public string Text { get => _text; set { _text = value ?? ""; QueueRedraw(); } }
        private string _text = "NEW";

        [Export] public UiSurface.Role Role { get; set; } = UiSurface.Role.Danger;

        /// <summary>True for a tick, false for a cross. Only used by <see cref="ChipKind.Status"/>.</summary>
        /// <summary>
        /// For <see cref="ChipKind.Delta"/>: the signed difference against what is equipped.
        /// Its SIGN drives the colour, not <see cref="Role"/> -- "+3 armour" is good and "-3" is
        /// not, whatever palette role the caller had in mind.
        /// </summary>
        [Export] public float Delta { get => _delta; set { _delta = value; QueueRedraw(); } }
        private float _delta = 3f;

        [Export] public bool Positive { get; set; } = true;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = _kind switch
                {
                    ChipKind.Dot => new Vector2(fs * 0.7f, fs * 0.7f),
                    ChipKind.Status => new Vector2(fs * 1.6f, fs * 1.6f),
                    ChipKind.Count => new Vector2(fs * 1.6f, fs * 1.25f),
                    ChipKind.Delta => new Vector2(fs * 2.6f, fs * 1.3f),
                    _ => new Vector2(fs * 3.6f, fs * 1.3f),
                };
            }
        }

        private KitShape ShapeFor() => _kind switch
        {
            ChipKind.Dot or ChipKind.Count or ChipKind.Delta => KitShape.Pill,
            ChipKind.Status => KitShape.Pentagon,
            _ => ActiveShape,
        };

        public override void _Draw()
        {
            if (Size.X < 3f || Size.Y < 3f) return;

            var r = new Rect2(Vector2.Zero, Size);
            // A DELTA colours itself from its SIGN. Everything else takes the declared role.
            Color fill = _kind == ChipKind.Delta
                ? UiSurface.Semantic(this, _delta >= 0f ? UiSurface.Role.Success
                                                       : UiSurface.Role.Danger)
                : UiSurface.Semantic(this, Role);
            if (fill.A < 0.02f) fill = UiSurface.Of(this);
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            float rimPx = Mathf.Max(1.5f, Geo.Rim * 0.7f * (fs / 14f));

            DrawShape(r, ShapeFor(), fill, ink, rimPx);

            // A dot says "something here" and carries no text by definition.
            if (_kind == ChipKind.Dot) return;

            Color on = UiSurface.Luminance(fill) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f);

            if (_kind == ChipKind.Status)
            {
                DrawMark(r, on);
                return;
            }

            if (_kind == ChipKind.Delta)
            {
                // Arrow DRAWN, number typed. The arrow must not depend on the theme font
                // carrying a glyph for it -- the pixel and blackletter faces do not.
                DrawArrow(r, on, _delta >= 0f);
                if (font == null) return;
                string txt = (_delta >= 0f ? "+" : "") + _delta.ToString("0.##");
                int dfs = UiSurface.FitText(this, new Vector2(r.Size.X * 0.58f, r.Size.Y * 0.86f),
                                            0.66f, txt, font, min: 7, themeMax: 0.82f);
                Vector2 dm = font.GetStringSize(txt, HorizontalAlignment.Left, -1, dfs);
                DrawText(font, new Vector2(r.Position.X + r.Size.X * 0.60f - dm.X * 0.5f,
                                           r.Position.Y + (r.Size.Y + dm.Y * 0.6f) * 0.5f),
                         txt, dfs, on);
                return;
            }

            if (font == null || string.IsNullOrEmpty(_text)) return;
            int size = UiSurface.FitText(this, r.Size * 0.82f, 0.66f, _text, font, min: 7, themeMax: 0.85f);
            Vector2 m = font.GetStringSize(_text, HorizontalAlignment.Left, -1, size);
            DrawText(font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.6f) * 0.5f),
                       _text, size, on);
        }

        /// <summary>The delta's arrow, on the chip's left third.</summary>
        private void DrawArrow(Rect2 r, Color col, bool up)
        {
            float a = Mathf.Min(r.Size.X, r.Size.Y) * 0.26f;
            var c = new Vector2(r.Position.X + r.Size.X * 0.22f, r.Position.Y + r.Size.Y * 0.5f);
            float dir = up ? -1f : 1f;
            DrawColoredPolygon(new[]
            {
                c + new Vector2(0f, a * dir),
                c + new Vector2(-a * 0.85f, -a * 0.35f * dir),
                c + new Vector2(a * 0.85f, -a * 0.35f * dir),
            }, col);
        }

        /// <summary>Tick or cross, drawn rather than typed, so it does not depend on the theme
        /// font carrying those glyphs.</summary>
        private void DrawMark(Rect2 r, Color col)
        {
            var c = r.Position + r.Size * 0.5f;
            float a = Mathf.Min(r.Size.X, r.Size.Y) * 0.22f;
            float w = Mathf.Max(2f, a * 0.42f);
            if (Positive)
            {
                DrawLine(c + new Vector2(-a, 0f), c + new Vector2(-a * 0.25f, a * 0.75f), col, w);
                DrawLine(c + new Vector2(-a * 0.25f, a * 0.75f), c + new Vector2(a, -a * 0.7f), col, w);
            }
            else
            {
                DrawLine(c + new Vector2(-a, -a), c + new Vector2(a, a), col, w);
                DrawLine(c + new Vector2(-a, a), c + new Vector2(a, -a), col, w);
            }
        }
    }
}
