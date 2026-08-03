using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A radar / spider chart — INDEX.md lists this as "a missing primitive, fully procedural,
    /// useful to racing, rpg and strategy", measured from `racing3.png`.
    ///
    /// It is the one comparison widget in the folder: vehicle stats, class loadouts and faction
    /// traits are all "five numbers you compare at a glance", and a stack of bars answers "how
    /// big is each" while a radar answers "what SHAPE is this thing" — which is the actual
    /// question on a character-select or vehicle-select screen.
    ///
    /// Fully procedural by design: no art, so it reskins with the palette like everything else.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitRadarChart : KitControl
    {
        /// <summary>Axis labels. The chart draws one spoke per entry.</summary>
        public readonly List<string> Axes = new();
        /// <summary>Values 0..1, parallel to <see cref="Axes"/>.</summary>
        public readonly List<float> Values = new();

        [Export] public UiSurface.Role Role { get; set; } = UiSurface.Role.Accent;

        /// <summary>Concentric guide rings. 0 draws none.</summary>
        [Export(PropertyHint.Range, "0,6,1")] public int Rings { get; set; } = 3;

        [Export] public bool ShowLabels { get; set; } = true;

        [Export] public bool Editable { get; set; } = true;

        [Signal] public delegate void ValueChangedEventHandler(int axis, float value);

        private int _activeAxis = -1;

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
            if (Axes.Count == 0)
            {
                Axes.AddRange(new[] { "SPD", "ACC", "GRIP", "BRK", "AIR" });
                Values.AddRange(new[] { 0.82f, 0.55f, 0.7f, 0.45f, 0.62f });
            }
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 8f, fs * 8f);
            }
        }

        public void SetValue(int i, float v)
        {
            if (i < 0 || i >= Values.Count) return;
            Values[i] = Mathf.Clamp(v, 0f, 1f);
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (!Editable) return;
            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                    if (mb.Pressed)
                    {
                        _activeAxis = NearestAxis(mb.Position);
                        ApplyPointerValue(mb.Position);
                    }
                    else
                    {
                        _activeAxis = -1;
                    }
                    AcceptEvent();
                    break;
                case InputEventMouseMotion mm when _activeAxis >= 0:
                    ApplyPointerValue(mm.Position);
                    AcceptEvent();
                    break;
            }
        }

        private int Count() => Mathf.Min(Axes.Count, Values.Count);

        private Vector2 Centre() => Size * 0.5f;

        private float Radius()
        {
            float d = Mathf.Min(Size.X, Size.Y);
            return d * 0.5f * (ShowLabels ? 0.68f : 0.88f);
        }

        private Vector2 AxisDirection(int i, int n)
        {
            float ang = -Mathf.Pi * 0.5f + i * Mathf.Tau / n;
            return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        }

        private int NearestAxis(Vector2 p)
        {
            int n = Count();
            if (n < 3) return -1;
            Vector2 v = p - Centre();
            if (v.LengthSquared() < 1f) return 0;

            int best = 0;
            float bestDot = -999f;
            Vector2 dir = v.Normalized();
            for (int i = 0; i < n; i++)
            {
                float dot = dir.Dot(AxisDirection(i, n));
                if (dot <= bestDot) continue;
                bestDot = dot;
                best = i;
            }
            return best;
        }

        private void ApplyPointerValue(Vector2 p)
        {
            int n = Count();
            if (_activeAxis < 0 || _activeAxis >= n) return;
            Vector2 dir = AxisDirection(_activeAxis, n);
            float value = (p - Centre()).Dot(dir) / Mathf.Max(1f, Radius());
            SetValue(_activeAxis, value);
            EmitSignal(SignalName.ValueChanged, _activeAxis, Values[_activeAxis]);
        }

        public override void _Draw()
        {
            int n = Count();
            if (n < 3) return;
            float d = Mathf.Min(Size.X, Size.Y);
            if (d < 24f) return;

            var c = Centre();
            // Leave room for labels outside the web rather than clipping them.
            float r = Radius();
            Color fill = UiSurface.Semantic(this, Role);
            Color ink = InkColor();
            Color face = FaceColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Small, min: 8);

            Vector2 At(int i, float t)
            {
                return c + AxisDirection(i, n) * r * t;
            }

            // Guide web: rings in the surface's own hue driven dark, never grey.
            Color guide = new(face.R * 0.55f, face.G * 0.55f, face.B * 0.6f, 1f);
            for (int ring = 1; ring <= Rings; ring++)
            {
                float t = ring / (float)Rings;
                for (int i = 0; i < n; i++)
                    DrawLine(At(i, t), At((i + 1) % n, t), guide, Mathf.Max(1f, r * 0.012f));
            }
            for (int i = 0; i < n; i++)
                DrawLine(c, At(i, 1f), guide, Mathf.Max(1f, r * 0.012f));

            // The value polygon.
            var poly = new Vector2[n];
            for (int i = 0; i < n; i++) poly[i] = At(i, Mathf.Clamp(Values[i], 0f, 1f));
            DrawColoredPolygon(poly, new Color(fill.R, fill.G, fill.B, 0.45f));
            var closed = new Vector2[n + 1];
            poly.CopyTo(closed, 0);
            closed[n] = poly[0];
            DrawPolyline(closed, fill, Mathf.Max(2f, r * 0.035f));
            foreach (var p in poly) DrawCircle(p, Mathf.Max(2f, r * 0.045f), fill);

            if (!ShowLabels || font == null) return;
            for (int i = 0; i < n; i++)
            {
                string t = Axes[i] ?? "";
                if (t.Length == 0) continue;
                int tf = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                           new Vector2(d * 0.18f, d * 0.08f), t, font, min: 7);
                Vector2 m = font.GetStringSize(t, HorizontalAlignment.Left, -1, tf);
                var at = At(i, 1.28f);
                var badge = new Rect2(at.X - m.X * 0.5f - tf * 0.35f, at.Y - tf * 0.55f,
                                      m.X + tf * 0.70f, tf * 1.25f);
                DrawShape(badge, KitShape.Pill, new Color(face.R * 0.85f, face.G * 0.85f, face.B * 0.90f, 0.92f),
                          ink with { A = 0.55f }, Mathf.Max(1f, tf * 0.08f));
                DrawText(font, new Vector2(at.X - m.X * 0.5f, at.Y + m.Y * 0.32f),
                           t, tf, UiSurface.Text(this));
            }
        }
    }
}
