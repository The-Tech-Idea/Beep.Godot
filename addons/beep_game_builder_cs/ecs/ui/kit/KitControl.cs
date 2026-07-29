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
        protected float CornerFraction => CornerOverride >= 0f ? CornerOverride : Geo.Corner;

        protected KitState State = KitState.Normal;
        protected readonly List<KitAttach> Attachments = new();

        private string _genre = "";

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
        internal static Vector2[]? Outline(KitShape shape, Rect2 r, float cut)
        {
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
                KitShape.Spiked => Spikes(x, y, w, h, c),

                // store's parchment cards: every edge offset by a different amount, so no two
                // sides are parallel. Seeded from the rect's own size, so it is stable across
                // redraws — a torn edge that reshuffles each frame reads as noise, not as paper.
                KitShape.Torn => Torn(x, y, w, h, c),

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
            static Vector2[] Spikes(float x, float y, float w, float h, float c)
            {
                int n = Mathf.Max(3, Mathf.RoundToInt(w / Mathf.Max(8f, h * 0.42f)));
                float sp = w / n, drop = Mathf.Min(h * 0.22f, sp * 0.55f);
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

            // Non-parallel torn edges — store's parchment cards.
            static Vector2[] Torn(float x, float y, float w, float h, float c)
            {
                uint s = (uint)(Mathf.RoundToInt(w) * 73856093 ^ Mathf.RoundToInt(h) * 19349663);
                float J(float amp)
                {
                    s = s * 1664525u + 1013904223u;
                    return (((s >> 16) & 0xFF) / 255f - 0.5f) * 2f * amp;
                }
                float ax = w * 0.06f, ay = h * 0.10f;
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
            if (!angular) return Mathf.Min(r.Size.X, r.Size.Y) * CornerFraction;

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
            return Mathf.Min(cut, shorter * capFrac);
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
        protected void DrawShape(Rect2 r, KitShape shape, Color fill, Color rim, float rimWidth)
        {
            // A sub-pixel rect cannot produce a valid polygon. Segmented meters generate these
            // at the leading edge of a partially-filled segment.
            if (r.Size.X < 1f || r.Size.Y < 1f) return;

            float cut = CornerFor(r);
            var poly = Outline(shape, r, cut);
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
            Rect2 cur = r;
            foreach (var layer in KitStacks.For(g.Register))
            {
                float inset = layer.Inset >= 0f ? r.Size.Y * layer.Inset : frame;

                // STRUCTURAL layers (Plate, Keyline) cut inward from the last plate; EFFECT
                // layers apply TO that plate and must not inset again. Insetting everything
                // compounded: each effect stepped a further frame inward, so the face shade
                // stopped short of the plate's bottom edge and the carved register measured
                // FURTHER from painted (rpg 0.33 -> 0.40) after the layer stack was introduced,
                // which is the opposite of what the stack was built to do.
                bool structural = layer.Kind is KitLayerKind.Plate or KitLayerKind.Keyline;
                Rect2 box = (layer.Kind == KitLayerKind.Plate && layer.Inset == 0f)
                    ? r
                    : structural ? Inset(cur, inset) : cur;
                if (box.Size.X < 2f || box.Size.Y < 2f) continue;
                KitShape s = layer.Shape ?? shape;

                switch (layer.Kind)
                {
                    case KitLayerKind.Plate:
                    {
                        // Shade may exceed 1.0 -- the measured outer rim is 2.05x the plate --
                        // so brightening lifts toward white instead of clipping each channel,
                        // which would shift hue as it saturated.
                        Color c;
                        if (layer.Shade <= 1f)
                            c = new Color(face.R * layer.Shade, face.G * layer.Shade,
                                          face.B * layer.Shade, face.A);
                        else
                        {
                            float lum = UiSurface.Luminance(face);
                            float want = Mathf.Min(1f, lum * layer.Shade);
                            float t = Mathf.Clamp((want - lum) / Mathf.Max(0.001f, 1f - lum), 0f, 1f);
                            c = new Color(Mathf.Lerp(face.R, 1f, t), Mathf.Lerp(face.G, 1f, t),
                                          Mathf.Lerp(face.B, 1f, t), face.A);
                        }
                        // The outermost plate carries the genre's rim polarity; inner plates take
                        // ink, so a bright carved frame still reads against its own plate.
                        Color edge = layer.Inset == 0f ? rim : ink;
                        DrawShape(box, s, c, edge,
                                  layer.Rim > 0f ? Mathf.Max(1f, rimPx * layer.Rim) : 0f);
                        cur = box;
                        break;
                    }
                    case KitLayerKind.Keyline:
                    {
                        var c = new Color(face.R * layer.Shade, face.G * layer.Shade,
                                          face.B * layer.Shade, layer.Amount);
                        DrawShape(box, s, new Color(0, 0, 0, 0), c, Mathf.Max(1f, rimPx * 0.5f));
                        break;
                    }
                    case KitLayerKind.Shade: DrawFaceShade(box, s, layer.Amount); break;
                    case KitLayerKind.Bevel: DrawBevel(box, g, layer.Amount); break;
                    case KitLayerKind.Gloss: DrawGloss(box, s, g, layer.Amount); break;
                    case KitLayerKind.Studs:
                        if (g.Studs > 0 && State != KitState.Disabled) DrawStuds(r, g, ink);
                        break;
                    case KitLayerKind.Sparkle:
                        if (g.Sparkle > 0f && State != KitState.Disabled) DrawSparkle(cur, g);
                        break;
                }
            }
        }

        private static Rect2 Inset(Rect2 r, float by)
            => new(r.Position + new Vector2(by, by), r.Size - new Vector2(by * 2f, by * 2f));

        /// <summary>Vertical falloff down the face -- the layer that decides PAINTED vs FLAT.
        /// The art pass measured a painted plate's bottom at 0.18-0.27 of its peak and a flat
        /// one at 0.76-0.84, and a gradient down the face is exactly that difference. Drawn as
        /// stacked bands rather than a shader, so it costs nothing and still cuts to the host
        /// silhouette at the last band.</summary>
        private void DrawFaceShade(Rect2 r, KitShape shape, float amount)
        {
            if (amount <= 0f) return;
            const int bands = 7;
            float bh = r.Size.Y / bands;
            for (int i = 0; i < bands; i++)
            {
                // Darkest at the bottom; the top is left alone so the peak stays the peak.
                float t = (i + 1) / (float)bands;
                float a = amount * 0.42f * t * t;
                var band = new Rect2(r.Position.X, r.Position.Y + bh * i, r.Size.X, bh + 1f);
                if (band.Size.Y < 1f) continue;
                DrawShape(band, i == bands - 1 ? shape : KitShape.Rect,
                          new Color(0, 0, 0, a), new Color(0, 0, 0, 0), 0f);
            }
        }

        private void DrawBevel(Rect2 plate, KitGeometry g, float amount)
        {
            if (g.Bevel <= 0f || amount <= 0f) return;
            float t = Mathf.Max(1f, plate.Size.Y * 0.08f * g.Bevel);
            var inner = new Rect2(plate.Position + new Vector2(t * 1.2f, t * 1.2f),
                                  plate.Size - new Vector2(t * 2.4f, t * 2.4f));
            if (inner.Size.X <= 2 || inner.Size.Y <= 2) return;

            Color hi = new(1, 1, 1, 0.22f * g.Bevel * amount);
            Color lo = new(0, 0, 0, 0.26f * g.Bevel * amount);
            Color top = Sunken ? lo : hi, bot = Sunken ? hi : lo;

            // The CASUAL register omits the dark half: that family expresses depth with a thick
            // outline and a top band, and raking a shadow across the plate reads as painted.
            bool allowDark = g.Register != KitRegister.Casual;
            if (allowDark || !Sunken)
            {
                DrawLine(inner.Position, inner.Position + new Vector2(inner.Size.X, 0), top, t);
                DrawLine(inner.Position, inner.Position + new Vector2(0, inner.Size.Y), top, t);
            }
            if (allowDark || Sunken)
            {
                DrawLine(inner.Position + new Vector2(0, inner.Size.Y), inner.End, bot, t);
                DrawLine(inner.Position + new Vector2(inner.Size.X, 0), inner.End, bot, t);
            }
        }

        private void DrawGloss(Rect2 plate, KitShape shape, KitGeometry g, float amount)
        {
            if (g.Gloss <= 0f || amount <= 0f || Sunken) return;
            var sheen = new Rect2(
                plate.Position + new Vector2(plate.Size.X * 0.07f, plate.Size.Y * 0.10f),
                new Vector2(plate.Size.X * 0.86f, plate.Size.Y * 0.34f));
            if (sheen.Size.X <= 2 || sheen.Size.Y <= 2) return;
            // Cut to the HOST's silhouette, never substituted with Round.
            DrawShape(sheen, shape, new Color(1, 1, 1, 0.16f * g.Gloss * amount),
                      new Color(0, 0, 0, 0), 0f);
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
        protected void DrawBanner(Rect2 host, string text, KitShape shape,
                                  float heightRatio = 0.14f, float widthRatio = 0.62f,
                                  float shade = 0.44f)
        {
            if (string.IsNullOrEmpty(text) || host.Size.X < 8f || host.Size.Y < 8f) return;

            var font = GetThemeDefaultFont();
            int fs = UiSurface.FontSize(this);

            // Floor the height at the type, or the banner clips its own text on a short host.
            float h = Mathf.Max(fs * 1.5f, host.Size.Y * heightRatio);
            float w = host.Size.X * widthRatio;
            if (font != null)
            {
                float need = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X + fs * 2f;
                w = Mathf.Max(w, Mathf.Min(need, host.Size.X * 1.08f));
            }

            // Straddles the edge: centred on it, so half the plate sits outside the host. This is
            // the move containers cannot express and the reason it is drawn, not parented.
            var r = new Rect2(host.Position.X + (host.Size.X - w) * 0.5f,
                              host.Position.Y - h * 0.5f, w, h);

            Color face = FaceColor();
            Color plate = new(face.R * shade, face.G * shade, face.B * shade, 1f);
            DrawShape(r, shape, plate, InkColor(), Mathf.Max(1f, Geo.Rim * 0.7f * (fs / 14f)));

            if (font == null) return;
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            Color ink = UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f, 1f)
                : new Color(0.98f, 0.96f, 0.92f, 1f);
            DrawString(font,
                       new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                   r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                       text, HorizontalAlignment.Left, -1, fs, ink);
        }

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
