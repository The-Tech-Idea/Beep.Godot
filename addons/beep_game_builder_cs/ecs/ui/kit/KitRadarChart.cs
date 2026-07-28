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

        public override void _Ready()
        {
            base._Ready();
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

        public override void _Draw()
        {
            int n = Mathf.Min(Axes.Count, Values.Count);
            if (n < 3) return;
            float d = Mathf.Min(Size.X, Size.Y);
            if (d < 24f) return;

            var c = Size * 0.5f;
            // Leave room for labels outside the web rather than clipping them.
            float r = d * 0.5f * (ShowLabels ? 0.68f : 0.88f);
            Color fill = UiSurface.Semantic(this, Role);
            Color ink = InkColor();
            Color face = FaceColor();
            var font = GetThemeDefaultFont();
            int fs = UiSurface.FontSize(this, 0.75f);

            Vector2 At(int i, float t)
            {
                float ang = -Mathf.Pi * 0.5f + i * Mathf.Tau / n;
                return c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r * t;
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
                Vector2 m = font.GetStringSize(t, HorizontalAlignment.Left, -1, fs);
                var at = At(i, 1.28f);
                DrawString(font, new Vector2(at.X - m.X * 0.5f, at.Y + m.Y * 0.32f),
                           t, HorizontalAlignment.Left, -1, fs, UiSurface.Text(this));
            }
        }
    }
}
