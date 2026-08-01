using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Base for every Game UI Kit widget: a stack of drawn layers cut to the genre's silhouette,
    /// plus attachments that may overhang.
    ///
    /// Why this exists rather than more theming: a Godot StyleBox is ONE rectangle with a border.
    /// Every reference kit builds a control from several layers, cuts it to a non-rectangular
    /// outline, and hangs sub-elements across its edges. See plans/game-ui-kit/PLAN.md §1.
    ///
    /// Skinning is unchanged and consumed, not replaced:
    ///   genre  -> silhouette + material   (KitMaterial)
    ///   theme  -> colour identity         (UiSurface roles)
    ///   palette-> tint                    (already applied inside the theme)
    /// </summary>
    [Tool]
    public abstract partial class KitControl : Godot.Control
    {
        /// <summary>Override the genre's silhouette. Leave as null to inherit — which is the
        /// normal case, and the reason a widget does not restate its own shape.</summary>
        [Export] public bool OverrideShape { get; set; }
        [Export] public KitShape Shape { get; set; } = KitShape.Round;

        [Export] public KitElevation Elevation { get; set; } = KitElevation.Raised;

        /// <summary>Corner fraction. Negative inherits the GENRE's value; set only to deviate.</summary>
        [Export(PropertyHint.Range, "-1.0,0.5,0.01")] public float CornerOverride { get; set; } = -1f;

        /// <summary>The genre's proportions. PLAN.md rule 7: no metric constants on this class.</summary>
        protected KitGeometry Geo => KitGeometry.ForGenre(_genre);
        /// <summary>What kind of object this widget is. Drives the properties the art varies by
        /// object rather than by genre — corner radius today. Widgets override; the default is
        /// Button because that is what most of the kit is.</summary>
        protected virtual KitWidgetClass WidgetClass => KitWidgetClass.Button;

        protected float CornerFraction =>
            CornerOverride >= 0f ? CornerOverride : Geo.CornerFor(WidgetClass);

        protected KitState State = KitState.Normal;
        protected readonly List<KitAttach> Attachments = new();

        private string _genre = "";

        /// <summary>TEMPORARY diagnostic switch. Reading the code failed twice on the outline
        /// inversion; this prints the value actually used.</summary>
        public static bool DebugOutline;

        public override void _Ready()
        {
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";

            // MouseFilter is deliberately NOT set here. Control already defaults to Stop, so the
            // interactive widgets get what they need for _GuiInput without help — while forcing
            // it in _Ready silently overrode any scene that had chosen otherwise. A HUD readout
            // is the case that matters: hud.tscn sets mouse_filter = 2 (Ignore) on its stat
            // widgets precisely so the HUD does not eat gameplay clicks, and this line was
            // undoing that on every one of them after the scene had loaded.
        }

        public override void _Notification(int what)
        {
            if (what == NotificationThemeChanged)
            {
                _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
                QueueRedraw();
            }
        }

        /// <summary>
        /// The font this widget should draw in: the GENRE's family, falling back to the theme
        /// default.
        ///
        /// Every kit widget used to call <c>GetThemeDefaultFont()</c> directly, so the font role
        /// added in Phase C reached only the derive-from-Godot drop-ins and every KitControl
        /// widget kept drawing in the theme's default sans — the proof render showed four genres
        /// with identical type and <c>KitFonts</c> never even warned. One resolver, swept over
        /// every call site, so a new widget cannot miss it.
        /// </summary>
        protected Font? KitFont() => KitFonts.Resolve(Geo.Font) ?? GetThemeDefaultFont();

        /// <summary>Apply the genre's case rule to a string before drawing it.</summary>
        protected string KitCase(string t) => Geo.UpperCase ? t.ToUpperInvariant() : t;

        protected KitMaterial Material => KitMaterial.ForGenre(_genre);
        protected KitShape ActiveShape => OverrideShape ? Shape : KitMaterial.ShapeForGenre(_genre);

        /// <summary>Surface for this widget's elevation and state. Every colour in the kit
        /// resolves here — no literals, per PLAN.md §4 rule 1.</summary>
        protected Color FaceColor()
        {
            Color s = UiSurface.Of(this);
            float k = Elevation switch
            {
                KitElevation.Recessed => 0.72f,
                KitElevation.Flush => 0.92f,
                _ => 1f,
            };
            k *= State switch
            {
                KitState.Hover => 1.12f,
                KitState.Pressed => 0.84f,
                KitState.Disabled or KitState.Locked => 0.88f,
                _ => 1f,
            };
            return new Color(Mathf.Min(1f, s.R * k), Mathf.Min(1f, s.G * k), Mathf.Min(1f, s.B * k), s.A);
        }

        protected Color InkColor() => UiSurface.Ink(UiSurface.Of(this));

        /// <summary>
        /// The outer rim, whose POLARITY is a genre tell rather than a constant.
        ///
        /// The kit used to draw <see cref="InkColor"/> on every widget in every genre, which the
        /// greyscale gate measured at rim:body = 0.16 for all ten — an identical dark line
        /// carrying no genre identity. The references split: carved families put a BRIGHT rim at
        /// 1.78-2.05x the plate, casual/mobile ones a thick DARK outline. Driven by
        /// <see cref="KitGeometry.RimBrightness"/>.
        /// </summary>
        protected Color RimColor()
        {
            Color face = FaceColor();
            float b = Geo.RimBrightness;

            // RimBrightness is a multiple of the PLATE's luminance, so it means the same thing
            // the reference measurements mean and can be checked against them directly. Lerping
            // toward white by "how far past 1.0" instead overshot 2.05x to a measured 6.1x.
            float plateLum = UiSurface.Luminance(face) * Geo.PlateShadeFor(Elevation);
            float target = Mathf.Clamp(plateLum * b, 0f, 1f);
            float faceLum = Mathf.Max(0.001f, UiSurface.Luminance(face));

            if (target <= faceLum)
            {
                float k = target / faceLum;
                return new Color(face.R * k, face.G * k, face.B * k, 1f);
            }
            // Brighter than the face itself: lift toward white by exactly enough to hit the
            // target luminance, keeping the hue.
            float t = Mathf.Clamp((target - faceLum) / Mathf.Max(0.001f, 1f - faceLum), 0f, 1f);
            return new Color(Mathf.Lerp(face.R, 1f, t),
                             Mathf.Lerp(face.G, 1f, t),
                             Mathf.Lerp(face.B, 1f, t), 1f);
        }

        /// <summary>True when the sculpt reads as pushed in — pressed, or a recessed well.
        /// Inverts the bevel so the same material makes both a raised plate and a groove.</summary>
        protected bool Sunken => State == KitState.Pressed || Elevation == KitElevation.Recessed;

        // ── Silhouette ────────────────────────────────────────────────────────────────────

        /// <summary>Outline of a shape inside a rect. Straight-edged shapes return a polygon;
        /// rounded ones return null and are drawn as a rounded rect instead.</summary>
        /// <summary>internal, not protected: <see cref="KitPanelContainer"/> derives from Godot's
        /// PanelContainer (to inherit layout) and so cannot inherit this class, but must cut to
        /// exactly the same silhouettes. Sharing the geometry is the point — two copies of the
        /// outline table would drift.</summary>
        /// <summary>The silhouette as a polygon, ALWAYS — unlike <see cref="Outline"/>, which
        /// returns null for the shapes expressed as a corner radius and leaves the caller to
        /// draw a StyleBox.
        ///
        /// Needed by any layer that must be CLIPPED to the widget's shape rather than to its
        /// bounding box. The grain is the first: filling a pill's bounding rect with wood would
        /// paint the material past both round ends and square off the silhouette the outline
        /// work exists to create. Shared with <see cref="KitPushButton"/> so the two renderers
        /// clip to the same shape instead of drifting.</summary>
        /// <summary>
        /// Apply the theme's SHEAR and WOBBLE to a finished silhouette.
        ///
        /// Both are post-passes on the polygon rather than new shapes, so every silhouette gets
        /// them for free and they compose: a sheared octagon and a wobbly pill both work. Applied
        /// here, at the one place every polygon is produced, so no widget can miss them.
        /// </summary>
        internal static Vector2[] Modify(Vector2[] poly, Rect2 r, float shear, float wobble)
        {
            if (poly == null || poly.Length < 3) return poly;
            if (shear <= 0.0001f && wobble <= 0.0001f) return poly;

            float h = Mathf.Max(1f, r.Size.Y);
            float amp = wobble * Mathf.Min(r.Size.X, r.Size.Y);
            // Seeded from the rect's own size, exactly as Torn is: a wobble that reshuffles on
            // every redraw reads as noise, not as a hand-drawn line.
            uint seed = (uint)(Mathf.RoundToInt(r.Size.X) * 73856093 ^
                               Mathf.RoundToInt(r.Size.Y) * 19349663);
            float Next()
            {
                seed = seed * 1664525u + 1013904223u;
                return ((seed >> 16) & 0xFF) / 255f - 0.5f;
            }

            // WOBBLE AS A LOW-FREQUENCY FUNCTION OF ARC LENGTH, not per-vertex noise.
            //
            // It used to displace every vertex by an independent random amount. With six points
            // per outline that reads as a hand-drawn waver; the moment the arc sampling became
            // adaptive (up to 174 points on a large plate) the identical code produced WHITE
            // NOISE -- a ragged fringe all the way around a shape whose outline is smooth. The
            // silhouette must not change character with how finely it happens to be sampled.
            //
            // Three seeded harmonics of the normalised arc length, displaced along the outward
            // normal: neighbouring vertices now move together, the waver has a fixed number of
            // lobes whatever the resolution, and it cannot self-intersect the way independent
            // jitter could.
            float total = 0f;
            var cum = new float[poly.Length + 1];
            for (int i = 0; i < poly.Length; i++)
            {
                cum[i] = total;
                total += poly[i].DistanceTo(poly[(i + 1) % poly.Length]);
            }
            cum[poly.Length] = total;
            if (total < 1f) total = 1f;

            float p1 = Next() * Mathf.Tau, p2 = Next() * Mathf.Tau, p3 = Next() * Mathf.Tau;
            Vector2 centre = r.Position + r.Size * 0.5f;

            var o = new Vector2[poly.Length];
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 v = poly[i];
                if (shear > 0.0001f)
                {
                    // Skew about the vertical centre so the widget stays put rather than
                    // drifting sideways as it shears.
                    float t = ((v.Y - r.Position.Y) / h) - 0.5f;
                    v.X -= t * shear * h;
                }
                if (amp > 0.0001f)
                {
                    float u = cum[i] / total * Mathf.Tau;
                    float d = Mathf.Sin(u * 3f + p1) * 0.55f
                            + Mathf.Sin(u * 5f + p2) * 0.30f
                            + Mathf.Sin(u * 8f + p3) * 0.15f;
                    Vector2 n = (v - centre);
                    if (n.LengthSquared() > 0.01f) v += n.Normalized() * d * amp;
                }
                o[i] = v;
            }
            return o;
        }

        internal static Vector2[] OutlinePoly(KitShape shape, Rect2 r, float cut,
                                              float shear = 0f, float wobble = 0f, float unit = 0f)
        {
            if (Outline(shape, r, cut, unit) is { } poly) return Modify(poly, r, shear, wobble);
            float rad = shape switch
            {
                KitShape.Rect => 0f,
                KitShape.Pill or KitShape.Ellipse => Mathf.Min(r.Size.X, r.Size.Y) * 0.5f,
                _ => cut,
            };
            rad = Mathf.Min(rad, Mathf.Min(r.Size.X, r.Size.Y) * 0.5f);
            if (rad <= 0.5f)
                return Modify(new[]
                {
                    r.Position, r.Position + new Vector2(r.Size.X, 0),
                    r.Position + r.Size, r.Position + new Vector2(0, r.Size.Y),
                }, r, shear, wobble);
            // ADAPTIVE resolution. This was a flat 6 segments per corner, which was invisible
            // while the polygon only fed grain clipping and the shadow -- the visible plate went
            // through DrawStyleBox, which draws a true arc. Now that every layer IS this polygon,
            // 6 segments on a 130px radius is a 34px facet and a stadium renders as a coarse
            // octagon. About one segment every 3px, clamped so a tiny chip stays cheap and a
            // large panel stays smooth.
            int seg = Mathf.Clamp(Mathf.RoundToInt(rad / 3f), 4, 48);
            var pts = new System.Collections.Generic.List<Vector2>(seg * 4 + 4);
            Vector2[] centres =
            {
                r.Position + new Vector2(r.Size.X - rad, rad),
                r.Position + new Vector2(r.Size.X - rad, r.Size.Y - rad),
                r.Position + new Vector2(rad, r.Size.Y - rad),
                r.Position + new Vector2(rad, rad),
            };
            for (int ci = 0; ci < 4; ci++)
            {
                float start = -Mathf.Pi * 0.5f + ci * Mathf.Pi * 0.5f;
                for (int i = 0; i <= seg; i++)
                {
                    float t = start + Mathf.Pi * 0.5f * i / seg;
                    var v = centres[ci] + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * rad;
                    // DEDUPE. At the stadium limit (rad == min(w,h)/2) the two corner centres on
                    // the short axis COINCIDE, so consecutive arc points land on top of each
                    // other and Godot's triangulator rejects the whole polygon — which is why
                    // Pill and Ellipse failed at every single size, silently drawing nothing.
                    if (pts.Count > 0 && pts[^1].DistanceSquaredTo(v) < 0.02f) continue;
                    pts.Add(v);
                }
            }
            if (pts.Count > 1 && pts[0].DistanceSquaredTo(pts[^1]) < 0.02f)
                pts.RemoveAt(pts.Count - 1);
            return Modify(pts.ToArray(), r, shear, wobble);
        }

        /// <param name="unit">The theme's base metric, which sizes the STRUCTURAL features of the
        /// big silhouettes (spike depth, tear amplitude). Pass 0 and it falls back to a fraction of
        /// the rect, which is what every caller did implicitly before and which is why those
        /// features scaled with the widget instead of with the theme.</param>
        internal static Vector2[]? Outline(KitShape shape, Rect2 r, float cut, float unit = 0f)
        {
            if (unit <= 0f) unit = Mathf.Min(r.Size.X, r.Size.Y) * 0.28f;
            float x = r.Position.X, y = r.Position.Y, w = r.Size.X, h = r.Size.Y;
            // 0.45, not 0.5: at exactly half, a chamfer's (x+c, y) and (x+w-c, y) COINCIDE and
            // the polygon degenerates, which Godot reports as "Invalid polygon data,
            // triangulation failed" and then draws nothing. Hit by KitMeter, whose segments are
            // narrow enough for the genre's corner cut to reach half their width.
            float c = Mathf.Min(cut, Mathf.Min(w, h) * 0.45f);
            return shape switch
            {
                KitShape.Chamfer => new[]
                {
                    new Vector2(x + c, y), new Vector2(x + w - c, y), new Vector2(x + w, y + c),
                    new Vector2(x + w, y + h - c), new Vector2(x + w - c, y + h),
                    new Vector2(x + c, y + h), new Vector2(x, y + h - c), new Vector2(x, y + c),
                },
                KitShape.Clip => new[]
                {
                    new Vector2(x + c, y), new Vector2(x + w, y), new Vector2(x + w, y + h - c),
                    new Vector2(x + w - c, y + h), new Vector2(x, y + h), new Vector2(x, y + c),
                },
                KitShape.Notch => new[]
                {
                    new Vector2(x + c, y), new Vector2(x + w - c, y), new Vector2(x + w, y + c * 0.6f),
                    new Vector2(x + w, y + h - c), new Vector2(x + w - c * 0.6f, y + h),
                    new Vector2(x + c, y + h), new Vector2(x, y + h - c * 0.6f), new Vector2(x, y + c),
                },
                KitShape.Speed => new[]
                {
                    new Vector2(x + c, y), new Vector2(x + w, y), new Vector2(x + w, y + h - c),
                    new Vector2(x + w - c, y + h), new Vector2(x, y + h), new Vector2(x, y + c),
                },
                // rpgui's PLAY plate. The points hang BELOW y+h — deliberately outside the rect,
                // which is the whole reason this reads as a different object rather than another
                // cut corner. Count scales with width so a chip gets 3 and a panel gets 12.
                KitShape.Spiked => Spikes(x, y, w, h, c, unit),

                // store's parchment cards: every edge offset by a different amount, so no two
                // sides are parallel. Seeded from the rect's own size, so it is stable across
                // redraws — a torn edge that reshuffles each frame reads as noise, not as paper.
                KitShape.Torn => Torn(x, y, w, h, c, unit),

                // ui1's mission bar, ui9's currency pill, and the mobile kit's "$ 200" chips all
                // do the SAME thing: a circular cap that OVERHANGS the left end, wider than the
                // bar is tall. Three independent references, and until now Capsule fell through
                // to a plain rounded rect — which is exactly why platformer never separated from
                // puzzle (0.026 against a 0.040 bar). Like Spiked, it leaves its bounding box.
                // Capsule is a BAR shape: the overhanging cap only means anything when there is a
                // bar for it to overhang. On a square or tall control the disc cannot be larger
                // than half the height AND fit the width, so it degenerates — return null and let
                // it draw as an ordinary pill rather than emit an invalid polygon.
                KitShape.Capsule when w >= h * 1.8f => CapsuleCap(x, y, w, h),

                // The sci-fi HUD sheet's defining move is ASYMMETRY: two diagonally opposite
                // corners cut long, the other two left square. A symmetric cut on all four
                // corners is what made shooter and racing measure 0.018 apart — both were a
                // rectangle with its corners taken off, differing only in angle.
                //
                // The cut is sized off HEIGHT, not the shared corner fraction. At 128x38 the
                // fraction gave an 11px nick on a 128px-wide plate -- racing vs shooter only
                // moved 0.018 -> 0.027 against a 0.040 bar, because a small nick on one corner
                // still reads as a rectangle. A cut this long changes the plate's whole profile.
                KitShape.Asymmetric => Asym(x, y, w, h),

                // Pixel-era UI rounds a corner in STEPS, not an arc. Reads unmistakably as 8-bit
                // and, unlike a radius, survives being measured at small size.
                KitShape.Stepped => Stepped(x, y, w, h, c),

                KitShape.Octagon => Oct(x, y, w, h, c),
                KitShape.Pentagon => new[]
                {
                    new Vector2(x + w * 0.5f, y), new Vector2(x + w, y + h * 0.38f),
                    new Vector2(x + w * 0.82f, y + h), new Vector2(x + w * 0.18f, y + h),
                    new Vector2(x, y + h * 0.38f),
                },
                KitShape.Chevron => new[]
                {
                    new Vector2(x, y), new Vector2(x + w - c, y), new Vector2(x + w, y + h * 0.5f),
                    new Vector2(x + w - c, y + h), new Vector2(x, y + h), new Vector2(x + c, y + h * 0.5f),
                },
                KitShape.Arrow => new[]
                {
                    new Vector2(x, y + h * 0.25f), new Vector2(x + w - c, y + h * 0.25f),
                    new Vector2(x + w - c, y), new Vector2(x + w, y + h * 0.5f),
                    new Vector2(x + w - c, y + h), new Vector2(x + w - c, y + h * 0.75f),
                    new Vector2(x, y + h * 0.75f),
                },
                KitShape.Parallelogram => new[]
                {
                    new Vector2(x + c, y), new Vector2(x + w, y),
                    new Vector2(x + w - c, y + h), new Vector2(x, y + h),
                },
                KitShape.Shield => new[]
                {
                    new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h * 0.62f),
                    new Vector2(x + w * 0.5f, y + h), new Vector2(x, y + h * 0.62f),
                },
                KitShape.Ribbon => new[]
                {
                    new Vector2(x - c, y), new Vector2(x + w + c, y),
                    new Vector2(x + w, y + h * 0.5f), new Vector2(x + w + c, y + h),
                    new Vector2(x - c, y + h), new Vector2(x, y + h * 0.5f),
                },
                _ => null,   // Rect / Round / Pill / Ellipse / Arch draw as rounded rects
            };

            // Triangular points hanging below the plate — rpgui's PLAY button.
            static Vector2[] Spikes(float x, float y, float w, float h, float c, float unit)
            {
                // BIG. The drop was min(h * 0.22, sp * 0.55) -- about 10px on a normal button,
                // which reads as a serrated edge, i.e. a smooth plate with a small decoration.
                // rpgui's PLAY plate has points roughly as deep as the plate's own padding, and
                // FEWER of them. Both are now unit-scaled: count from the unit rather than from
                // the height, so a wide panel gets bigger spikes instead of more of them.
                // FEWER and DEEPER. rpgui's plate has a handful of substantial points, not a
                // saw edge: spacing ~3.6 units and a drop of ~1.8 units gives about seven points
                // on a wide panel instead of a fifteen-tooth serration.
                int n = Mathf.Max(2, Mathf.RoundToInt(w / Mathf.Max(18f, unit * 3.6f)));
                float sp = w / n;
                float drop = Mathf.Min(unit * 1.8f, Mathf.Min(h * 0.45f, sp * 0.85f));
                var p = new List<Vector2>
                {
                    new(x + c, y), new(x + w - c, y), new(x + w, y + c), new(x + w, y + h),
                };
                for (int i = n - 1; i >= 0; i--)
                {
                    p.Add(new Vector2(x + sp * (i + 0.5f), y + h + drop));   // the point, OUTSIDE
                    p.Add(new Vector2(x + sp * i, y + h));
                }
                p.Add(new Vector2(x, y + h));
                p.Add(new Vector2(x, y + c));
                return p.ToArray();
            }

            /// <summary>ui1's mission bar / ui9's currency pill: a bar with a circular cap
            /// OVERHANGING the left end.
            ///
            /// The cap is larger than the bar is tall and its centre sits at the bar's left
            /// edge, so roughly half of it protrudes outside the control's rect — same principle
            /// as Spiked, and the reason this reads as an assembled object rather than a rounded
            /// rectangle. Traced as one closed polygon: cap arc first, then the bar's right end.
            /// </summary>
            static Vector2[] CapsuleCap(float x, float y, float w, float h)
            {
                float rad = h * 0.5f;
                // The disc must be LARGER than the bar's own radius or nothing protrudes and
                // the intersection math goes imaginary (sqrt of a negative), which is what made
                // Capsule fail on square and tall controls.
                float cap = Mathf.Clamp(Mathf.Min(h * 0.72f, w * 0.34f), rad * 1.12f, w * 0.45f);
                var cc = new Vector2(x + rad * 0.30f, y + rad);

                // Where the disc crosses the bar's top and bottom edges. Everything between
                // these two angles (going the LONG way round, through the left) is the part
                // that protrudes; the rest is hidden behind the bar. Tracing one closed loop
                // in a single winding direction is what keeps the polygon SIMPLE — the first
                // attempt swept two arcs in opposite directions and self-intersected, which
                // Godot reports as "Invalid polygon data" and then draws nothing at all.
                float half = Mathf.Sqrt(Mathf.Max(0.01f, cap * cap - rad * rad));
                float aTop = Mathf.Atan2(-rad, half);          // top crossing, right of centre
                float aBot = Mathf.Atan2(rad, half);           // bottom crossing

                const int seg = 16;
                var p = new List<Vector2> { cc + new Vector2(Mathf.Cos(aTop), Mathf.Sin(aTop)) * cap };

                // Top edge, then the bar's ordinary round right end, then back along the bottom.
                p.Add(new Vector2(x + w - rad, y));
                for (int i = 0; i <= seg; i++)
                {
                    float t = Mathf.Lerp(-Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, (float)i / seg);
                    p.Add(new Vector2(x + w - rad, y + rad) +
                          new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * rad);
                }
                p.Add(new Vector2(cc.X + half, y + h));

                // Round the disc the long way — bottom crossing, through the left extreme
                // (angle pi), up to the top crossing. This is the overhang.
                for (int i = 0; i <= seg * 2; i++)
                {
                    float t = Mathf.Lerp(aBot, Mathf.Tau + aTop, (float)i / (seg * 2));
                    p.Add(cc + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * cap);
                }
                return p.ToArray();
            }

            /// <summary>Sci-fi HUD frame: two diagonally opposite corners cut LONG, the other
            /// two square, plus a shallow notch bitten out of the top edge. The notch is the
            /// second tell of the family and costs nothing to carry.</summary>
            static Vector2[] Asym(float x, float y, float w, float h)
            {
                float d = Mathf.Min(h * 0.62f, w * 0.28f);       // the long diagonal cut
                float nw = Mathf.Min(w * 0.16f, h * 0.9f);       // notch width
                float nd = h * 0.16f;                            // notch depth
                float nx = x + w * 0.60f;
                return new[]
                {
                    new Vector2(x + d, y),
                    new Vector2(nx, y), new Vector2(nx + nw * 0.22f, y + nd),
                    new Vector2(nx + nw * 0.78f, y + nd), new Vector2(nx + nw, y),
                    new Vector2(x + w, y),
                    new Vector2(x + w, y + h - d), new Vector2(x + w - d, y + h),
                    new Vector2(x, y + h), new Vector2(x, y + d),
                };
            }

            /// <summary>Pixel-era stepped corner: a staircase instead of an arc.</summary>
            static Vector2[] Stepped(float x, float y, float w, float h, float c)
            {
                const int n = 3;                                   // steps per corner
                float s = Mathf.Max(1.5f, Mathf.Min(c, Mathf.Min(w, h) * 0.24f) / n);
                float k = s * n;
                var p = new List<Vector2>();

                // One clockwise loop: top edge, then each corner as n right-angle steps. Written
                // out per corner rather than as a sign-flipped generic, because the generic
                // version emitted the steps in the wrong order on two of the four corners and
                // self-intersected.
                p.Add(new Vector2(x + k, y));
                p.Add(new Vector2(x + w - k, y));
                for (int i = 0; i < n; i++)                          // top-right
                {
                    p.Add(new Vector2(x + w - k + (i + 1) * s, y + i * s));
                    p.Add(new Vector2(x + w - k + (i + 1) * s, y + (i + 1) * s));
                }
                p.Add(new Vector2(x + w, y + h - k));
                for (int i = 0; i < n; i++)                          // bottom-right
                {
                    p.Add(new Vector2(x + w - i * s, y + h - k + (i + 1) * s));
                    p.Add(new Vector2(x + w - (i + 1) * s, y + h - k + (i + 1) * s));
                }
                p.Add(new Vector2(x + k, y + h));
                for (int i = 0; i < n; i++)                          // bottom-left
                {
                    p.Add(new Vector2(x + k - (i + 1) * s, y + h - i * s));
                    p.Add(new Vector2(x + k - (i + 1) * s, y + h - (i + 1) * s));
                }
                p.Add(new Vector2(x, y + k));
                for (int i = 0; i < n; i++)                          // top-left
                {
                    p.Add(new Vector2(x + i * s, y + k - (i + 1) * s));
                    p.Add(new Vector2(x + (i + 1) * s, y + k - (i + 1) * s));
                }
                return p.ToArray();
            }

            // Non-parallel torn edges — store's parchment cards.
            static Vector2[] Torn(float x, float y, float w, float h, float c, float unit)
            {
                uint s = (uint)(Mathf.RoundToInt(w) * 73856093 ^ Mathf.RoundToInt(h) * 19349663);
                float J(float amp)
                {
                    s = s * 1664525u + 1013904223u;
                    return (((s >> 16) & 0xFF) / 255f - 0.5f) * 2f * amp;
                }
                // Amplitude from the UNIT and roughly doubled. At w*0.06/h*0.10 the tear was a
                // few pixels of wobble that read as anti-aliasing noise rather than as a torn
                // parchment edge.
                float ax = Mathf.Min(unit * 1.20f, w * 0.18f);
                float ay = Mathf.Min(unit * 1.20f, h * 0.28f);
                return new[]
                {
                    new Vector2(x + J(ax), y + J(ay)),
                    new Vector2(x + w * 0.5f, y + J(ay) * 0.6f),
                    new Vector2(x + w + J(ax), y + J(ay)),
                    new Vector2(x + w + J(ax) * 0.7f, y + h * 0.5f),
                    new Vector2(x + w + J(ax), y + h + J(ay)),
                    new Vector2(x + w * 0.5f, y + h + J(ay) * 0.6f),
                    new Vector2(x + J(ax), y + h + J(ay)),
                    new Vector2(x + J(ax) * 0.7f, y + h * 0.5f),
                };
            }

            static Vector2[] Oct(float x, float y, float w, float h, float c) => new[]
            {
                new Vector2(x + c, y), new Vector2(x + w - c, y), new Vector2(x + w, y + c),
                new Vector2(x + w, y + h - c), new Vector2(x + w - c, y + h),
                new Vector2(x + c, y + h), new Vector2(x, y + h - c), new Vector2(x, y + c),
            };
        }

        /// <summary>Corner cut in px.
        ///
        /// Angular silhouettes take it from HEIGHT so the diagonal is a real angle: on a 116x33
        /// racing button, min(w,h) x 0.08 gave a 2.6px nick that read as a plain rectangle.
        /// Rounded silhouettes keep the min-side radius rule, or a wide pill over-rounds.</summary>
        protected float CornerFor(Rect2 r)
        {
            bool angular = ActiveShape is KitShape.Chamfer or KitShape.Clip or KitShape.Notch
                or KitShape.Speed or KitShape.Octagon or KitShape.Parallelogram
                or KitShape.Chevron or KitShape.Arrow;
            if (!angular) return Snap(Mathf.Min(r.Size.X, r.Size.Y) * CornerFraction);

            // Derived from HEIGHT so a wide button gets a real rake rather than a 2.6px nick.
            float cut = r.Size.Y * Mathf.Max(0.22f, CornerFraction * 2.6f);

            // The cap is conditioned on how SQUARE the host is, because that is where a
            // height-derived cut misbehaves: on a square slot, 0.42 x height eats both corners
            // and the square becomes a diamond (seen on the rpg slot grid, twelve lozenges).
            // Capping unconditionally instead fixed the slots but shaved the rake off tall
            // buttons, which pushed rpg-vs-survival to a marginal pass on the greyscale gate.
            float shorter = Mathf.Min(r.Size.X, r.Size.Y);
            float aspect = shorter > 0f ? Mathf.Max(r.Size.X, r.Size.Y) / shorter : 1f;
            float capFrac = Mathf.Lerp(0.30f, 0.50f, Mathf.Clamp((aspect - 1f) / 1.2f, 0f, 1f));
            return Snap(Mathf.Min(cut, shorter * capFrac));
        }

        /// <summary>Layers that still apply over sliced art. Studs and sparkle are GEOMETRY the
        /// art may not carry; bevel and gloss are not re-applied, because painted art already has
        /// them and doubling them is what makes textured chrome look plastic.</summary>
        private void DrawAfterArt(Rect2 plate, KitGeometry g)
        {
            if (g.Sparkle > 0f && State != KitState.Disabled)
            {
                float sp = Mathf.Max(2f, plate.Size.Y * 0.07f);
                DrawCircle(plate.Position + new Vector2(plate.Size.X * 0.16f, plate.Size.Y * 0.22f),
                           sp, new Color(1, 1, 1, 0.5f * g.Sparkle));
            }
        }

        /// <summary>Art slot name for this widget — the file stem under the kit art root, e.g.
        /// "button" resolves &lt;root&gt;/&lt;genre&gt;/button_base.png. Defaults to the class
        /// name minus "Kit", so a widget opts in simply by existing.</summary>
        protected virtual string ArtName => GetType().Name.StartsWith("Kit")
            ? GetType().Name[3..].ToLowerInvariant()
            : GetType().Name.ToLowerInvariant();

        /// <summary>
        /// Draw a layer from sliced 9-patch art, if any exists for this genre and slot.
        ///
        /// Returns false when there is no art, which is the normal case and not a failure — the
        /// caller then draws the layer procedurally. That fallback is why the kit works with no
        /// art at all: PLAN.md's casual/mobile register is procedurally reachable, and only the
        /// painted register needs slices.
        ///
        /// A StyleBoxTexture is used rather than DrawTextureRect because only it does real
        /// 9-patch margins; stretching a bordered texture across a button is exactly how corner
        /// artwork gets smeared.
        /// </summary>
        protected bool TryDrawArt(Rect2 r, string slot)
        {
            if (r.Size.X < 1f || r.Size.Y < 1f) return false;
            var tex = KitArt.Resolve(_genre, ArtName, slot);
            if (tex == null) return false;

            string key = $"{_genre}/{ArtName}_{slot}";
            Vector4 m = KitArt.Margins(tex, key);
            var sb = new StyleBoxTexture { Texture = tex };
            sb.SetTextureMargin(Side.Left, m.X);
            sb.SetTextureMargin(Side.Top, m.Y);
            sb.SetTextureMargin(Side.Right, m.Z);
            sb.SetTextureMargin(Side.Bottom, m.W);
            // The palette still drives the tint, so sliced art reskins with the theme instead of
            // pinning one game's colours into every project that uses it.
            sb.ModulateColor = ArtModulate();
            DrawStyleBox(sb, r);
            return true;
        }

        /// <summary>Tint applied to sliced art. Neutral by default; state still reads through.</summary>
        protected virtual Color ArtModulate() => State switch
        {
            KitState.Hover => new Color(1.10f, 1.10f, 1.10f, 1f),
            KitState.Pressed => new Color(0.86f, 0.86f, 0.88f, 1f),
            KitState.Disabled or KitState.Locked => new Color(0.72f, 0.72f, 0.74f, 1f),
            _ => Colors.White,
        };

        /// <summary>Fill + rim, cut to the shape. The single primitive every layer is built on.</summary>
        /// <summary>
        /// Quantise a length to the art-pixel grid, for <see cref="KitRegister.Pixel"/> only.
        ///
        /// Everything else returns the value untouched, so this is inert for the other three
        /// registers rather than a global rounding pass -- which would have moved every widget in
        /// the kit by up to half a pixel for no reason.
        /// </summary>
        protected float Snap(float v)
        {
            var g = Geo;
            if (g.Register != KitRegister.Pixel) return v;
            float px = Mathf.Max(1f, g.PixelSize);
            return Mathf.Round(v / px) * px;
        }

        protected void DrawShape(Rect2 r, KitShape shape, Color fill, Color rim, float rimWidth)
        {
            // A sub-pixel rect cannot produce a valid polygon. Segmented meters generate these
            // at the leading edge of a partially-filled segment.
            if (r.Size.X < 1f || r.Size.Y < 1f) return;

            float cut = CornerPx(r);

            // THE PIXEL REGISTER'S CONSTRUCTION RULE. A rounded corner is an ARC, and an arc is
            // the single loudest way to break the 8-bit reading -- a stepped outline with a
            // smoothly rounded plate inside it is exactly the giveaway files 40 and 42 avoid.
            // So any rounding-family shape is rebuilt as a staircase.
            //
            // Only when there IS a corner to construct: `corner: 0` means a square button, and
            // routing that through Stepped would manufacture a 3-step notch the theme never asked
            // for. The angular silhouettes are left alone -- they are already made of straight
            // lines, which is what the register wants.
            if (Geo.Register == KitRegister.Pixel && cut >= Mathf.Max(1f, Geo.PixelSize)
                && shape is KitShape.Round or KitShape.Pill or KitShape.Ellipse or KitShape.Arch
                    or KitShape.Capsule)
                shape = KitShape.Stepped;

            // Capsule is on that list deliberately, and it costs something: it is platformer's
            // defining silhouette (ui1's mission bar with a cap overhanging the left end), and in
            // the pixel register it becomes a staircase like everything else. That is the right
            // trade -- a smoothly swept cap is precisely the anti-aliased curve the register
            // exists to eliminate, and the first version of this list omitted Capsule, which left
            // platformer/pixel8bit rendering 67 distinct grey levels while topdown/classic
            // rendered 3. One of those is pixel art.

            // One art pixel, never 1.7 -- a fractional rim anti-aliases into a soft grey line and
            // reads as vector art at any zoom.
            if (Geo.Register == KitRegister.Pixel && rimWidth > 0f)
                rimWidth = Mathf.Max(1f, Snap(rimWidth));

            var poly = Outline(shape, r, cut, Unit);
            if (poly != null)
            {
                DrawColoredPolygon(poly, fill);
                if (rimWidth > 0f)
                {
                    var closed = new Vector2[poly.Length + 1];
                    poly.CopyTo(closed, 0);
                    closed[^1] = poly[0];
                    DrawPolyline(closed, rim, rimWidth);
                }
                return;
            }

            float radius = shape switch
            {
                KitShape.Rect => 0f,
                // Capsule keeps ui1's big left radius; the overhanging cap is drawn as an
                // attachment by the widget, not carved out of the plate.
                KitShape.Pill or KitShape.Ellipse or KitShape.Capsule => Mathf.Min(r.Size.X, r.Size.Y) * 0.5f,
                _ => cut,
            };
            DrawRoundedRect(r, radius, fill);
            if (rimWidth > 0f) DrawRoundedRectOutline(r, radius, rim, rimWidth);
        }

        // Godot has no rounded-rect draw call; a StyleBoxFlat is the supported way to get one
        // with real corner radii, so the kit builds a throwaway box rather than approximating
        // corners with polygons (which visibly facets at small sizes).
        private void DrawRoundedRect(Rect2 r, float radius, Color fill)
        {
            var sb = new StyleBoxFlat { BgColor = fill };
            sb.SetCornerRadiusAll(Mathf.RoundToInt(radius));
            DrawStyleBox(sb, r);
        }

        private void DrawRoundedRectOutline(Rect2 r, float radius, Color rim, float width)
        {
            var sb = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = rim, DrawCenter = false };
            sb.SetCornerRadiusAll(Mathf.RoundToInt(radius));
            sb.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(width)));
            DrawStyleBox(sb, r);
        }


        // ── THE UNIT ──────────────────────────────────────────────────────────────────────
        //
        // One metric for the whole theme, taken from the same font size the widget's own size is
        // derived from (KitButton: `fs * HeightRatio`). Every decorative metric is a multiple of
        // it.
        //
        // WHY. Corner, rim, shadow and material used to be re-derived from the WIDGET's pixel
        // size -- `min(w,h) * CornerFraction`, `min(w,h) * 0.055`, "N grain tiles across the short
        // edge". So inside ONE theme a 40px chip and a 400px panel got corner radii, outline
        // weights, shadow offsets and material feature sizes that differed by TEN TIMES. The
        // reference kits do the opposite: a theme has one radius and one outline weight, and a
        // big panel and a small chip share them. Size and typography were two disconnected
        // systems; this is the join.

        /// <summary>The theme's base metric, in px. Everything decorative is a multiple.</summary>
        protected float Unit => Mathf.Max(8f, UiSurface.FontSize(this));

        /// <summary>
        /// Corner radius in PX, from the unit rather than from the widget's own size.
        ///
        /// Still capped at half the short edge, because a constant radius on a very small control
        /// would otherwise swallow it -- a chip degrades to a pill instead of going inside out.
        /// </summary>
        protected float CornerPx(Rect2 r)
        {
            float px = Unit * CornerFraction * CornerK;
            return Snap(Mathf.Min(px, Mathf.Min(r.Size.X, r.Size.Y) * 0.5f));
        }

        /// <summary>Converts the stored 0..0.5 corner FRACTION into unit multiples. Chosen so a
        /// default button keeps the radius it had when the fraction was applied to its own
        /// height, which is what stops this change from restyling every widget at once.</summary>
        private const float CornerK = 3.0f;

        /// <summary>Same conversion for layer insets, which were fractions of widget height.</summary>
        private const float InsetK = 3.0f;

        /// <summary>Bounding rect of a silhouette, for the few layers that still need one.</summary>
        protected static Rect2 PolyRect(Vector2[] p)
        {
            if (p.Length == 0) return new Rect2();
            float x0 = p[0].X, y0 = p[0].Y, x1 = x0, y1 = y0;
            foreach (var v in p)
            {
                x0 = Mathf.Min(x0, v.X); y0 = Mathf.Min(y0, v.Y);
                x1 = Mathf.Max(x1, v.X); y1 = Mathf.Max(y1, v.Y);
            }
            return new Rect2(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>
        /// A TRUE parallel offset of a silhouette. Positive grows, negative shrinks.
        ///
        /// This replaces two separate wrong things:
        ///   * insetting the RECT and re-deriving the corner, which gave
        ///     `radius_outer - 2*inset*fraction` instead of `radius_outer - inset`, so the layers
        ///     were not parallel and pinched at the corners; and
        ///   * scaling a polygon about its centre for the shadow, which on a 420x260 plate is not
        ///     an outward offset at all.
        ///
        /// Miter joins keep a chamfer a chamfer and a spike a spike; a round join would soften
        /// every angular silhouette into the "smooth add-on" reading these shapes are supposed to
        /// break out of. Returns the input when the offset would erase the shape.
        /// </summary>
        protected static Vector2[] OffsetPoly(Vector2[] poly, float delta)
        {
            if (poly.Length < 3 || Mathf.Abs(delta) < 0.01f) return poly;
            var pieces = Geometry2D.OffsetPolygon(poly, delta, Geometry2D.PolyJoinType.Miter);
            if (pieces.Count == 0) return poly;
            // Offsetting a non-convex silhouette (Spiked, Torn) can split it. Keep the largest
            // piece -- the body -- rather than the first, which may be a sliver off one spike.
            Vector2[] best = pieces[0];
            float bestArea = 0f;
            foreach (var q in pieces)
            {
                float a = Mathf.Abs(Area(q));
                if (a > bestArea) { bestArea = a; best = q; }
            }
            return bestArea > 1f ? best : poly;
        }

        private static float Area(Vector2[] p)
        {
            float a = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                Vector2 u = p[i], v = p[(i + 1) % p.Length];
                a += u.X * v.Y - v.X * u.Y;
            }
            return a * 0.5f;
        }

        /// <summary>Fill a silhouette, optionally stroking its own outline.</summary>
        protected void FillPoly(Vector2[] poly, Color fill, Color rim, float rimWidth)
        {
            if (poly.Length < 3) return;
            if (Geometry2D.TriangulatePolygon(poly).Length == 0) return;
            if (fill.A > 0.003f) DrawColoredPolygon(poly, fill);
            if (rimWidth > 0f && rim.A > 0.003f)
            {
                var closed = new Vector2[poly.Length + 1];
                poly.CopyTo(closed, 0);
                closed[^1] = poly[0];
                DrawPolyline(closed, rim, rimWidth);
            }
        }

        /// <summary>
        /// Draw <paramref name="band"/> clipped to <paramref name="host"/>.
        ///
        /// This is what makes an internal layer follow the widget's outline. The face shade used
        /// to draw six of its seven bands as KitShape.Rect, so an Ellipse, Octagon, Shield or
        /// Spiked widget had a stack of RECTANGLES shaded inside a non-rectangular outline --
        /// visible as straight edges cutting across a curved or pointed plate.
        /// </summary>
        protected void ClipFill(Vector2[] host, Vector2[] band, Color c)
        {
            if (c.A < 0.003f || host.Length < 3 || band.Length < 3) return;
            foreach (var piece in Geometry2D.IntersectPolygons(host, band))
            {
                if (piece.Length < 3 || Geometry2D.TriangulatePolygon(piece).Length == 0) continue;
                DrawColoredPolygon(piece, c);
            }
        }

        /// <summary>A horizontal band across a rect, as a polygon, generously overhanging on
        /// both sides so the CLIP decides where it ends rather than the band's own extent.</summary>
        protected static Vector2[] BandPoly(Rect2 r, float top, float bottom)
        {
            float l = r.Position.X - 4f, rt = r.Position.X + r.Size.X + 4f;
            return new[] { new Vector2(l, top), new Vector2(rt, top),
                           new Vector2(rt, bottom), new Vector2(l, bottom) };
        }

        // ── Material layers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw the genre's material by walking its declared LAYER STACK.
        ///
        /// This used to be a fixed sequence -- frame, plate, bevel, gloss, studs, sparkle,
        /// always, in that order -- so a genre could only ever be a re-tinted version of one
        /// build. That is the failure PLAN.md 4.1 exists to prevent, and it is why the carved
        /// register could not be pushed toward the painted band: there was nowhere to put another
        /// layer. The stack now comes from KitStacks, so a register's build is DATA.
        /// </summary>
        protected void DrawMaterial(Rect2 r, KitShape shape)
        {
            var m = Material;
            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            Color rim = RimColor();
            float fs = UiSurface.FontSize(this);
            float rimPx = g.Rim * (fs / 14f);

            // Sliced art, where a project has mounted any, still short-circuits the whole stack:
            // painted art already contains its own frame, bevel and rim.
            if (m.Base && TryDrawArt(r, "base"))
            {
                float ftArt = g.FramePx(r.Size.Y);
                var pl = Inset(r, ftArt);
                if (pl.Size.X <= 4 || pl.Size.Y <= 4) pl = r;
                TryDrawArt(pl, "plate");
                if (g.Sparkle > 0f && State != KitState.Disabled) DrawSparkle(pl, g);
                return;
            }
            if (!m.Base) return;

            float frame = g.FramePx(r.Size.Y);

            // THE HOST SILHOUETTE, built once. Every layer below is a PARALLEL OFFSET of this
            // polygon -- not its own re-derived shape -- so a spiked plate stays spiked all the
            // way in, and an octagon never contains a rectangle.
            // THE PIXEL REGISTER'S CONSTRUCTION RULE, applied where the silhouette is DECIDED.
            //
            // It used to live in DrawShape. Moving the layer stack onto polygons routed the plate
            // through FillPoly instead, so the rule stopped reaching the one thing it exists for
            // -- pixel8bit went straight back to drawing an arc, and the corner gate said so
            // (mobility 0.86 where a staircase is < 0.40). Deciding it here means every layer
            // inherits it, which is the same reason the silhouette is built once.
            KitShape shp = shape;
            float cornerPx = CornerPx(r);
            if (g.Register == KitRegister.Pixel && cornerPx >= Mathf.Max(1f, g.PixelSize)
                && shp is KitShape.Round or KitShape.Pill or KitShape.Ellipse or KitShape.Arch
                    or KitShape.Capsule)
                shp = KitShape.Stepped;

            var host = OutlinePoly(shp, r, cornerPx, g.Shear, g.Wobble, Unit);
            Vector2[] cur = host;

            // SHADOW FIRST, under everything -- see KitChrome.DrawPlate for why it is not a
            // member of the register's stack.
            KitShadow.Draw(this, g.Shadow, host, r, Unit, face);

            foreach (var layer in KitStacks.For(g.Register))
            {
                // Insets are UNIT multiples now. They were fractions of the widget's height, so
                // the same declared layer stack produced a 2px band on a chip and a 30px band on
                // a panel -- the bands are a property of the theme, not of the widget.
                float inset = layer.Inset >= 0f ? Unit * layer.Inset * InsetK : frame;

                // STRUCTURAL layers (Plate, Keyline) cut inward from the last plate; EFFECT
                // layers apply TO that plate and must not inset again.
                bool structural = layer.Kind is KitLayerKind.Plate or KitLayerKind.Keyline;
                Vector2[] poly = (layer.Kind == KitLayerKind.Plate && layer.Inset == 0f)
                    ? host
                    : structural ? OffsetPoly(cur, -inset) : cur;
                if (poly.Length < 3) continue;

                switch (layer.Kind)
                {
                    case KitLayerKind.Plate:
                    {
                        // Shade may exceed 1.0 -- the measured outer rim is 2.05x the plate --
                        // so brightening lifts toward white instead of clipping each channel,
                        // which would shift hue as it saturated.
                        //
                        // Shade < 0 is the sentinel for "the THEME decides this band's polarity"
                        // (KitGeometry.OutlineShade).
                        float shade = layer.Shade < 0f ? g.OutlineShade : layer.Shade;
                        Color c;
                        if (shade <= 1f)
                            c = new Color(face.R * shade, face.G * shade, face.B * shade, face.A);
                        else
                        {
                            float lum = UiSurface.Luminance(face);
                            float want = Mathf.Min(1f, lum * shade);
                            float t = Mathf.Clamp((want - lum) / Mathf.Max(0.001f, 1f - lum), 0f, 1f);
                            c = new Color(Mathf.Lerp(face.R, 1f, t), Mathf.Lerp(face.G, 1f, t),
                                          Mathf.Lerp(face.B, 1f, t), face.A);
                        }
                        // The outermost plate carries the genre's rim polarity; inner plates take
                        // ink, so a bright carved frame still reads against its own plate.
                        Color edge = layer.Inset == 0f ? rim : ink;
                        FillPoly(poly, c, edge, layer.Rim > 0f ? Mathf.Max(1f, rimPx * layer.Rim) : 0f);
                        cur = poly;
                        break;
                    }
                    case KitLayerKind.Keyline:
                    {
                        var c = new Color(face.R * layer.Shade, face.G * layer.Shade,
                                          face.B * layer.Shade, layer.Amount);
                        FillPoly(poly, new Color(0, 0, 0, 0), c, Mathf.Max(1f, rimPx * 0.5f));
                        break;
                    }
                    case KitLayerKind.Grain:
                        // The genre's MATERIAL, clipped to the face's own silhouette and drawn
                        // UNDER the lighting layers below, so gloss reads as sheen on the
                        // material rather than the material reading as dirt on the gloss.
                        KitGrain.Draw(this, _genre, poly, PolyRect(poly), face, layer.Amount);
                        break;
                    case KitLayerKind.Shade: DrawFaceShade(poly, layer.Amount); break;
                    case KitLayerKind.Bevel: DrawBevel(poly, g, layer.Amount); break;
                    case KitLayerKind.Gloss: DrawGloss(poly, g, layer.Amount); break;
                    case KitLayerKind.Studs:
                        if (g.Studs > 0 && State != KitState.Disabled) DrawStuds(r, g, ink);
                        break;
                    case KitLayerKind.Sparkle:
                        if (g.Sparkle > 0f && State != KitState.Disabled)
                            DrawSparkle(PolyRect(cur), g);
                        break;
                }
            }

            // The constructed frame LAST -- on top of the surface it encloses, as the references
            // draw it. Null for every genre but the sci-fi ones, so nothing else changes.
            KitEdge.Draw(this, g.EdgeRun, r, rimPx, RimColor(), g.Shear, g.Wobble);

        }

        private static Rect2 Inset(Rect2 r, float by)
            => new(r.Position + new Vector2(by, by), r.Size - new Vector2(by * 2f, by * 2f));

        /// <summary>Vertical falloff down the face -- the layer that decides PAINTED vs FLAT.
        /// The art pass measured a painted plate's bottom at 0.18-0.27 of its peak and a flat
        /// one at 0.76-0.84, and a gradient down the face is exactly that difference. Drawn as
        /// stacked bands rather than a shader, so it costs nothing and still cuts to the host
        /// silhouette at the last band.</summary>
        private void DrawFaceShade(Vector2[] host, float amount)
        {
            if (amount <= 0f || host.Length < 3) return;
            Rect2 r = PolyRect(host);
            if (r.Size.Y < 4f) return;

            const int bands = 7;
            float bh = r.Size.Y / bands;
            for (int i = 0; i < bands; i++)
            {
                // Darkest at the bottom; the top is left alone so the peak stays the peak.
                float t = (i + 1) / (float)bands;
                float a = amount * 0.42f * t * t;
                float top = r.Position.Y + bh * i;
                // CLIPPED to the host, not drawn as a rectangle. Six of these seven bands used to
                // be KitShape.Rect, so every non-rectangular genre had straight shaded edges
                // cutting across a curved, pointed or notched plate.
                ClipFill(host, BandPoly(r, top, top + bh + 1f), new Color(0, 0, 0, a));
            }
        }

        private void DrawGloss(Vector2[] host, KitGeometry g, float amount)
        {
            if (g.Gloss <= 0f || amount <= 0f || Sunken || host.Length < 3) return;
            Rect2 r = PolyRect(host);
            if (r.Size.X < 6f || r.Size.Y < 6f) return;

            // Band depth is a UNIT multiple, so the highlight is the same thickness on a chip and
            // on a panel. It was `plate.Size.Y * 0.26`, which made it a different feature at every
            // widget size.
            float h = Mathf.Min(Unit * 1.6f, r.Size.Y * 0.45f);

            if (g.GlossStyle == KitGloss.Linear)
            {
                // The soft sheen, as an INSET COPY OF THE HOST intersected with a top band. It
                // used to be an inset RECT re-cut to the host shape, which on an ellipse or a
                // shield produced a second, differently-proportioned silhouette floating inside
                // the first -- a shape inside a shape that did not agree with it.
                var inner = OffsetPoly(host, -Unit * 0.35f);
                ClipFill(inner, BandPoly(r, r.Position.Y - 4f, r.Position.Y + h * 1.3f),
                         new Color(1, 1, 1, Mathf.Clamp(0.16f * g.Gloss * amount, 0f, 1f)));
                return;
            }

            // A BAND has a FLOOR. Sized as `0.22 * Gloss * amount` it inherited the genre's
            // sheen intensity, so a carved genre (Gloss 0.30, stack amount 0.55) rendered its
            // "discrete lighter band" at 3.6% alpha -- a wash that neither the eye nor the gate
            // could tell from the soft sheen. Choosing HardBand is choosing a construction, not
            // an intensity; the reference sheets put a clearly lighter quarter across the top.
            float a = Mathf.Clamp(Mathf.Max(0.13f, 0.30f * g.Gloss) * amount, 0f, 1f);
            if (a < 0.004f) return;

            float top = r.Position.Y - 4f;
            float l = r.Position.X - 4f, rt = r.Position.X + r.Size.X + 4f;

            Vector2[] band;
            if (g.GlossStyle == KitGloss.HardBand)
            {
                band = new[]
                {
                    new Vector2(l, top), new Vector2(rt, top),
                    new Vector2(rt, r.Position.Y + h), new Vector2(l, r.Position.Y + h),
                };
            }
            else
            {
                // Convex lower boundary, deepest at the centre. Sampled rather than a true arc
                // because it has to be a polygon to clip against the silhouette anyway.
                const int steps = 24;
                var pts = new List<Vector2> { new(l, top), new(rt, top) };
                for (int i = steps; i >= 0; i--)
                {
                    float t = i / (float)steps;
                    float y = r.Position.Y + h * (0.62f + 0.38f * Mathf.Sin(Mathf.Pi * t));
                    pts.Add(new Vector2(Mathf.Lerp(l, rt, t), y));
                }
                band = pts.ToArray();
            }
            ClipFill(host, band, new Color(1, 1, 1, a));
        }

        /// <summary>
        /// Inner bevel that WALKS THE SILHOUETTE.
        ///
        /// It used to stroke the four sides of the bounding RECT, so a spiked, octagonal, shield
        /// or torn plate carried a rectangular bevel that ignored its own outline -- the clearest
        /// case of the "shape inside a shape that disagrees with it" problem. Now every edge of
        /// the polygon is lit or shaded by its own outward normal against a top-left key light, so
        /// the spikes on rpg's plate are beveled like the rest of the edge.
        /// </summary>
        private void DrawBevel(Vector2[] host, KitGeometry g, float amount)
        {
            if (g.Bevel <= 0f || amount <= 0f || host.Length < 3) return;

            // Thickness from the UNIT, not from the plate's height: a bevel is a property of the
            // theme's material, and `plate.Size.Y * 0.08` made it 3px on a chip and 21px on a panel.
            float t = Mathf.Max(1f, Unit * 0.20f * g.Bevel);
            var inner = OffsetPoly(host, -t * 0.6f);
            if (inner.Length < 3) return;

            Color hi = new(1, 1, 1, 0.22f * g.Bevel * amount);
            Color lo = new(0, 0, 0, 0.26f * g.Bevel * amount);

            // The CASUAL register omits the dark half: that family expresses depth with a thick
            // outline and a top band, and raking a shadow across the plate reads as painted.
            bool allowDark = g.Register != KitRegister.Casual;

            Vector2 c = Vector2.Zero;
            foreach (var v in inner) c += v;
            c /= inner.Length;

            var key = new Vector2(-0.7071f, -0.7071f);        // light from the top-left

            // Stroked as CONTIGUOUS RUNS, not edge by edge.
            //
            // Drawing each edge with DrawLine looked equivalent and was not: once the arc
            // resolution became adaptive, a large plate has ~200 edges of about 3px each, and a
            // 7px-thick line on a 3px segment overshoots its own ends. The result was a ragged
            // fringe all the way around a shape whose outline is actually smooth. A polyline
            // joins its segments instead of stacking overshooting caps.
            var run = new List<Vector2>();
            bool runLit = false;

            void Flush()
            {
                if (run.Count >= 2 && (runLit || allowDark))
                    DrawPolyline(run.ToArray(), runLit ? hi : lo, t);
                run.Clear();
            }

            for (int i = 0; i <= inner.Length; i++)
            {
                Vector2 a = inner[i % inner.Length], b = inner[(i + 1) % inner.Length];
                Vector2 d = b - a;
                float len = d.Length();
                if (len < 0.01f) continue;
                d /= len;
                Vector2 n = new(-d.Y, d.X);
                if (n.Dot(a - c) < 0f) n = -n;                // outward

                bool lit = n.Dot(key) > 0f;
                if (Sunken) lit = !lit;

                if (run.Count == 0) { runLit = lit; run.Add(a); }
                else if (lit != runLit) { run.Add(a); Flush(); runLit = lit; run.Add(a); }
                run.Add(b);
            }
            Flush();
        }

        private void DrawStuds(Rect2 r, KitGeometry g, Color ink)
        {
            float sr = Mathf.Max(1.5f, r.Size.Y * 0.06f);
            float off = Mathf.Max(sr * 1.8f, g.FramePx(r.Size.Y) * 0.55f);
            foreach (var c in new[]
            {
                r.Position + new Vector2(off, off),
                r.Position + new Vector2(r.Size.X - off, off),
                r.Position + new Vector2(off, r.Size.Y - off),
                r.Position + new Vector2(r.Size.X - off, r.Size.Y - off),
            })
            {
                DrawCircle(c, sr, new Color(1, 1, 1, 0.30f));
                DrawArc(c, sr, 0, Mathf.Tau, 12, ink, Mathf.Max(1f, sr * 0.35f));
            }
        }

        private void DrawSparkle(Rect2 plate, KitGeometry g)
        {
            float sp = Mathf.Max(2f, plate.Size.Y * 0.07f);
            DrawCircle(plate.Position + new Vector2(plate.Size.X * 0.16f, plate.Size.Y * 0.22f),
                       sp, new Color(1, 1, 1, 0.5f * g.Sparkle));
        }


        /// <summary>
        /// An overhanging title banner straddling the top edge of a host rect.
        ///
        /// Lives on the base class because it is, by the art pass's count, "the single most
        /// repeated element across all 7 kits" -- panels, cards, modals and store tiles all carry
        /// one, and every one of them OVERHANGS rather than sitting inline. An inline Label is
        /// what the framework shipped instead, everywhere.
        ///
        /// Measured: height <b>0.14 x the host</b> (rpgui2: 18px on a 129px card).
        /// <paramref name="shade"/> defaults to <b>0.44 x the frame's lightness</b> (gameui2),
        /// i.e. a title plate reads RECESSED, not raised -- though the polarity is per-family
        /// (gameui4's banner is white L=0.97), so it is a parameter rather than a constant.
        /// </summary>
        /// <summary>The straddling header plaque. Delegates to <see cref="KitChrome.DrawBanner"/>
        /// so the Godot-derived widgets and this family draw the identical construction.</summary>
        protected void DrawBanner(Rect2 host, string text, KitShape shape,
                                  float heightRatio = 0.14f, float widthRatio = 0.62f,
                                  float shade = 0.44f)
            => KitChrome.DrawBanner(this, _genre, host, text, shape, heightRatio, widthRatio, shade);

        /// <summary>Attachments last, so they draw OVER the host and can cross its edge.</summary>
        protected void DrawAttachments()
        {
            foreach (var a in Attachments)
            {
                Rect2 r = a.Resolve(Size);
                Color fill = UiSurface.Semantic(this, a.Role);
                DrawShape(r, a.Shape, fill, UiSurface.Ink(fill), 2f);

                if (a.Icon != null)
                    DrawTextureRect(a.Icon, r.Grow(-r.Size.X * 0.22f), false);
                else if (!string.IsNullOrEmpty(a.Text))
                {
                    var font = GetThemeDefaultFont();
                    int fs = UiSurface.FontSize(this, 0.8f);
                    if (font != null)
                    {
                        Vector2 m = font.GetStringSize(a.Text, HorizontalAlignment.Left, -1, fs);
                        DrawString(font, r.Position + new Vector2((r.Size.X - m.X) * 0.5f,
                                                                  (r.Size.Y + m.Y * 0.62f) * 0.5f),
                                   a.Text, HorizontalAlignment.Left, -1, fs,
                                   UiSurface.Text(this));
                    }
                }
            }
        }

        public void SetState(KitState s)
        {
            if (State == s) return;
            State = s;
            QueueRedraw();
        }
    }
}
