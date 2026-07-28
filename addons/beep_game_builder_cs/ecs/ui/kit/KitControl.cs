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
        protected static Vector2[]? Outline(KitShape shape, Rect2 r, float cut)
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
                KitShape.Pill or KitShape.Ellipse => Mathf.Min(r.Size.X, r.Size.Y) * 0.5f,
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

        /// <summary>Draw the genre's material into a rect: base, bevel, gloss, rim, sparkle.
        /// One call, so every kit widget is made of the same stuff.</summary>
        protected void DrawMaterial(Rect2 r, KitShape shape)
        {
            var m = Material;
            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            Color rim = RimColor();

            float rimPx = g.Rim * (UiSurface.FontSize(this) / 14f);

            // FRAME then PLATE - two nested shapes, which is how every control on
            // Example_Art/rpgui.png is built, and how PanelFrameComponent has always drawn its
            // frame + recessed well. A single bevelled plate is what made every genre generic.
            //
            // A genre in the CASUAL register (KitFrameMode.None) deliberately has no frame: that
            // family is one flat plate with a thick dark outline, and carving it would average
            // the two reference families together.
            Rect2 plate = r;
            if (m.Base)
            {
                float ft = g.FramePx(r.Size.Y);
                if (ft > 0f)
                {
                    DrawShape(r, shape, face, rim, rimPx);
                    plate = new Rect2(r.Position + new Vector2(ft, ft),
                                      r.Size - new Vector2(ft * 2f, ft * 2f));
                    if (plate.Size.X > 4 && plate.Size.Y > 4)
                    {
                        // The inner plate carries its own shade and its own rim, so the frame
                        // reads as a separate piece rather than a border on one slab. The shade
                        // follows ELEVATION, not the genre - see KitGeometry.PlateShadeFor.
                        float ps = g.PlateShadeFor(Elevation);
                        Color inner = new Color(face.R * ps, face.G * ps, face.B * ps, face.A);

                        // A hairline frame is 1-3px, so an ink rim on the plate sits directly on
                        // top of it and swallows it: racing asked for a 1.45x bright rim and
                        // rendered 0.17x, because the plate's own dark rim covered the frame.
                        // The thin line IS the edge treatment in that register.
                        float innerRim = g.FrameMode == KitFrameMode.Hairline
                            ? 0f
                            : Mathf.Max(1f, rimPx * 0.55f);
                        DrawShape(plate, shape, inner, ink, innerRim);
                    }
                    else plate = r;
                }
                else DrawShape(r, shape, face, rim, rimPx);
            }

            if (g.Bevel > 0f)
            {
                // Light along the top-left and dark along the bottom-right, swapped when sunken.
                // Inset so the bevel sits inside the rim rather than on top of it.
                float t = Mathf.Max(1f, plate.Size.Y * 0.08f * g.Bevel);
                var inner = new Rect2(plate.Position + new Vector2(t * 1.2f, t * 1.2f),
                                      plate.Size - new Vector2(t * 2.4f, t * 2.4f));
                if (inner.Size.X > 2 && inner.Size.Y > 2)
                {
                    Color hi = new Color(1, 1, 1, 0.22f * g.Bevel);
                    Color lo = new Color(0, 0, 0, 0.26f * g.Bevel);
                    Color top = Sunken ? lo : hi, bot = Sunken ? hi : lo;

                    // The CASUAL register omits the dark half of the bevel. That family expresses
                    // depth with a thick outline and a discrete top band; raking a shadow across
                    // the plate is the carved family's cue, and borrowing it is what made every
                    // casual genre measure bottom:peak 0.23-0.26 (painted) against a 0.76-0.84
                    // flat target. A gradient down the face IS the painted reading.
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
            }

            if (g.Gloss > 0f && !Sunken)
            {
                // Sheen across the upper face only — the reference kits all light from above.
                var sheen = new Rect2(plate.Position + new Vector2(plate.Size.X * 0.07f, plate.Size.Y * 0.10f),
                                      new Vector2(plate.Size.X * 0.86f, plate.Size.Y * 0.34f));
                // Cut to the HOST's silhouette, never substituted with Round. Drawing a rounded
                // highlight inside an angular outline is the "triangle inside a curved triangle".
                if (sheen.Size.X > 2 && sheen.Size.Y > 2)
                    DrawShape(sheen, shape,
                              new Color(1, 1, 1, 0.16f * g.Gloss), new Color(0, 0, 0, 0), 0f);
            }

            // Corner studs. After silhouette this is the strongest NON-COLOUR genre tell,
            // which is why it is geometry rather than decoration.
            if (g.Studs > 0 && State != KitState.Disabled)
            {
                float sr = Mathf.Max(1.5f, r.Size.Y * 0.06f);
                float off = Mathf.Max(sr * 1.8f, g.FramePx(r.Size.Y) * 0.55f);
                var corners = new[]
                {
                    r.Position + new Vector2(off, off),
                    r.Position + new Vector2(r.Size.X - off, off),
                    r.Position + new Vector2(off, r.Size.Y - off),
                    r.Position + new Vector2(r.Size.X - off, r.Size.Y - off),
                };
                foreach (var c in corners)
                {
                    DrawCircle(c, sr, new Color(1, 1, 1, 0.30f));
                    DrawArc(c, sr, 0, Mathf.Tau, 12, ink, Mathf.Max(1f, sr * 0.35f));
                }
            }

            if (g.Sparkle > 0f && State != KitState.Disabled)
            {
                float sp = Mathf.Max(2f, plate.Size.Y * 0.07f);
                DrawCircle(plate.Position + new Vector2(plate.Size.X * 0.16f, plate.Size.Y * 0.22f),
                           sp, new Color(1, 1, 1, 0.5f * g.Sparkle));
            }
        }

        /// <summary>
        /// An overhanging title banner straddling the top edge of a host rect.
        ///
        /// Lives on the base class because it is, by the art pass's count, "the single most
        /// repeated element across all 7 kits" — panels, cards, modals and store tiles all carry
        /// one, and every one of them OVERHANGS rather than sitting inline. An inline Label is
        /// what the framework shipped instead, everywhere.
        ///
        /// Measured: height <b>0.14 x the host</b> (rpgui2: 18px on a 129px card).
        /// <paramref name="shade"/> defaults to <b>0.44 x the frame's lightness</b> (gameui2),
        /// i.e. a title plate reads RECESSED, not raised — though the polarity is per-family
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
                // Never narrower than the text it carries.
                float need = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X + fs * 2f;
                w = Mathf.Max(w, Mathf.Min(need, host.Size.X * 1.08f));
            }

            // Straddles the edge: centred on it, so half the plate sits outside the host. This
            // is the move containers cannot express and the reason it is drawn, not parented.
            var r = new Rect2(host.Position.X + (host.Size.X - w) * 0.5f,
                              host.Position.Y - h * 0.5f, w, h);

            Color face = FaceColor();
            Color plate = new Color(face.R * shade, face.G * shade, face.B * shade, 1f);
            DrawShape(r, shape, plate, InkColor(),
                      Mathf.Max(1f, Geo.Rim * 0.7f * (fs / 14f)));

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
