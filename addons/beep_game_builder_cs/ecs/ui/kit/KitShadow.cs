using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>How a widget is separated from what is behind it.</summary>
    public enum KitShadowKind
    {
        /// <summary>Nothing. Separation comes from the outline or from value contrast alone —
        /// a deliberate choice in seven of the reference files, not an omission.</summary>
        None,
        /// <summary>Opaque, offset, no blur. The cartoon/carved families.</summary>
        Hard,
        /// <summary>Large radius, low alpha, ambient. The papery and ornate families.</summary>
        Soft,
        /// <summary>Coloured outer glow. Also serves as a selection cue.</summary>
        Glow,
        /// <summary>A solid dark SIDE FACE directly under the widget, so it reads as a slab seen
        /// slightly from above. Not an offset copy — there is no gap and no blur.</summary>
        Extrude,
    }

    /// <summary>
    /// A theme's shadow, as data.
    ///
    /// The art pass (`plans/game-ui-kit/ART_PASS_PER_FILE.md`) found five distinct behaviours
    /// across the 59 reference images, and the kit had **no shadow layer at all** — every widget
    /// in every genre drew flat onto whatever was behind it. Two themes of the same genre are
    /// told apart by this as much as by silhouette.
    /// </summary>
    public sealed class KitShadowDef
    {
        public KitShadowKind Kind = KitShadowKind.None;
        /// <summary>Offset in multiples of the widget's own frame thickness, so a shadow scales
        /// with the widget rather than being a fixed pixel count.</summary>
        public float OffsetX, OffsetY;
        /// <summary>How far the shadow spreads outward, in the same units.</summary>
        public float Spread;
        /// <summary>Number of concentric passes. 1 = a crisp edge; more = a soft falloff.</summary>
        public int Steps = 1;
        public float Alpha = 0.45f;

        public static readonly KitShadowDef None = new() { Kind = KitShadowKind.None };

        public static KitShadowDef Hard(float dx = 0.9f, float dy = 0.9f, float a = 0.55f)
            => new() { Kind = KitShadowKind.Hard, OffsetX = dx, OffsetY = dy, Alpha = a, Steps = 1 };

        public static KitShadowDef Soft(float spread = 1.5f, float a = 0.34f, int steps = 7)
            => new() { Kind = KitShadowKind.Soft, OffsetY = 0.35f, Spread = spread,
                       Alpha = a, Steps = steps };

        public static KitShadowDef Glow(float spread = 1.4f, float a = 0.55f, int steps = 6)
            => new() { Kind = KitShadowKind.Glow, Spread = spread, Alpha = a, Steps = steps };

        public static KitShadowDef Extrude(float depth = 1.6f, float a = 0.80f)
            => new() { Kind = KitShadowKind.Extrude, OffsetY = depth, Alpha = a, Steps = 1 };
    }

    /// <summary>Draws a <see cref="KitShadowDef"/> under a widget's silhouette.</summary>
    public static class KitShadow
    {
        /// <summary>Global off-switch, for the gate only.
        ///
        /// measure_shadow.py compares a render WITH the shadow against one WITHOUT and analyses
        /// the difference. That is the only way to measure this honestly: a silhouette that
        /// overhangs its rect (Capsule, Spiked, Torn) adds dark pixels outside it that are not
        /// shadow, and one that sits inset within its rect (Shield) casts a shadow that never
        /// leaves it. Both defeat any "look outside the rect" test. Differencing cancels the
        /// silhouette exactly, whatever it does.</summary>
        public static bool Enabled = true;

        /// <summary>
        /// The scale a shadow's offsets and spread are measured in.
        ///
        /// Derived from the WIDGET's short edge, not from its frame thickness. Sizing it off
        /// FramePx looked reasonable and was wrong: the Casual genres declare
        /// <c>KitFrameMode.None</c>, so their frame is ~0 and their shadows collapsed to about a
        /// pixel — cardgame and platformer rendered a shadow the gate could not even see. A
        /// shadow scales with the thing casting it, which is the widget.
        /// </summary>
        public static float UnitFor(Rect2 body)
            => Mathf.Max(3f, Mathf.Min(body.Size.X, body.Size.Y) * 0.055f);

        /// <summary>
        /// Paint the shadow for <paramref name="poly"/>. Call FIRST — before the plate, the
        /// grain and the rim — because a shadow is behind everything by definition.
        /// </summary>
        /// <param name="unit">The widget's frame thickness. Offsets and spread are multiples of
        /// it, so a shadow scales with the widget instead of being a fixed pixel count that
        /// looks heavy on a chip and invisible on a panel.</param>
        public static void Draw(CanvasItem ci, KitShadowDef def, Vector2[] poly, Rect2 body,
                                float unit, Color surface, Color? glowColor = null)
        {
            if (!Enabled) return;
            if (def == null || def.Kind == KitShadowKind.None) return;
            if (poly == null || poly.Length < 3) return;
            if (body.Size.X < 3f || body.Size.Y < 3f) return;

            unit = Mathf.Max(1f, unit);
            var centre = body.Position + body.Size * 0.5f;

            // A shadow is a dark tint of the SURFACE's own hue, never a neutral grey — the same
            // rule the slider track and toggle already follow, so a warm parchment theme casts a
            // warm shadow and a cool metal one casts a cool shadow.
            Color ink = new(surface.R * 0.18f, surface.G * 0.17f, surface.B * 0.22f, 1f);
            Color tone = def.Kind == KitShadowKind.Glow
                ? (glowColor ?? new Color(1f, 0.92f, 0.55f))
                : ink;

            // The widget's own silhouette is SUBTRACTED from every shadow pass below. Without
            // it the shadow is drawn under the plate and shows through wherever the plate is not
            // fully opaque -- five shipped themes declare a 95%-opaque panel, and the difference
            // render showed a large faint population (9068 px at depth 5-18 for citybuilder)
            // sitting under the plate alongside the real shadow. A widget must not show its own
            // shadow through itself.
            Vector2[] cutout = poly;

            switch (def.Kind)
            {
                case KitShadowKind.Hard:
                    Fill(ci, Offset(poly, new Vector2(def.OffsetX, def.OffsetY) * unit),
                         tone with { A = def.Alpha }, cutout);
                    break;

                case KitShadowKind.Extrude:
                    // A SIDE FACE, not an offset copy: no gap, no blur, straight down. Drawing
                    // it as a copy would leave a sliver of background between slab and face and
                    // destroy the "solid block" reading the reference gets from it.
                    Fill(ci, Offset(poly, new Vector2(0f, def.OffsetY * unit)),
                         tone with { A = def.Alpha }, cutout);
                    break;

                case KitShadowKind.Soft:
                case KitShadowKind.Glow:
                {
                    // Concentric expanded copies with falling alpha. Godot's CanvasItem has no
                    // blur, and a real blur would need a shader per widget; N passes reproduce
                    // the falloff the metric measures without one.
                    int n = Mathf.Max(1, def.Steps);
                    for (int i = n; i >= 1; i--)
                    {
                        float t = i / (float)n;
                        float grow = def.Spread * unit * t;
                        var off = new Vector2(def.OffsetX, def.OffsetY) * unit;
                        // Alpha falls off with distance; squared so the outer passes are faint
                        // rather than a set of visible concentric bands.
                        // The floor matters: at 0.08 the outermost pass darkened the ground by
                        // about 7/255 and a genuine soft shadow measured as NONE. 0.22 keeps the
                        // falloff readable without turning the outer passes into visible bands.
                        float a = def.Alpha * ((1f - t) * (1f - t) * 0.9f + 0.22f);
                        Fill(ci, Offset(Expand(poly, centre, grow), off), tone with { A = a }, cutout);
                    }
                    break;
                }
            }
        }

        /// <summary>Draw one shadow pass with the widget's own silhouette cut out of it.</summary>
        private static void Fill(CanvasItem ci, Vector2[] p, Color c, Vector2[]? cutout = null)
        {
            if (c.A <= 0.004f) return;

            // Subtract the widget. ClipPolygons returns the pieces of `p` outside `cutout`,
            // which is exactly the visible part of a shadow; it can return several pieces (a
            // ring becomes two) and can return none at all when the shadow is entirely covered.
            if (cutout is { Length: >= 3 })
            {
                var pieces = Geometry2D.ClipPolygons(p, cutout);
                if (pieces.Count > 0)
                {
                    foreach (var piece in pieces) Emit(ci, piece, c);
                    return;
                }
                // No pieces means fully covered -- nothing to draw. Falling through to draw the
                // uncut polygon here would silently reintroduce the bleed-through.
                return;
            }
            Emit(ci, p, c);
        }

        private static void Emit(CanvasItem ci, Vector2[] p, Color c)
        {
            // A silhouette that cannot be triangulated draws nothing; skipping quietly is fine
            // here because the plate above will still render — the widget loses its shadow, not
            // its body, and KitGrain already warns about the same silhouettes.
            if (p.Length < 3 || Geometry2D.TriangulatePolygon(p).Length == 0) return;
            ci.DrawColoredPolygon(p, c);
        }

        private static Vector2[] Offset(Vector2[] p, Vector2 d)
        {
            var o = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++) o[i] = p[i] + d;
            return o;
        }

        /// <summary>Scale the polygon about the widget's centre. Cheap, and correct for the
        /// convex-ish silhouettes the kit draws; a true outward offset would need mitre joins
        /// and would fail on the deliberately non-convex ones (Spiked, Torn).</summary>
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
