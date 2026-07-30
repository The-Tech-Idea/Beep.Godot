using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// An inventory / hotbar / recipe slot grid.
    ///
    /// Measured from Example_Art/gameui9.png, rpg3.png, gameui2.png and gameui8.png:
    ///
    ///  - <b>interior : pitch = 0.58</b> (gameui9: 49px interior on an ~85px pitch), so the gap
    ///    between slots is a real part of the design rather than a layout leftover.
    ///  - <b>Selection is a 3px pure-white rectangle drawn OUTSIDE the slot</b> (gameui9) — not
    ///    a fill change, not a glow. It survives greyscale and works over any slot contents,
    ///    which is exactly why that sheet uses it.
    ///  - <b>Empty slots desaturate rather than darken</b>: rpg3 measures an available slot at
    ///    L≈0.67-0.72 S=0.65-0.72 and an empty one at L=0.26 <b>S=0.05</b>. The settled 7x rule
    ///    is that unavailable drains SATURATION; lightness may even rise.
    ///  - <b>Locked states state their requirement in words</b> (5 references), so a locked slot
    ///    carries a reason string and not just a padlock glyph.
    ///  - Count badges sit at the <b>bottom-right corner, straddling it</b> (gameui8).
    ///
    /// KitState.Empty has three distinct meanings on one screen in ui3.png — blank, an invite
    /// "+", and locked-with-a-requirement — so <see cref="SlotKind"/> models all three rather
    /// than collapsing them into "not filled".
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitSlotGrid : KitControl
    {
        public enum SlotKind { Filled, Blank, Invite, Locked }

        public sealed class Slot
        {
            public SlotKind Kind = SlotKind.Blank;
            public Texture2D? Icon;
            /// <summary>Stack count. Drawn as a corner badge when above 1.</summary>
            public int Count;
            /// <summary>Shown for <see cref="SlotKind.Locked"/> — in words, per the 5x rule.</summary>
            public string Requirement = "";
            /// <summary>Rarity/quality tint on the slot background (gameui8). Neutral = none.</summary>
            public UiSurface.Role Tint = UiSurface.Role.Neutral;
        }

        [Export(PropertyHint.Range, "1,12,1")] public int Columns { get => _cols; set { _cols = Mathf.Max(1, value); QueueRedraw(); } }
        private int _cols = 4;

        [Export(PropertyHint.Range, "1,12,1")] public int Rows { get => _rows; set { _rows = Mathf.Max(1, value); QueueRedraw(); } }
        private int _rows = 3;

        /// <summary>Selected index, or -1. Drawn as an outline OUTSIDE the slot.</summary>
        [Export] public int Selected { get => _sel; set { _sel = value; QueueRedraw(); } }
        private int _sel = -1;

        /// <summary>Interior as a fraction of pitch. 0.58 measured; lower widens the gutters.</summary>
        [Export(PropertyHint.Range, "0.3,1.0,0.01")] public float InteriorRatio { get; set; } = 0.58f;

        public readonly List<Slot> Slots = new();

        [Signal] public delegate void SlotActivatedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                float pitch = fs * 3.2f;
                CustomMinimumSize = new Vector2(pitch * _cols, pitch * _rows);
            }
        }

        private float Pitch() => Mathf.Min(Size.X / _cols, Size.Y / _rows);

        private Rect2 SlotRect(int i)
        {
            float pitch = Pitch();
            float interior = pitch * InteriorRatio;
            float pad = (pitch - interior) * 0.5f;
            int cx = i % _cols, cy = i / _cols;
            return new Rect2(cx * pitch + pad, cy * pitch + pad, interior, interior);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
                return;
            for (int i = 0; i < _cols * _rows; i++)
            {
                if (!SlotRect(i).HasPoint(mb.Position)) continue;
                Selected = i;
                EmitSignal(SignalName.SlotActivated, i);
                AcceptEvent();
                return;
            }
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = GetThemeDefaultFont();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * 0.6f * (fs / 14f));
            int total = _cols * _rows;

            for (int i = 0; i < total; i++)
            {
                Rect2 r = SlotRect(i);
                if (r.Size.X < 2f) continue;
                Slot s = i < Slots.Count ? Slots[i] : new Slot();

                // Slots are content wells, so they take WellShade — not the readout recess,
                // which renders a grid of black holes.
                float ps = g.WellShade;
                Color plate = new Color(face.R * ps, face.G * ps, face.B * ps, 1f);

                if (s.Tint != UiSurface.Role.Neutral && s.Kind == SlotKind.Filled)
                {
                    Color t = UiSurface.Semantic(this, s.Tint);
                    plate = new Color(Mathf.Lerp(plate.R, t.R, 0.45f),
                                      Mathf.Lerp(plate.G, t.G, 0.45f),
                                      Mathf.Lerp(plate.B, t.B, 0.45f), 1f);
                }

                if (s.Kind is SlotKind.Blank or SlotKind.Locked or SlotKind.Invite)
                {
                    // Drain saturation, do not merely darken (7x settled rule).
                    float l = UiSurface.Luminance(plate);
                    plate = new Color(Mathf.Lerp(plate.R, l, 0.92f), Mathf.Lerp(plate.G, l, 0.92f),
                                      Mathf.Lerp(plate.B, l, 0.92f), 1f);
                }

                DrawShape(r, ActiveShape, plate, ink, rimPx);

                switch (s.Kind)
                {
                    case SlotKind.Filled when s.Icon != null:
                        DrawTextureRect(s.Icon, r.Grow(-r.Size.X * 0.14f), false);
                        break;
                    case SlotKind.Invite:
                        DrawPlus(r, ink);
                        break;
                    case SlotKind.Locked:
                        DrawCross(r);
                        break;
                }

                if (s.Kind == SlotKind.Filled && s.Count > 1 && font != null)
                    DrawCountBadge(r, s.Count, font, fs, ink);

                // Locked slots say WHY, in words. A padlock alone is the thing the references
                // consistently do NOT do.
                if (s.Kind == SlotKind.Locked && !string.IsNullOrEmpty(s.Requirement) && font != null)
                {
                    int small = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                                  new Vector2(r.Size.X * 1.3f, r.Size.Y * 0.30f),
                                                  s.Requirement, font, min: 8);
                    Vector2 m = font.GetStringSize(s.Requirement, HorizontalAlignment.Left, -1, small);
                    if (m.X <= r.Size.X * 1.35f)
                        DrawString(font,
                                   new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                               r.End.Y - small * 0.15f),
                                   s.Requirement, HorizontalAlignment.Left, -1, small,
                                   UiSurface.Text(this));
                }
            }

            // Selection LAST and OUTSIDE the slot: a 3px white rectangle around it, so it reads
            // over any contents and does not restyle the slot itself.
            if (_sel >= 0 && _sel < total)
            {
                Rect2 sel = SlotRect(_sel).Grow(Mathf.Max(2f, Pitch() * 0.045f));
                DrawShape(sel, ActiveShape, new Color(0, 0, 0, 0), new Color(1f, 0.97f, 0.92f),
                          Mathf.Max(2f, 3f * (fs / 14f)));
            }

            DrawAttachments();
        }

        private void DrawPlus(Rect2 r, Color ink)
        {
            var c = r.Position + r.Size * 0.5f;
            float a = r.Size.X * 0.20f, w = Mathf.Max(2f, r.Size.X * 0.07f);
            var col = new Color(ink.R, ink.G, ink.B, 0.75f);
            DrawLine(c - new Vector2(a, 0), c + new Vector2(a, 0), col, w);
            DrawLine(c - new Vector2(0, a), c + new Vector2(0, a), col, w);
        }

        private void DrawCross(Rect2 r)
        {
            var c = r.Position + r.Size * 0.5f;
            float a = r.Size.X * 0.18f, w = Mathf.Max(2f, r.Size.X * 0.07f);
            var col = new Color(0.85f, 0.85f, 0.87f, 0.55f);
            DrawLine(c - new Vector2(a, a), c + new Vector2(a, a), col, w);
            DrawLine(c - new Vector2(a, -a), c + new Vector2(a, -a), col, w);
        }

        /// <summary>Bottom-right, straddling the corner (gameui8).</summary>
        private void DrawCountBadge(Rect2 r, int count, Font font, int fs, Color ink)
        {
            string txt = count.ToString();
            // Sized off the SLOT, not the theme. At a flat 0.72x body size a count badge was
            // barely legible on a large slot and identical on a small one -- the badge is drawn
            // inside a box this widget controls, so the box sets the type.
            int small = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                          new Vector2(r.Size.X * 0.55f, r.Size.Y * 0.42f),
                                          txt, font, min: 9);
            Vector2 m = font.GetStringSize(txt, HorizontalAlignment.Left, -1, small);
            float w = Mathf.Max(m.X + small * 0.7f, small * 1.4f), h = small * 1.25f;
            var b = new Rect2(r.End.X - w * 0.55f, r.End.Y - h * 0.55f, w, h);
            DrawShape(b, KitShape.Pill, UiSurface.Semantic(this, UiSurface.Role.Warning), ink, 1.5f);
            DrawString(font, new Vector2(b.Position.X + (b.Size.X - m.X) * 0.5f,
                                         b.Position.Y + (b.Size.Y + m.Y * 0.6f) * 0.5f),
                       txt, HorizontalAlignment.Left, -1, small, new Color(0.10f, 0.09f, 0.08f));
        }
    }
}
