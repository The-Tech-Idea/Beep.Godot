using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// `&lt; Option &gt;` — the game form's replacement for a dropdown.
    ///
    /// CATALOGUE-FROM-ART.md section D lists `ArrowSelector` from `settings1.png`, and records a
    /// correction worth keeping: <b>dropdowns appear in NONE of the 43 reference images</b>. Game
    /// UIs page through options with arrows instead, because a dropdown needs a popup layer, a
    /// pointer, and a list that does not fit a controller. This is the widget a settings screen
    /// actually wants for resolution, language and difficulty.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitArrowSelector : KitControl
    {
        public readonly List<string> Options = new();

        [Export]
        public string[] OptionLabels
        {
            get => Options.ToArray();
            set => SetOptions(value, _current);
        }

        public void SetOptions(IEnumerable<string>? options, int current = 0)
        {
            string[] next = NormalizeStrings(options);
            int normalizedCurrent = next.Length == 0
                ? 0
                : Clamp
                    ? Mathf.Clamp(current, 0, next.Length - 1)
                    : Mathf.PosMod(current, next.Length);
            if (SameStrings(Options, next) && _current == normalizedCurrent) return;

            Options.Clear();
            Options.AddRange(next);
            _current = normalizedCurrent;
            RefreshOptions();
        }

        public void AddOption(string option)
        {
            Options.Add(option ?? "");
            RefreshOptions();
        }

        public bool RemoveOption(int index)
        {
            if (index < 0 || index >= Options.Count)
                return false;

            Options.RemoveAt(index);
            if (index <= _current)
                _current = Mathf.Max(0, _current - 1);
            RefreshOptions();
            return true;
        }

        public void ClearOptions()
        {
            if (Options.Count == 0 && _current == 0)
                return;

            Options.Clear();
            _current = 0;
            RefreshOptions();
        }

        public void RefreshOptions()
        {
            if (Options.Count == 0)
                _current = 0;
            else if (Clamp)
                _current = Mathf.Clamp(_current, 0, Options.Count - 1);
            else
                _current = Mathf.PosMod(_current, Options.Count);
            _hoverSide = 0;
            RefreshMinimumAndRedraw();
        }

        [Export] public int Current
        {
            get => _current;
            set
            {
                if (Options.Count == 0) { _current = 0; return; }
                int v = Clamp
                    ? Mathf.Clamp(value, 0, Options.Count - 1)
                    : Mathf.PosMod(value, Options.Count);
                if (v == _current) return;
                _current = v;
                RefreshVisualAndRedraw();
                if (IsInsideTree())
                    EmitSignal(SignalName.OptionChanged, v);
            }
        }
        private int _current;

        /// <summary>Stop at the ends instead of cycling. Off by default: the references page
        /// round, which avoids a dead-looking arrow at either end.</summary>
        [Export] public bool Clamp
        {
            get => _clamp;
            set
            {
                if (_clamp == value) return;
                _clamp = value;
                if (_clamp && Options.Count > 0)
                    Current = Mathf.Clamp(_current, 0, Options.Count - 1);
                RefreshVisualAndRedraw();
            }
        }
        private bool _clamp;

        [Signal] public delegate void OptionChangedEventHandler(int index);
        private int _hoverSide;
        private bool _eventsHooked;

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

        private float ArrowW => Mathf.Max(14f, Size.Y * 0.8f);

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 9f, fs * 2.1f);
        }

        private void RefreshMinimumAndRedraw()
        {
            if (IsInsideTree())
            {
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
                UpdateMinimumSize();
            }
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey key)
            {
                Vector2I dir = KitChrome.DirectionFromKey(key);
                if (dir.X < 0 && CanStep(-1)) { Step(-1); AcceptEvent(); }
                else if (dir.X > 0 && CanStep(1)) { Step(1); AcceptEvent(); }
                return;
            }

            if (@event is InputEventMouseMotion mm)
            {
                int side = mm.Position.X < ArrowW ? -1 : mm.Position.X > Size.X - ArrowW ? 1 : 0;
                if (side != _hoverSide)
                {
                    _hoverSide = side;
                    QueueRedraw();
                }
                return;
            }

            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
                return;
            if (mb.Position.X < ArrowW) Step(-1);
            else if (mb.Position.X > Size.X - ArrowW) Step(1);
            else return;
            GrabFocus();
            AcceptEvent();
        }

        private void Step(int d)
        {
            if (Options.Count == 0) return;
            int next = _current + d;
            if (Clamp) next = Mathf.Clamp(next, 0, Options.Count - 1);
            Current = next;
        }

        private bool CanStep(int d)
            => !Clamp || (_current + d >= 0 && _current + d < Options.Count);

        private void ClearHover()
        {
            if (_hoverSide == 0) return;
            _hoverSide = 0;
            QueueRedraw();
        }

        private static string[] NormalizeStrings(IEnumerable<string>? values)
        {
            if (values == null)
                return System.Array.Empty<string>();

            var next = new List<string>();
            foreach (string value in values)
                next.Add(value ?? "");
            return next.Count == 0 ? System.Array.Empty<string>() : next.ToArray();
        }

        private static bool SameStrings(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if ((a[i] ?? "") != (b[i] ?? ""))
                    return false;
            return true;
        }

        public override void _Draw()
        {
            if (Size.X < 12f || Size.Y < 6f) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * 0.7f * (fs / 14f));

            var r = new Rect2(Vector2.Zero, Size);
            float ps = g.WellShade;
            DrawShape(r, ActiveShape, new Color(face.R * ps, face.G * ps, face.B * ps, 1f), ink, rimPx);

            float aw = ArrowW;
            DrawLine(new Vector2(aw, Size.Y * 0.18f), new Vector2(aw, Size.Y * 0.82f),
                     ink with { A = 0.45f }, Mathf.Max(1f, rimPx * 0.5f));
            DrawLine(new Vector2(Size.X - aw, Size.Y * 0.18f), new Vector2(Size.X - aw, Size.Y * 0.82f),
                     ink with { A = 0.45f }, Mathf.Max(1f, rimPx * 0.5f));
            DrawArrow(new Rect2(0f, 0f, aw, Size.Y), -1, ink, CanStep(-1), _hoverSide == -1);
            DrawArrow(new Rect2(Size.X - aw, 0f, aw, Size.Y), 1, ink, CanStep(1), _hoverSide == 1);
            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), r, ActiveShape);

            if (Options.Count == 0)
            {
                KitChrome.DrawEmptyPreview(this, KitChrome.GenreOf(this),
                                           new Rect2(aw, 0f, Mathf.Max(1f, Size.X - aw * 2f), Size.Y),
                                           KitShape.Pill, "Options");
                return;
            }
            if (font == null) return;
            string txt = KitCase(Options[Mathf.Clamp(_current, 0, Options.Count - 1)]);
            float textWidth = Mathf.Max(1f, Size.X - aw * 2.25f);
            int tf = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                       new Vector2(textWidth, Size.Y * 0.72f),
                                       txt, font, min: 8);
            txt = KitChrome.EllipsizeText(font, txt, tf, textWidth);
            if (string.IsNullOrEmpty(txt)) return;
            Vector2 m = font.GetStringSize(txt, HorizontalAlignment.Left, -1, tf);
            DrawText(font, new Vector2((Size.X - m.X) * 0.5f, (Size.Y + m.Y * 0.6f) * 0.5f),
                       txt, tf, UiSurface.Text(this));
        }

        /// <summary>An arrow that cannot be taken drains saturation rather than disappearing —
        /// a missing control is harder to read than a muted one.</summary>
        private void DrawArrow(Rect2 box, int dir, Color ink, bool enabled, bool hover)
        {
            var c = box.Position + box.Size * 0.5f;
            float a = Mathf.Min(box.Size.X, box.Size.Y) * 0.22f;
            float w = Mathf.Max(2f, a * 0.5f);
            Color col = UiSurface.Text(this);
            if (!enabled) col = col with { A = 0.28f };
            if (enabled)
            {
                if (hover)
                {
                    Color h = UiSurface.Semantic(this, UiSurface.Role.Info);
                    DrawRect(box.Grow(-box.Size.Y * 0.12f), new Color(h.R, h.G, h.B, 0.20f), false,
                             Mathf.Max(1f, w * 0.35f));
                }
            }
            var tip = c + new Vector2(a * dir, 0f);
            DrawLine(c + new Vector2(-a * dir, -a), tip, col, w);
            DrawLine(c + new Vector2(-a * dir, a), tip, col, w);
        }
    }
}
