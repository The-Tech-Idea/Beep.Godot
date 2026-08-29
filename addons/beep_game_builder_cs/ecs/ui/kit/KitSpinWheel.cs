using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A prize wheel — CATALOGUE-FROM-ART.md F.2's `SpinWheel`, the daily-reward set piece.
    ///
    /// Wedges alternate two tones so adjacent prizes are separable without a border per wedge,
    /// and the pointer sits OUTSIDE the rim at the top: the wheel turns under a fixed marker,
    /// which is what makes the result readable. Turning the marker with the wheel is the classic
    /// mistake — the player then has to track a moving reference.
    ///
    /// <see cref="Spin"/> eases out over its duration and lands the CHOSEN index under the
    /// pointer. The caller decides the prize; a wheel that picks its own would make the odds a
    /// property of the widget, where no designer can reach them.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitSpinWheel : KitControl
    {
        public readonly List<string> Wedges = new();

        [Export]
        public string[] WedgeLabels
        {
            get => Wedges.ToArray();
            set => SetWedges(value);
        }

        public void SetWedges(IEnumerable<string>? wedges)
        {
            List<string> next = NormalizeStrings(wedges);
            if (SameStrings(Wedges, next)) return;
            Wedges.Clear();
            Wedges.AddRange(next);
            RefreshWedges();
        }

        public void AddWedge(string text)
        {
            Wedges.Add(text ?? "");
            RefreshWedges();
        }

        public bool RemoveWedge(int index)
        {
            if (index < 0 || index >= Wedges.Count)
                return false;

            Wedges.RemoveAt(index);
            RefreshWedges();
            return true;
        }

        public void ClearWedges()
        {
            if (Wedges.Count == 0)
                return;

            Wedges.Clear();
            RefreshWedges();
        }

        public void RefreshWedges()
        {
            if (Wedges.Count == 0)
            {
                _target = 0;
                _spinning = false;
                UpdateProcessing();
            }
            else
            {
                _target = Mathf.PosMod(_target, Wedges.Count);
                UpdateProcessing();
            }
            RefreshVisualAndRedraw();
        }

        [Export] public UiSurface.Role Role { get => _role; set { if (_role == value) return; _role = value; RefreshVisualAndRedraw(); } }
        private UiSurface.Role _role = UiSurface.Role.Warning;

        /// <summary>Current rotation in radians. Set directly, or driven by <see cref="Spin"/>.</summary>
        [Export] public float Rotation_ { get => _rot; set { if (Mathf.IsEqualApprox(_rot, value)) return; _rot = value; RefreshVisualAndRedraw(); } }
        private float _rot;

        [Signal] public delegate void SpinFinishedEventHandler(int index);

        private bool _spinning;
        private float _t, _dur, _from, _to;
        private int _target;

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
            UpdateProcessing();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationVisibilityChanged)
                UpdateProcessing();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 9f, fs * 9f);
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private bool ShouldAnimate() => _spinning && Wedges.Count > 0;

        private void UpdateProcessing()
        {
            SetProcess(IsVisibleInTree() && ShouldAnimate());
        }

        private static List<string> NormalizeStrings(IEnumerable<string>? values)
        {
            var next = new List<string>();
            if (values == null)
                return next;

            foreach (string? value in values)
                next.Add(value ?? "");
            return next;
        }

        private static bool SameStrings(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if ((left[i] ?? "") != right[i]) return false;
            return true;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey key && KitChrome.IsConfirmKey(key))
            {
                if (!_spinning && Wedges.Count > 0) Spin(_target + 1);
                AcceptEvent();
                return;
            }

            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                return;
            if (_spinning || Wedges.Count == 0) return;
            GrabFocus();
            Spin(_target + 1);
            AcceptEvent();
        }

        /// <summary>Spin to a caller-chosen wedge. Extra whole turns are cosmetic.</summary>
        public void Spin(int index, float seconds = 2.6f, int turns = 4)
        {
            if (Wedges.Count == 0 || _spinning) return;
            _target = Mathf.PosMod(index, Wedges.Count);
            float per = Mathf.Tau / Wedges.Count;
            // Land the wedge's CENTRE under the pointer at the top.
            float want = -(_target * per + per * 0.5f) - Mathf.Pi * 0.5f;
            _from = _rot;
            _to = want - Mathf.Tau * turns;
            _dur = Mathf.Max(0.2f, seconds);
            _t = 0f;
            _spinning = true;
            UpdateProcessing();
        }

        public override void _Process(double delta)
        {
            if (!ShouldAnimate())
            {
                UpdateProcessing();
                return;
            }
            _t += (float)delta;
            float k = Mathf.Clamp(_t / _dur, 0f, 1f);
            // Ease out cubic: fast away, long settle, which is what sells the deceleration.
            float e = 1f - Mathf.Pow(1f - k, 3f);
            _rot = Mathf.Lerp(_from, _to, e);
            QueueRedraw();
            if (k < 1f) return;
            _spinning = false;
            UpdateProcessing();
            EmitSignal(SignalName.SpinFinished, _target);
        }

        public override void _Draw()
        {
            int n = Wedges.Count;
            float d = Mathf.Min(Size.X, Size.Y);
            if (d < 30f) return;
            if (n < 2)
            {
                KitChrome.DrawEmptyPreview(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size),
                                           KitShape.Ellipse, "Wedges");
                return;
            }

            var c = Size * 0.5f;
            float r = d * 0.5f * 0.86f;
            Color acc = UiSurface.Semantic(this, Role);
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this, 0.85f);
            float per = Mathf.Tau / n;

            for (int i = 0; i < n; i++)
            {
                float a0 = _rot + i * per, a1 = a0 + per;
                // Alternating tones, so neighbours separate without a border each.
                Color w = (i % 2 == 0)
                    ? acc
                    : new Color(Mathf.Lerp(acc.R, face.R, 0.62f), Mathf.Lerp(acc.G, face.G, 0.62f),
                                Mathf.Lerp(acc.B, face.B, 0.62f), 1f);

                var pts = new List<Vector2> { c };
                const int steps = 10;
                for (int s = 0; s <= steps; s++)
                {
                    float a = Mathf.Lerp(a0, a1, s / (float)steps);
                    pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
                DrawColoredPolygon(pts.ToArray(), w);

                if (font == null || string.IsNullOrEmpty(Wedges[i])) continue;
                string wedge = KitCase(Wedges[i]);
                float mid = a0 + per * 0.5f;
                var at = c + new Vector2(Mathf.Cos(mid), Mathf.Sin(mid)) * r * 0.66f;
                float labelWidth = r * 0.34f;
                int wf = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                           new Vector2(labelWidth, r * 0.16f),
                                           wedge, font, min: 7);
                wedge = KitChrome.EllipsizeText(font, wedge, wf, labelWidth);
                if (string.IsNullOrEmpty(wedge)) continue;
                Vector2 m = font.GetStringSize(wedge, HorizontalAlignment.Left, -1, wf);
                Color badge = UiSurface.Luminance(w) > 0.5f
                    ? new Color(1f, 1f, 1f, 0.22f)
                    : new Color(0f, 0f, 0f, 0.20f);
                DrawShape(new Rect2(at.X - m.X * 0.5f - wf * 0.30f, at.Y - wf * 0.55f,
                                    m.X + wf * 0.60f, wf * 1.20f),
                          KitShape.Pill, badge, new Color(0, 0, 0, 0), 0f);
                DrawText(font, new Vector2(at.X - m.X * 0.5f, at.Y + m.Y * 0.32f),
                           wedge, wf, UiSurface.Luminance(w) > 0.5f
                               ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
            }

            DrawArc(c, r, 0f, Mathf.Tau, 64, ink, Mathf.Max(2.5f, d * 0.028f));
            DrawCircle(c, r * 0.14f, face);
            DrawArc(c, r * 0.14f, 0f, Mathf.Tau, 24, ink, Mathf.Max(2f, d * 0.02f));

            // Fixed pointer OUTSIDE the rim at the top — the wheel moves, the marker does not.
            float ph = d * 0.09f;
            var tip = new Vector2(c.X, c.Y - r + ph * 0.35f);
            DrawColoredPolygon(new[]
            {
                new Vector2(c.X - ph * 0.5f, tip.Y - ph),
                new Vector2(c.X + ph * 0.5f, tip.Y - ph),
                tip,
            }, ink);

            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size), KitShape.Ellipse, 0.8f);
        }
    }
}
