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

                Color c = Tint(face, layer.Shade);
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

        public static Vector2[] Poly(KitShape shape, Rect2 r, KitGeometry g)
            => KitControl.OutlinePoly(shape, r, Mathf.Min(r.Size.X, r.Size.Y) * g.Corner);

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
            var font = ctl.GetThemeDefaultFont();
            if (font == null) return;
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
                ci.DrawString(font, new Vector2(x, top + lh * i), lines[i],
                              HorizontalAlignment.Left, -1, fs, col);
            }
        }
    }
}
