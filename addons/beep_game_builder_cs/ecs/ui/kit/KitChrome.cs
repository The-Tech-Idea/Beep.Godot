using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// The kit's plate, drawn onto ANY CanvasItem — shared by every drop-in that derives from a
    /// Godot control rather than from <see cref="KitControl"/>.
    ///
    /// WHY THESE DROP-INS EXIST
    /// ------------------------
    /// `KitSlider`, `KitTabStrip`, `KitToggle` and `KitArrowSelector` all derive from KitControl,
    /// which buys the layer/attachment model but makes them NOT an HSlider, TabContainer,
    /// CheckButton or OptionButton. `SettingsMenu.cs` alone resolves ten controls by Godot type —
    /// `Find&lt;TabContainer&gt;("Tabs")`, `Find&lt;OptionButton&gt;("ResolutionOption")`,
    /// `Find&lt;CheckButton&gt;(name)` — and every one would return null after such a swap, with
    /// nothing logged. That is the same trap that left 126 buttons unconverted until
    /// <see cref="KitPushButton"/> derived from Button instead.
    ///
    /// So the migration drop-ins derive from the Godot type, suppress its stock chrome with empty
    /// StyleBoxes, and draw the kit's bands here. Typed lookups, signals and layout all survive.
    ///
    /// One copy of the band walk, not five: the register stack is the kit's definition of what a
    /// plate IS, and five hand-copies of it would drift within a release.
    /// </summary>
    public static class KitChrome
    {
        /// <summary>Blank a control's StyleBoxes so the base class paints nothing, KEEPING the
        /// content margins — Godot sizes a control's text and children from them, so zeroing them
        /// collapses the widget onto its label.</summary>
        public static void Suppress(Godot.Control ctl, string[] states, float frame, float pad,
                                    float vpad = -1f)
        {
            if (vpad < 0f) vpad = frame * 0.5f + pad * 0.4f;
            foreach (string s in states)
            {
                var sb = new StyleBoxEmpty
                {
                    ContentMarginLeft = frame + pad,
                    ContentMarginRight = frame + pad,
                    ContentMarginTop = vpad,
                    ContentMarginBottom = vpad,
                };
                ctl.AddThemeStyleboxOverride(s, sb);
            }
        }

        /// <summary>A 1×1 transparent texture, for icon slots that cannot be blanked with a
        /// StyleBox (Slider's grabber, CheckButton's tick). Cached — one per process, not one
        /// per redraw.</summary>
        public static Texture2D Blank => _blank ??= MakeBlank();
        private static Texture2D? _blank;

        private static Texture2D MakeBlank()
        {
            var img = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            img.SetPixel(0, 0, new Color(0, 0, 0, 0));
            return ImageTexture.CreateFromImage(img);
        }

        /// <summary>
        /// Draw the genre's plate into <paramref name="body"/>: the register's band stack, the
        /// material grain, and the rim. Everything a kit widget's face is made of, minus text.
        /// </summary>
        public static void DrawPlate(CanvasItem ci, string genre, Rect2 body, Color face,
                                     KitState state, float rimScale = 1f)
        {
            if (body.Size.X < 3f || body.Size.Y < 3f) return;
            var g = KitGeometry.ForGenre(genre);
            KitShape shape = KitMaterial.ShapeForGenre(genre);
            Color ink = UiSurface.Ink(face);
            float rimPx = Mathf.Max(1f, g.Rim * rimScale);
            float frame = g.FramePx(body.Size.Y);

            // SHADOW FIRST, under the whole stack. It is not in the register's layer list on
            // purpose: the register says how a plate is BUILT, the theme says how it is
            // SEPARATED from its ground, and two themes of one genre differ by the second more
            // than the first.
            KitShadow.Draw(ci, g.Shadow, Poly(shape, body, g), body, KitShadow.UnitFor(body), face);

            Rect2 cur = body;

            foreach (var layer in KitStacks.For(g.Register))
            {
                if (layer.Kind == KitLayerKind.Grain)
                {
                    Rect2 gb = layer.Inset >= 0f ? Inset(body, body.Size.Y * layer.Inset) : cur;
                    if (gb.Size.X > 2f && gb.Size.Y > 2f)
                        KitGrain.Draw(ci, genre, Poly(shape, gb, g), gb, face, layer.Amount);
                    continue;
                }
                if (layer.Kind != KitLayerKind.Plate && layer.Kind != KitLayerKind.Keyline)
                    continue;

                float inset = layer.Inset >= 0f ? body.Size.Y * layer.Inset : frame;
                Rect2 box = (layer.Kind == KitLayerKind.Plate && layer.Inset == 0f)
                    ? body : Inset(cur, inset);
                if (box.Size.X < 2f || box.Size.Y < 2f) continue;

                // Shade < 0 is the sentinel for "the theme decides this band's polarity".
                Color c = Tint(face, layer.Shade < 0f ? g.OutlineShade : layer.Shade);
                if (layer.Kind == KitLayerKind.Keyline)
                    Fill(ci, shape, box, g, new Color(0, 0, 0, 0), c with { A = layer.Amount },
                         Mathf.Max(1f, rimPx * 0.5f));
                else
                {
                    Fill(ci, shape, box, g, c, ink,
                         layer.Rim > 0f ? Mathf.Max(1f, rimPx * layer.Rim) : 0f);
                    cur = box;
                }
            }

            // The constructed frame LAST: in the references the edge run sits on top of the
            // surface it encloses, not under it.
            KitEdge.Draw(ci, g.EdgeRun, body, rimPx, Tint(face, g.OutlineShade), g.Shear, g.Wobble);
        }

        /// <summary>State as a SCULPT, not an alpha change — fading a control is the clearest
        /// tell that a UI is a themed form rather than a game.</summary>
        public static Color StateFace(Color s, KitState st)
        {
            float k = st switch
            {
                KitState.Hover => 1.12f,
                KitState.Pressed => 0.84f,
                KitState.Disabled => 0.88f,
                _ => 1f,
            };
            var c = new Color(Mathf.Min(1f, s.R * k), Mathf.Min(1f, s.G * k),
                              Mathf.Min(1f, s.B * k), s.A);
            if (st != KitState.Disabled) return c;
            // Disabled DRAINS SATURATION rather than dimming (the 7x settled rule).
            float l = UiSurface.Luminance(c);
            return new Color(Mathf.Lerp(c.R, l, 0.9f), Mathf.Lerp(c.G, l, 0.9f),
                             Mathf.Lerp(c.B, l, 0.9f), c.A);
        }

        public static Rect2 Inset(Rect2 r, float by)
            => new(r.Position + new Vector2(by, by), r.Size - new Vector2(by * 2f, by * 2f));

        /// <summary>
        /// The silhouette. <paramref name="unit"/> is the theme's base metric (its font size).
        ///
        /// Pass it. Without it the corner falls back to `min(w,h) * Corner`, which is the
        /// SIZE-PROPORTIONAL rule Stage 50 removed from KitControl -- leaving KitChrome on the old
        /// one meant a button (drawn through here) and a panel (drawn through KitControl) resolved
        /// DIFFERENT radii from the same theme. The fallback is kept only for callers that have no
        /// Control to measure.
        /// </summary>
        public static Vector2[] Poly(KitShape shape, Rect2 r, KitGeometry g, float unit = 0f)
        {
            float corner = unit > 0f
                ? Mathf.Min(unit * g.Corner * 3.0f, Mathf.Min(r.Size.X, r.Size.Y) * 0.5f)
                : Mathf.Min(r.Size.X, r.Size.Y) * g.Corner;
            return KitControl.OutlinePoly(shape, r, corner, g.Shear, g.Wobble, unit);
        }

        // ── The static surface a Godot-derived kit widget needs ──────────────────────────────
        //
        // KitControl gives its subclasses Geo/FaceColor/DrawShape/... as instance members. A
        // widget that derives from Button, HSlider, CheckButton or ProgressBar instead -- which is
        // what every widget with a real Godot equivalent should do -- cannot inherit those. These
        // are the same operations, taking the Control explicitly, so both families draw through
        // ONE implementation rather than drifting apart.

        /// <summary>The active genre, or "" when no skin is applied.</summary>
        public static string GenreOf(Godot.Control _)
            => SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";

        /// <summary>The theme's base metric in px — everything decorative is a multiple of it.</summary>
        public static float Unit(Godot.Control ctl) => Mathf.Max(8f, UiSurface.FontSize(ctl));

        /// <summary>The genre's silhouette.</summary>
        public static KitShape Shape(string genre) => KitMaterial.ShapeForGenre(genre);

        /// <summary>Fill a shape inside <paramref name="r"/>, unit-aware.</summary>
        public static void DrawShape(Godot.Control ctl, string genre, Rect2 r, KitShape shape,
                                     Color fill, Color rim, float rimWidth)
        {
            var g = KitGeometry.ForGenre(genre);
            if (r.Size.X < 1f || r.Size.Y < 1f) return;
            var poly = Poly(shape, r, g, Unit(ctl));
            if (poly.Length < 3 || Geometry2D.TriangulatePolygon(poly).Length == 0) return;
            if (fill.A > 0f) ctl.DrawColoredPolygon(poly, fill);
            if (rimWidth > 0f && rim.A > 0f)
            {
                var closed = new Vector2[poly.Length + 1];
                poly.CopyTo(closed, 0);
                closed[^1] = poly[0];
                ctl.DrawPolyline(closed, rim, rimWidth);
            }
        }

        /// <summary>The rim's POLARITY is a genre tell: above 1 a bright carved rim, below 1 the
        /// thick dark outline of the casual family.</summary>
        public static Color Rim(Color face, KitGeometry g) => Tint(face, g.OutlineShade);

        /// <summary>The genre's type family, falling back to the theme default.</summary>
        public static Font? Font(Godot.Control ctl, string genre)
            => KitFonts.Resolve(KitGeometry.ForGenre(genre).Font) ?? ctl.GetThemeDefaultFont();

        /// <summary>
        /// The header plaque that STRADDLES the host's top edge — the single most repeated
        /// construction in the reference folder (15 of 59 files).
        ///
        /// Static, because it is now drawn by both families: KitControl subclasses and the
        /// widgets that derive from a real Godot type. Three private copies of it were already
        /// starting to appear, which is exactly how they drift.
        /// </summary>
        public static void DrawBanner(Godot.Control ctl, string genre, Rect2 host, string text,
                                      KitShape shape, float heightRatio = 0.14f,
                                      float widthRatio = 0.62f, float shade = 0.44f)
        {
            if (string.IsNullOrEmpty(text) || host.Size.X < 8f || host.Size.Y < 8f) return;

            var g = KitGeometry.ForGenre(genre);
            var font = Font(ctl, genre);
            // A banner is a SUBTITLE, not body text: it names the panel it straddles.
            int fs = UiSurface.FontSize(ctl, UiSurface.TextRole.Subtitle);

            // Floor the height at the type, or the banner clips its own text on a short host.
            float h = Mathf.Max(fs * 1.5f, host.Size.Y * heightRatio);
            float w = host.Size.X * widthRatio;
            if (font != null)
            {
                float need = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X + fs * 2f;
                w = Mathf.Max(w, Mathf.Min(need, host.Size.X * 1.08f));
            }

            // Centred ON the edge, so half the plate sits outside the host. This is the move
            // containers cannot express and the reason it is drawn rather than parented.
            var r = new Rect2(host.Position.X + (host.Size.X - w) * 0.5f,
                              host.Position.Y - h * 0.5f, w, h);

            Color face = UiSurface.Of(ctl);
            Color plate = new(face.R * shade, face.G * shade, face.B * shade, 1f);
            DrawShape(ctl, genre, r, shape, plate, UiSurface.Ink(face),
                      Mathf.Max(1f, g.Rim * 0.7f * (fs / 14f)));

            if (font == null) return;
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            Color ink = UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f, 1f)
                : new Color(0.98f, 0.96f, 0.92f, 1f);
            ctl.DrawString(font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                             r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                           text, HorizontalAlignment.Left, -1, fs, ink);
        }

        /// <summary>Apply the genre's case rule before drawing a string.</summary>
        public static string Case(string t, string genre)
            => KitGeometry.ForGenre(genre).UpperCase ? t.ToUpperInvariant() : t;

        /// <summary>Draw sub-elements that may overhang the host, from the same KitAttach resolve
        /// KitControl uses — so an overhanging badge looks identical on both families.</summary>
        public static void DrawAttachments(Godot.Control ctl, string genre,
                                           System.Collections.Generic.IEnumerable<KitAttach> list)
        {
            var g = KitGeometry.ForGenre(genre);
            foreach (var a in list)
            {
                Rect2 r = a.Resolve(ctl.Size);
                Color fill = UiSurface.Semantic(ctl, a.Role);
                if (fill.A < 0.02f) fill = UiSurface.Of(ctl);
                DrawShape(ctl, genre, r, a.Shape, fill, UiSurface.Ink(fill),
                          Mathf.Max(1f, g.Rim * 0.5f));
                if (a.Icon != null)
                    ctl.DrawTextureRect(a.Icon, KitChrome.Inset(r, r.Size.Y * 0.18f), false);
                if (string.IsNullOrEmpty(a.Text)) continue;
                var font = Font(ctl, genre);
                if (font == null) continue;
                int fs = UiSurface.FontSize(ctl, 0.8f);
                Vector2 m = font.GetStringSize(a.Text, HorizontalAlignment.Left, -1, fs);
                ctl.DrawString(font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                                 r.Position.Y + (r.Size.Y + m.Y * 0.6f) * 0.5f),
                               a.Text, HorizontalAlignment.Left, -1, fs, UiSurface.Ink(fill));
            }
        }

        /// <summary>Shade may exceed 1.0 — the measured outer rim is 2.05× the plate — so
        /// brightening lifts toward white rather than clipping each channel, which would shift
        /// hue as it saturated.</summary>
        public static Color Tint(Color face, float shade)
        {
            if (shade <= 1f)
                return new Color(face.R * shade, face.G * shade, face.B * shade, face.A);
            float lum = UiSurface.Luminance(face);
            float want = Mathf.Min(1f, lum * shade);
            float t = Mathf.Clamp((want - lum) / Mathf.Max(0.001f, 1f - lum), 0f, 1f);
            return new Color(Mathf.Lerp(face.R, 1f, t), Mathf.Lerp(face.G, 1f, t),
                             Mathf.Lerp(face.B, 1f, t), face.A);
        }

        /// <summary>Fill and rim a shape. Always via a polygon, so the silhouette work applies to
        /// the drop-ins too rather than only to KitControl widgets.</summary>
        public static void Fill(CanvasItem ci, KitShape shape, Rect2 r, KitGeometry g,
                                Color fill, Color rim, float rimWidth)
        {
            if (r.Size.X < 1f || r.Size.Y < 1f) return;
            var poly = Poly(shape, r, g);
            if (poly.Length < 3) return;
            if (fill.A > 0f) ci.DrawColoredPolygon(poly, fill);
            if (rimWidth > 0f)
            {
                var closed = new Vector2[poly.Length + 1];
                poly.CopyTo(closed, 0);
                closed[^1] = poly[0];
                ci.DrawPolyline(closed, rim, rimWidth);
            }
        }

        /// <summary>Centred, multi-line aware label. Several template controls carry two lines,
        /// and drawing only the first would silently lose half of every one of them.</summary>
        public static void DrawLabel(CanvasItem ci, Godot.Control ctl, string text, Rect2 box,
                                     Color col, float dy = 0f,
                                     HorizontalAlignment align = HorizontalAlignment.Center)
        {
            if (string.IsNullOrEmpty(text)) return;

            // The GENRE's family, falling back to the theme default. KitFonts warns when a
            // declared role has no shipped face, because that failure renders identically to
            // having no font system at all.
            var g = KitGeometry.ForGenre(SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "");
            var font = KitFonts.Resolve(g.Font) ?? ctl.GetThemeDefaultFont();
            if (font == null) return;
            if (g.UpperCase) text = text.ToUpperInvariant();
            int fs = UiSurface.FontSize(ctl);
            string[] lines = text.Split('\n');
            float lh = fs * 1.15f;
            float top = box.Position.Y + (box.Size.Y - lh * lines.Length) * 0.5f + fs * 0.82f + dy;
            for (int i = 0; i < lines.Length; i++)
            {
                Vector2 m = font.GetStringSize(lines[i], HorizontalAlignment.Left, -1, fs);
                float x = align switch
                {
                    HorizontalAlignment.Left => box.Position.X,
                    HorizontalAlignment.Right => box.Position.X + box.Size.X - m.X,
                    _ => box.Position.X + (box.Size.X - m.X) * 0.5f,
                };
                if (g.Tracking > 0.001f)
                {
                    // Godot's DrawString has no letter-spacing, so tracked text is drawn glyph by
                    // glyph. Only on the themes that ask for it -- the per-glyph path is slower
                    // and would be waste on the eight genres that do not.
                    float gx = x;
                    foreach (char ch in lines[i])
                    {
                        string one = ch.ToString();
                        ci.DrawString(font, new Vector2(gx, top + lh * i), one,
                                      HorizontalAlignment.Left, -1, fs, col);
                        gx += font.GetStringSize(one, HorizontalAlignment.Left, -1, fs).X
                            + fs * g.Tracking;
                    }
                }
                else
                    ci.DrawString(font, new Vector2(x, top + lh * i), lines[i],
                                  HorizontalAlignment.Left, -1, fs, col);
            }
        }
    }
}
