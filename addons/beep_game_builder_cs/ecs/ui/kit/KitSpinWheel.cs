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

        [Export] public UiSurface.Role Role { get; set; } = UiSurface.Role.Warning;

        /// <summary>Current rotation in radians. Set directly, or driven by <see cref="Spin"/>.</summary>
        [Export] public float Rotation_ { get => _rot; set { _rot = value; QueueRedraw(); } }
        private float _rot;

        [Signal] public delegate void SpinFinishedEventHandler(int index);

        private bool _spinning;
        private float _t, _dur, _from, _to;
        private int _target;

        public override void _Ready()
        {
            base._Ready();
            if (Wedges.Count == 0)
                Wedges.AddRange(new[] { "50", "10", "x2", "5", "100", "1", "x3", "25" });
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 9f, fs * 9f);
            }
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
            SetProcess(true);
        }

        public override void _Process(double delta)
        {
            if (!_spinning) return;
            _t += (float)delta;
            float k = Mathf.Clamp(_t / _dur, 0f, 1f);
            // Ease out cubic: fast away, long settle, which is what sells the deceleration.
            float e = 1f - Mathf.Pow(1f - k, 3f);
            _rot = Mathf.Lerp(_from, _to, e);
            QueueRedraw();
            if (k < 1f) return;
            _spinning = false;
            SetProcess(false);
            EmitSignal(SignalName.SpinFinished, _target);
        }

        public override void _Draw()
        {
            int n = Wedges.Count;
            float d = Mathf.Min(Size.X, Size.Y);
            if (n < 2 || d < 30f) return;

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
                float mid = a0 + per * 0.5f;
                var at = c + new Vector2(Mathf.Cos(mid), Mathf.Sin(mid)) * r * 0.66f;
                int wf = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                           new Vector2(r * 0.34f, r * 0.16f),
                                           Wedges[i], font, min: 7);
                Vector2 m = font.GetStringSize(Wedges[i], HorizontalAlignment.Left, -1, wf);
                Color badge = UiSurface.Luminance(w) > 0.5f
                    ? new Color(1f, 1f, 1f, 0.22f)
                    : new Color(0f, 0f, 0f, 0.20f);
                DrawShape(new Rect2(at.X - m.X * 0.5f - wf * 0.30f, at.Y - wf * 0.55f,
                                    m.X + wf * 0.60f, wf * 1.20f),
                          KitShape.Pill, badge, new Color(0, 0, 0, 0), 0f);
                DrawText(font, new Vector2(at.X - m.X * 0.5f, at.Y + m.Y * 0.32f),
                           Wedges[i], wf, UiSurface.Luminance(w) > 0.5f
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
        }
    }
}
