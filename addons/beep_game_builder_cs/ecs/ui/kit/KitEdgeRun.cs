using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>What one segment of an edge draws.</summary>
    public enum KitSegFill
    {
        /// <summary>A solid stroke.</summary>
        Solid,
        /// <summary>Nothing — a deliberate break in the frame.</summary>
        Gap,
        /// <summary>A filled block, several times the base weight.</summary>
        Block,
    }

    /// <summary>One run along one edge. Positions are fractions of the edge's length.</summary>
    public sealed class KitEdgeSeg
    {
        public float Start, Length;
        /// <summary>Multiple of the frame's base stroke width.</summary>
        public float Weight = 1f;
        public KitSegFill Fill = KitSegFill.Solid;

        public KitEdgeSeg(float start, float length, float weight = 1f,
                          KitSegFill fill = KitSegFill.Solid)
        { Start = start; Length = length; Weight = weight; Fill = fill; }
    }

    /// <summary>
    /// A frame described as a RUN LIST PER EDGE.
    ///
    /// The sci-fi reference sheets (art pass files 14 and 43) are the reason this exists. Their
        /// frame is not a border with decorated corners: the stroke **changes weight along its
        /// length**, **breaks and restarts**, turns into **solid blocks**, and is
        /// **deliberately asymmetric** — no two corners of the same frame are
    /// treated alike. Eight frames on one sheet, not one of them expressible as a StyleBox, a
    /// silhouette, or a corner-ornament enum.
    ///
    /// A plain rectangle is the degenerate case — one `Solid` segment spanning each edge — so a
    /// theme that declares no run is unaffected.
    /// </summary>
    public sealed class KitEdgeRun
    {
        public KitEdgeSeg[] Top = System.Array.Empty<KitEdgeSeg>();
        public KitEdgeSeg[] Right = System.Array.Empty<KitEdgeSeg>();
        public KitEdgeSeg[] Bottom = System.Array.Empty<KitEdgeSeg>();
        public KitEdgeSeg[] Left = System.Array.Empty<KitEdgeSeg>();

        public int SegmentCount => Top.Length + Right.Length + Bottom.Length + Left.Length;

        /// <summary>Segments that actually mark the edge (a Gap draws nothing).</summary>
        public int DrawnCount
        {
            get
            {
                int n = 0;
                foreach (var e in new[] { Top, Right, Bottom, Left })
                    foreach (var s in e)
                        if (s.Fill != KitSegFill.Gap) n++;
                return n;
            }
        }

        /// <summary>
        /// The sci-fi run, read off files 14 and 43.
        ///
        /// Asymmetry is the point and is built in: the top edge carries a heavy block on its left
        /// third then breaks; the right and bottom edges stay solid; the left is mostly gap with
        /// one short block. Rotating this frame 180 degrees does not give the same frame, which is
        /// exactly what the sheets do.
        /// </summary>
        public static KitEdgeRun SciFi() => new()
        {
            Top = new[]
            {
                new KitEdgeSeg(0.00f, 0.34f, 2.6f, KitSegFill.Block),
                new KitEdgeSeg(0.34f, 0.10f, 1f, KitSegFill.Gap),
                new KitEdgeSeg(0.44f, 0.56f, 1f),
            },
            Right = new[]
            {
                new KitEdgeSeg(0.00f, 0.22f, 1f),
                new KitEdgeSeg(0.22f, 0.30f, 1f),
                new KitEdgeSeg(0.52f, 0.48f, 1f),
            },
            Bottom = new[]
            {
                new KitEdgeSeg(0.00f, 0.58f, 1f),
                new KitEdgeSeg(0.58f, 0.26f, 1f),
                new KitEdgeSeg(0.84f, 0.16f, 2.2f, KitSegFill.Block),
            },
            Left = new[]
            {
                new KitEdgeSeg(0.00f, 0.18f, 2.2f, KitSegFill.Block),
                new KitEdgeSeg(0.18f, 0.52f, 1f, KitSegFill.Gap),
                new KitEdgeSeg(0.70f, 0.30f, 1f),
            },
        };
    }

    /// <summary>Draws a <see cref="KitEdgeRun"/> around a widget.</summary>
    public static class KitEdge
    {
        /// <summary>Global off-switch, for the gate only.
        ///
        /// measure_edgerun.py compares a render WITH the run against one WITHOUT and counts
        /// connected components in the difference. Scanning a fixed row inside the widget cannot
        /// work once shear is involved: a sheared frame is diagonal, so it stops crossing the
        /// scan line and a perfectly good run measures as "not broken" -- which is exactly what
        /// happened the moment the run started following the silhouette.</summary>
        public static bool Enabled = true;

        /// <summary>
        /// Stroke the run around <paramref name="r"/>. Drawn AFTER the plate and the material,
        /// because in the references the frame sits on top of the surface it encloses.
        /// </summary>
        public static void Draw(CanvasItem ci, KitEdgeRun? run, Rect2 r, float baseWidth, Color col,
                                float shear = 0f, float wobble = 0f)
        {
            if (!Enabled) return;
            if (run == null || r.Size.X < 6f || r.Size.Y < 6f) return;
            baseWidth = Mathf.Max(1f, baseWidth);

            // Walk the SILHOUETTE's quad, not the axis-aligned rect.
            //
            // Stroking the rect while the shape is sheared draws the frame somewhere the widget
            // is not: racing (shear 0.16) rendered its declared run entirely off the silhouette
            // and measured as having no frame at all, while shooter (shear 0.09) partly landed.
            // The same Modify() the silhouette uses is applied to the four corners, so the run
            // follows whatever the shape does.
            var q = KitControl.Modify(new[]
            {
                r.Position,
                r.Position + new Vector2(r.Size.X, 0f),
                r.Position + r.Size,
                r.Position + new Vector2(0f, r.Size.Y),
            }, r, shear, wobble);

            var centre = (q[0] + q[1] + q[2] + q[3]) / 4f;
            var lists = new[] { run.Top, run.Right, run.Bottom, run.Left };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = q[i], b = q[(i + 1) % 4];
                float len = a.DistanceTo(b);
                if (len < 2f) continue;
                Vector2 dir = (b - a) / len;
                // Inward is the perpendicular that points at the centroid, so weight always
                // grows into the widget whatever the quad has been skewed to.
                Vector2 n = new(-dir.Y, dir.X);
                if (n.Dot(centre - a) < 0f) n = -n;
                Edge(ci, lists[i], a, dir, n, len, baseWidth, col);
            }
        }

        /// <param name="dir">Along the edge.</param>
        /// <param name="inward">Perpendicular, pointing into the widget — so weight grows inward
        /// and a heavy block never spills outside the control's rect.</param>
        private static void Edge(CanvasItem ci, KitEdgeSeg[] segs, Vector2 origin, Vector2 dir,
                                 Vector2 inward, float len, float baseWidth, Color col)
        {
            foreach (var s in segs)
            {
                if (s.Fill == KitSegFill.Gap) continue;
                float a = Mathf.Clamp(s.Start, 0f, 1f) * len;
                float b = Mathf.Clamp(s.Start + s.Length, 0f, 1f) * len;
                if (b - a < 0.5f) continue;

                Vector2 p0 = origin + dir * a, p1 = origin + dir * b;
                float w = baseWidth * Mathf.Max(0.2f, s.Weight);

                switch (s.Fill)
                {
                    case KitSegFill.Solid:
                        ci.DrawLine(p0 + inward * (w * 0.5f), p1 + inward * (w * 0.5f), col, w);
                        break;

                    case KitSegFill.Block:
                        ci.DrawColoredPolygon(new[]
                        {
                            p0, p1, p1 + inward * w, p0 + inward * w,
                        }, col);
                        break;

                }
            }
        }
    }
}
