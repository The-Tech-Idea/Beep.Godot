using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A welded group of icon buttons where exactly one is active — CATALOGUE-FROM-ART.md
    /// section D's `SegmentedIconGroup`, from `settings1.png`.
    ///
    /// This is the game form's radio group: quality presets, camera modes, info-view overlays.
    /// Welded rather than spaced, because the join is what says "these are alternatives" — three
    /// separate buttons say "these are three independent actions".
    ///
    /// Only the ends are rounded; interior corners are square so the segments read as one bar.
    /// Selection is a FILL, matching the convention for a control whose members sit in a strip.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitSegmentedIconGroup : KitControl
    {
        public sealed class Segment
        {
            public string Glyph = "";
            public Texture2D? Icon;
            public string Tip = "";
        }

        public readonly List<Segment> Segments = new();

        [Export] public int Current
        {
            get => _current;
            set
            {
                if (Segments.Count == 0) { _current = 0; return; }
                int v = Mathf.Clamp(value, 0, Segments.Count - 1);
                if (v == _current) return;
                _current = v; QueueRedraw(); EmitSignal(SignalName.SegmentChanged, v);
            }
        }
        private int _current;
        private int _hover = -1;

        [Signal] public delegate void SegmentChangedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
            FocusMode = FocusModeEnum.All;
            if (Segments.Count == 0)
                Segments.AddRange(new[]
                {
                    new Segment { Glyph = "1" }, new Segment { Glyph = "2" }, new Segment { Glyph = "3" },
                });
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 2.6f * Segments.Count, fs * 2.4f);
            }
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 2.6f * Mathf.Max(1, Segments.Count), fs * 2.4f);
        }

        private Rect2 SegRect(int i)
        {
            float w = Size.X / Mathf.Max(1, Segments.Count);
            return new Rect2(i * w, 0f, w, Size.Y);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey key)
            {
                Vector2I dir = KitChrome.DirectionFromKey(key);
                if (dir.X <= -9999) { Current = 0; AcceptEvent(); }
                else if (dir.X >= 9999) { Current = Segments.Count - 1; AcceptEvent(); }
                else if (dir.X < 0) { Current = Mathf.Max(0, _current - 1); AcceptEvent(); }
                else if (dir.X > 0) { Current = Mathf.Min(Segments.Count - 1, _current + 1); AcceptEvent(); }
                else if (KitChrome.IsConfirmKey(key))
                {
                    EmitSignal(SignalName.SegmentChanged, _current);
                    AcceptEvent();
                }
                return;
            }

            if (@event is InputEventMouseMotion mm)
            {
                int next = HitSegment(mm.Position);
                if (next != _hover)
                {
                    _hover = next;
                    QueueRedraw();
                }
                return;
            }

            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
                return;
            int hit = HitSegment(mb.Position);
            if (hit >= 0)
            {
                GrabFocus();
                Current = hit;
                AcceptEvent();
            }
        }

        private int HitSegment(Vector2 p)
        {
            for (int i = 0; i < Segments.Count; i++)
                if (SegRect(i).HasPoint(p)) return i;
            return -1;
        }

        public override void _Draw()
        {
            if (Size.X < 16f || Size.Y < 8f || Segments.Count == 0) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            Color acc = UiSurface.Semantic(this, UiSurface.Role.Accent);
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1.5f, g.Rim * 0.7f * (fs / 14f));

            // One plate under the whole strip, so the group reads as a single object.
            DrawShape(new Rect2(Vector2.Zero, Size), ActiveShape,
                      new Color(face.R * g.WellShade, face.G * g.WellShade, face.B * g.WellShade, 1f),
                      ink, rimPx);

            for (int i = 0; i < Segments.Count; i++)
            {
                Rect2 r = SegRect(i);
                bool sel = i == _current;

                if (sel)
                {
                    // Inset slightly so the group's own outline still frames the selection.
                    var fillRect = r.Grow(-rimPx);
                    if (fillRect.Size.X > 2f && fillRect.Size.Y > 2f)
                        DrawShape(fillRect, ActiveShape, acc, ink, 0f);
                }
                else if (_hover == i)
                {
                    var fillRect = r.Grow(-rimPx);
                    if (fillRect.Size.X > 2f && fillRect.Size.Y > 2f)
                    {
                        Color hover = UiSurface.Semantic(this, UiSurface.Role.Info);
                        DrawShape(fillRect, ActiveShape, new Color(hover.R, hover.G, hover.B, 0.42f), ink, 0f);
                    }
                }
                else if (i > 0)
                {
                    // Divider between unselected members — the weld line.
                    DrawLine(new Vector2(r.Position.X, r.Position.Y + Size.Y * 0.18f),
                             new Vector2(r.Position.X, r.End.Y - Size.Y * 0.18f),
                             new Color(ink.R, ink.G, ink.B, 0.6f), Mathf.Max(1f, rimPx * 0.6f));
                }

                Color on = sel
                    ? (UiSurface.Luminance(acc) > 0.5f ? new Color(0.10f, 0.09f, 0.08f)
                                                      : new Color(0.98f, 0.96f, 0.92f))
                    : UiSurface.Text(this);

                if (Segments[i].Icon != null)
                {
                    float s = Mathf.Min(r.Size.X, r.Size.Y) * g.GlyphRatio;
                    DrawTextureRect(Segments[i].Icon,
                                    new Rect2(r.Position + (r.Size - new Vector2(s, s)) * 0.5f,
                                              new Vector2(s, s)), false, on);
                }
                else if (font != null && !string.IsNullOrEmpty(Segments[i].Glyph))
                {
                    int gf = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                               new Vector2(r.Size.X * 0.64f, r.Size.Y * 0.58f),
                                               Segments[i].Glyph, font, min: 8);
                    Vector2 m = font.GetStringSize(Segments[i].Glyph, HorizontalAlignment.Left, -1, gf);
                    DrawText(font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.6f) * 0.5f),
                               Segments[i].Glyph, gf, on);
                }
            }

            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size), ActiveShape, 0.8f);
        }
    }
}
