using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A row of currency / resource readouts, each a capsule with its icon cap OVERHANGING the
    /// left end.
    ///
    /// CATALOGUE-FROM-ART.md section A lists this first: it "appears in nearly every picture",
    /// and its build order puts it in the top tier because it carries the most screens per unit
    /// of work. Measured from citybuilder5's StoneCapsule row (x6 across the top of that HUD):
    ///
    /// | part          | measured                                              |
    /// |---------------|-------------------------------------------------------|
    /// | capsule       | **35px** tall                                         |
    /// | frame         | 7px top, 5px bottom — the frame is ASYMMETRIC         |
    /// | inner plate   | **0.12 x the frame's lightness** — nearly black       |
    /// | gloss band    | ~8px at the TOP of the plate                          |
    ///
    /// Note this is the widget class the 0.12 plate shade was actually measured on — a small
    /// readout sunk into a pale frame — so unlike a panel well it uses
    /// <see cref="KitGeometry.PlateShadeFor"/>'s recessed value rather than
    /// <see cref="KitGeometry.WellShade"/>.
    ///
    /// Icon overhang is per-skin (citybuilder1 1.48x vs citybuilder2 1.0x), so
    /// <see cref="IconOverhang"/> is exposed rather than fixed.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitCurrencyBar : KitControl
    {
        /// <summary>A bar: takes the theme's bar corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Bar;

        public sealed class Entry
        {
            public string Value = "0";
            public Texture2D? Icon;
            /// <summary>Fallback when there is no icon, so a capsule is never blank.</summary>
            public string Glyph = "";
            public UiSurface.Role Accent = UiSurface.Role.Warning;
        }

        public readonly List<Entry> Entries = new();

        /// <summary>How far the icon cap hangs past the capsule's left end, as a multiple of the
        /// capsule height. 1.48 and 1.0 are both measured; neither is universal.</summary>
        [Export(PropertyHint.Range, "0.6,1.8,0.01")] public float IconOverhang { get; set; } = 1.2f;

        /// <summary>Gap between capsules, as a multiple of the capsule height.</summary>
        [Export(PropertyHint.Range, "0.1,1.5,0.05")] public float Spacing { get; set; } = 0.5f;

        public override void _Ready()
        {
            base._Ready();
            if (Entries.Count == 0)
                Entries.AddRange(new[]
                {
                    new Entry { Value = "1,240", Glyph = "$", Accent = UiSurface.Role.Warning },
                    new Entry { Value = "38", Glyph = "*", Accent = UiSurface.Role.Info },
                    new Entry { Value = "7", Glyph = "+", Accent = UiSurface.Role.Success },
                });
            if (CustomMinimumSize == Vector2.Zero)
                CustomMinimumSize = _GetMinimumSize();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float h = fs * 2.2f;
            return new Vector2(h * 4.2f * Mathf.Max(1, Entries.Count), h * 1.25f);
        }

        /// <summary>Set one entry's value by index, the call a HUD binder makes each tick.</summary>
        public void SetValue(int index, string value)
        {
            if (index < 0 || index >= Entries.Count) return;
            Entries[index].Value = value ?? "";
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 6 || Entries.Count == 0) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);

            // The capsule is shorter than the control, leaving room for the cap to hang below
            // and above without being clipped by the control's own bounds.
            float h = Mathf.Min(Size.Y * 0.8f, fs * 2.2f);
            float capR = h * IconOverhang * 0.5f;
            float y = (Size.Y - h) * 0.5f;

            float x = capR;                    // first capsule starts clear of its own cap
            float gap = h * Spacing;
            float each = Mathf.Max(h * 2.2f, (Size.X - capR - gap * (Entries.Count - 1)) / Entries.Count);

            foreach (var e in Entries)
            {
                if (x >= Size.X) break;
                var capsule = new Rect2(x, y, Mathf.Min(each, Size.X - x), h);

                // Frame, then the recessed inner plate at the measured 0.12.
                DrawShape(capsule, KitShape.Pill, face, RimColor(),
                          Mathf.Max(1f, g.Rim * 0.8f * (fs / 14f)));

                float ft = Mathf.Max(2f, h * 0.16f);     // 7px on a 35px capsule
                var plate = new Rect2(capsule.Position + new Vector2(ft, ft),
                                      capsule.Size - new Vector2(ft * 2f, ft * 1.7f));
                if (plate.Size.X > 3 && plate.Size.Y > 3)
                {
                    float ps = g.PlateShadeFor(KitElevation.Recessed);
                    DrawShape(plate, KitShape.Pill,
                              new Color(face.R * ps, face.G * ps, face.B * ps, 1f), ink, 0f);

                    // Gloss band across the TOP of the plate, ~8px of a 35px capsule.
                    if (g.Gloss > 0f)
                    {
                        var band = new Rect2(plate.Position + new Vector2(plate.Size.X * 0.06f, plate.Size.Y * 0.08f),
                                             new Vector2(plate.Size.X * 0.88f, plate.Size.Y * 0.30f));
                        if (band.Size.Y > 1.5f)
                            DrawShape(band, KitShape.Pill, new Color(1, 1, 1, 0.14f * g.Gloss),
                                      new Color(0, 0, 0, 0), 0f);
                    }
                }

                // Value, CLEAR of the cap.
                //
                // The cap overhangs INTO the capsule by capR and is drawn LAST, so anything at
                // x < capsule.X + capR gets painted over. The text started at capR * 0.9 — inside
                // that — and the leading character was buried under the icon: "12,480" rendered
                // as "2,480" with a sliver of the 1. Start past the cap's real right edge, budget
                // the fit against what is actually left, and centre it in that well instead of
                // jamming it against the cap with dead space trailing off to the right.
                if (font != null && !string.IsNullOrEmpty(e.Value))
                {
                    float padX = Mathf.Max(3f, h * 0.14f);
                    float textX0 = capsule.Position.X + capR + padX;
                    float avail = capsule.End.X - padX - textX0;
                    if (avail > 6f)
                    {
                        int vf = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                                   new Vector2(avail, capsule.Size.Y * 0.52f),
                                                   e.Value, font, min: 8);
                        Vector2 m = font.GetStringSize(e.Value, HorizontalAlignment.Left, -1, vf);
                        float tx = textX0 + Mathf.Max(0f, (avail - m.X) * 0.5f);
                        DrawText(font, new Vector2(tx, capsule.Position.Y + (capsule.Size.Y + m.Y * 0.6f) * 0.5f),
                                 e.Value, vf, new Color(0.97f, 0.95f, 0.90f));
                    }
                }

                // The cap LAST and OVERHANGING the capsule's left end — the element that makes
                // this read as a game currency chip rather than a labelled text field.
                var cap = new Rect2(capsule.Position.X - capR, capsule.Position.Y + (h - capR * 2f) * 0.5f,
                                    capR * 2f, capR * 2f);
                Color capCol = UiSurface.Semantic(this, e.Accent);
                DrawShape(cap, KitShape.Round, capCol, ink, Mathf.Max(1.5f, g.Rim * 0.8f * (fs / 14f)));

                if (e.Icon != null)
                    DrawTextureRect(e.Icon, cap.Grow(-cap.Size.X * 0.22f), false);
                else if (font != null && !string.IsNullOrEmpty(e.Glyph))
                {
                    int gs = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                               cap.Size * 0.58f, e.Glyph, font, min: 8);
                    Vector2 m = font.GetStringSize(e.Glyph, HorizontalAlignment.Left, -1, gs);
                    DrawText(font, new Vector2(cap.Position.X + (cap.Size.X - m.X) * 0.5f, cap.Position.Y + (cap.Size.Y + m.Y * 0.6f) * 0.5f),
                               e.Glyph, gs, UiSurface.Luminance(capCol) > 0.5f
                                   ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
                }

                x += capsule.Size.X + gap;
            }

            DrawAttachments();
        }
    }
}
