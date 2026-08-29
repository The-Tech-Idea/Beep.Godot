using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// How a widget shows that it is selected. A SET, not one value.
    ///
    /// racing3 (art pass file 09) settles it: on ONE screen the icon cells use an accent **fill**
    /// and the carousel cells use an accent **border**. store1 (39) uses a cream pill on its first
    /// tab row and a dark pill on its second. citybuilder5 (06) uses a cyan **glow**. skilltree4
    /// (37) **lifts** the active nav item so it is taller and overhangs the bar. ui1 (17)
    /// **underlines** the active icon tab.
    ///
    /// The art pass counted seventeen distinct mechanisms across the folder, and they **stack** —
    /// so one enum value per widget cannot express it, and one value per genre certainly cannot.
    /// Keyed by <see cref="KitWidgetClass"/>, because the variation is by object, not by theme.
    /// </summary>
    [System.Flags]
    public enum KitSelectCue
    {
        None = 0,
        /// <summary>Swap the plate to the accent.</summary>
        Fill = 1 << 0,
        /// <summary>A ring in the accent, outside the widget so it reads as a frame around it
        /// rather than a change to it.</summary>
        Border = 1 << 1,
        /// <summary>A soft accent halo.</summary>
        Glow = 1 << 2,
        /// <summary>Raise the widget — the active nav item in 37 is taller and overhangs.</summary>
        Lift = 1 << 3,
        /// <summary>A bar under the widget: the tab-strip cue in 17.</summary>
        Underline = 1 << 4,
    }

    /// <summary>Draws the selection cues a theme declares for a widget class.</summary>
    public static class KitSelect
    {
        /// <summary>
        /// Draw every declared cue except <see cref="KitSelectCue.Fill"/>, which the caller must
        /// apply to its own plate colour before drawing (it is a fill, not an overlay).
        ///
        /// Called AFTER the widget's own layers, so Border and Glow sit outside it — a selection
        /// ring drawn under the plate is invisible, which is the obvious way to get this wrong.
        /// </summary>
        public static void Draw(CanvasItem ci, KitSelectCue cues, Vector2[] poly, Rect2 body,
                                Color accent, float unit)
        {
            if (cues == KitSelectCue.None || poly == null || poly.Length < 3) return;
            unit = Mathf.Max(1f, unit);
            var centre = body.Position + body.Size * 0.5f;

            if (cues.HasFlag(KitSelectCue.Glow))
                for (int i = 5; i >= 1; i--)
                {
                    float t = i / 5f;
                    var p = Expand(poly, centre, unit * 1.6f * t);
                    ci.DrawColoredPolygon(p, accent with { A = 0.34f * (1f - t) * (1f - t) + 0.05f });
                }

            if (cues.HasFlag(KitSelectCue.Border))
            {
                var p = Expand(poly, centre, unit * 0.9f);
                var closed = new Vector2[p.Length + 1];
                p.CopyTo(closed, 0);
                closed[^1] = p[0];
                ci.DrawPolyline(closed, accent, Mathf.Max(2f, unit * 0.7f));
            }

            if (cues.HasFlag(KitSelectCue.Underline))
            {
                float w = Mathf.Max(2f, unit * 0.8f);
                ci.DrawLine(new Vector2(body.Position.X, body.End.Y - w * 0.5f),
                            new Vector2(body.End.X, body.End.Y - w * 0.5f), accent, w);
            }
        }

        /// <summary>Vertical offset a Lift cue applies. Returned rather than drawn, because the
        /// widget has to lay itself out at the raised position — an overlay cannot move it.</summary>
        public static float LiftOffset(KitSelectCue cues, float unit)
            => cues.HasFlag(KitSelectCue.Lift) ? -Mathf.Max(2f, unit * 1.2f) : 0f;

        private static Vector2[] Expand(Vector2[] p, Vector2 c, float by)
        {
            var o = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                Vector2 v = p[i] - c;
                float len = v.Length();
                o[i] = len < 0.01f ? p[i] : c + v * ((len + by) / len);
            }
            return o;
        }
    }
}
