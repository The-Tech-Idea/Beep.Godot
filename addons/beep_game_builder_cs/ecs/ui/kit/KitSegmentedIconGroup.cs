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
        /// <summary>A row of pressable segments in a track -- pressed, so Button, notwithstanding the recessed plate underneath them.
        /// Stated explicitly rather than inherited from KitControl's default, so that every
        /// widget's class is a decision on the record instead of whatever the base happens
        /// to return.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Button;

        public sealed class Segment
        {
            public string Glyph = "";
            public Texture2D? Icon;
            public string Tip = "";
        }

        public readonly List<Segment> Segments = new();

        [Export]
        public string[] SegmentGlyphs
        {
            get
            {
                var glyphs = new string[Segments.Count];
                for (int i = 0; i < Segments.Count; i++)
                    glyphs[i] = Segments[i].Glyph;
                return glyphs;
            }
            set => SetSegmentGlyphs(value);
        }

        [Export]
        public string[] SegmentTips
        {
            get
            {
                var tips = new string[Segments.Count];
                for (int i = 0; i < Segments.Count; i++)
                    tips[i] = Segments[i].Tip;
                return tips;
            }
            set => SetSegmentTips(value);
        }

        [Export]
        public Texture2D[] SegmentIcons
        {
            get
            {
                var icons = new Texture2D[Segments.Count];
                for (int i = 0; i < Segments.Count; i++)
                    icons[i] = Segments[i].Icon!;
                return icons;
            }
            set => SetSegmentIcons(value);
        }

        public void SetSegments(IEnumerable<Segment>? segments)
        {
            List<Segment> next = NormalizeSegments(segments);
            if (SameSegments(Segments, next)) return;
            Segments.Clear();
            Segments.AddRange(next);
            RefreshSegments();
        }

        public void SetSegmentGlyphs(string[]? glyphs)
        {
            int count = glyphs?.Length ?? 0;
            bool changed = Segments.Count != count;
            while (Segments.Count > count)
                Segments.RemoveAt(Segments.Count - 1);
            for (int i = 0; i < count; i++)
            {
                EnsureSegment(i);
                string next = glyphs![i] ?? "";
                if (Segments[i].Glyph == next) continue;
                Segments[i].Glyph = next;
                changed = true;
            }
            if (!changed) return;
            RefreshSegments();
        }

        public void SetSegmentTips(string[]? tips)
        {
            if (tips == null)
            {
                bool changed = false;
                for (int i = 0; i < Segments.Count; i++)
                {
                    if (Segments[i].Tip == "") continue;
                    Segments[i].Tip = "";
                    changed = true;
                }
                if (!changed) return;
                RefreshSegments();
                return;
            }

            bool updated = false;
            for (int i = 0; i < tips.Length; i++)
            {
                EnsureSegment(i);
                string next = tips[i] ?? "";
                if (Segments[i].Tip == next) continue;
                Segments[i].Tip = next;
                updated = true;
            }
            for (int i = tips.Length; i < Segments.Count; i++)
            {
                if (Segments[i].Tip == "") continue;
                Segments[i].Tip = "";
                updated = true;
            }
            if (!updated) return;
            RefreshSegments();
        }

        public void SetSegmentIcons(Texture2D[]? icons)
        {
            if (icons == null)
            {
                bool changed = false;
                for (int i = 0; i < Segments.Count; i++)
                {
                    if (Segments[i].Icon == null) continue;
                    Segments[i].Icon = null;
                    changed = true;
                }
                if (!changed) return;
                RefreshSegments();
                return;
            }

            bool updated = false;
            for (int i = 0; i < icons.Length; i++)
            {
                EnsureSegment(i);
                if (Segments[i].Icon == icons[i]) continue;
                Segments[i].Icon = icons[i];
                updated = true;
            }
            for (int i = icons.Length; i < Segments.Count; i++)
            {
                if (Segments[i].Icon == null) continue;
                Segments[i].Icon = null;
                updated = true;
            }
            if (!updated) return;
            RefreshSegments();
        }

        public void AddSegment(string glyph, Texture2D? icon = null, string tip = "")
        {
            Segments.Add(new Segment { Glyph = glyph ?? "", Icon = icon, Tip = tip ?? "" });
            RefreshSegments();
        }

        public bool RemoveSegment(int index)
        {
            if (index < 0 || index >= Segments.Count)
                return false;

            Segments.RemoveAt(index);
            if (index <= _current)
                _current = Mathf.Max(0, _current - 1);
            RefreshSegments();
            return true;
        }

        public void ClearSegments()
        {
            if (Segments.Count == 0 && _current == 0)
                return;

            Segments.Clear();
            _current = 0;
            RefreshSegments();
        }

        public void RefreshSegments()
        {
            if (Segments.Count == 0)
                _current = 0;
            else
                _current = Mathf.Clamp(_current, 0, Segments.Count - 1);
            _hover = -1;
            RefreshMinimumAndRedraw();
        }

        private void EnsureSegment(int index)
        {
            while (Segments.Count <= index)
                Segments.Add(new Segment());
        }

        private static List<Segment> NormalizeSegments(IEnumerable<Segment>? segments)
        {
            var next = new List<Segment>();
            if (segments == null)
                return next;

            foreach (Segment? segment in segments)
            {
                next.Add(new Segment
                {
                    Glyph = segment?.Glyph ?? "",
                    Icon = segment?.Icon,
                    Tip = segment?.Tip ?? "",
                });
            }
            return next;
        }

        private static bool SameSegments(IReadOnlyList<Segment> left, IReadOnlyList<Segment> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if ((left[i].Glyph ?? "") != (right[i].Glyph ?? "")) return false;
                if (!ReferenceEquals(left[i].Icon, right[i].Icon)) return false;
                if ((left[i].Tip ?? "") != (right[i].Tip ?? "")) return false;
            }
            return true;
        }

        [Export] public int Current
        {
            get => _current;
            set
            {
                if (Segments.Count == 0) { _current = 0; return; }
                int v = Mathf.Clamp(value, 0, Segments.Count - 1);
                if (v == _current) return;
                _current = v;
                RefreshVisualAndRedraw();
                if (IsInsideTree())
                    EmitSignal(SignalName.SegmentChanged, v);
            }
        }
        private int _current;
        private int _hover = -1;
        private bool _eventsHooked;

        [Signal] public delegate void SegmentChangedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            if (!_eventsHooked)
            {
                MouseExited += ClearHover;
                _eventsHooked = true;
            }
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearHover();
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

        /// <summary>Move the active segment, reporting whether it moved. At either end the key is
        /// released so focus leaves the group rather than stalling on its last segment.</summary>
        private bool SelectByArrow(Vector2I dir)
        {
            if (Segments.Count <= 0) return false;

            int wanted;
            if (dir.X <= -KitChrome.Jump) wanted = 0;
            else if (dir.X >= KitChrome.Jump) wanted = Segments.Count - 1;
            else if (dir.X < 0) wanted = _current - 1;
            else if (dir.X > 0) wanted = _current + 1;
            else return false;

            wanted = Mathf.Clamp(wanted, 0, Segments.Count - 1);
            if (wanted == _current) return false;
            Current = wanted;
            return true;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (KitChrome.NavigateOrRelease(this, @event, SelectByArrow))
                return;

            if (KitChrome.IsConfirm(@event))
            {
                if (Segments.Count > 0 && _current >= 0 && _current < Segments.Count)
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

        /// <summary>
        /// The tip for whichever segment the pointer is over.
        ///
        /// <see cref="SegmentTips"/> has been an exported, authored array since this widget
        /// shipped, and nothing ever read it: the strings were stored per segment and shown
        /// nowhere. Godot asks this method for the text and then
        /// <see cref="KitControl._MakeCustomTooltip"/> draws it in the kit's own chrome.
        /// </summary>
        public override string _GetTooltip(Vector2 atPosition)
        {
            int index = HitSegment(atPosition);
            return index >= 0 && index < Segments.Count ? Segments[index].Tip : "";
        }

        private void ClearHover()
        {
            if (_hover < 0) return;
            _hover = -1;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Size.X < 16f || Size.Y < 8f) return;
            if (Segments.Count == 0)
            {
                KitChrome.DrawEmptyPreview(this, Genre, new Rect2(Vector2.Zero, Size),
                                           ActiveShape, "Segments");
                return;
            }

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            Color acc = UiSurface.Semantic(this, UiSurface.Role.Accent);
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1.5f, g.Rim * 0.7f * (fs / 14f));

            // One plate under the whole strip, so the group reads as a single object.
            DrawShape(new Rect2(Vector2.Zero, Size), ActiveShape,
                      KitChrome.RecessFace(face, g.WellShade) with { A = 1f },
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
                    string glyph = KitCase(Segments[i].Glyph);
                    float textWidth = r.Size.X * 0.64f;
                    int gf = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                               new Vector2(textWidth, r.Size.Y * 0.58f),
                                               glyph, font, min: 8);
                    glyph = KitChrome.EllipsizeText(font, glyph, gf, textWidth);
                    if (string.IsNullOrEmpty(glyph)) continue;
                    Vector2 m = font.GetStringSize(glyph, HorizontalAlignment.Left, -1, gf);
                    DrawText(font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.6f) * 0.5f),
                               glyph, gf, on);
                }
            }

            KitChrome.DrawFocusRing(this, Genre, new Rect2(Vector2.Zero, Size), ActiveShape, 0.8f);
        }
    }
}
