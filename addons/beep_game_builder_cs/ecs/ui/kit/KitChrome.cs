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
        private const string AutoMinimumMeta = "_beep_kit_auto_minimum";
        private const string ThemeWriteMetaPrefix = "_beep_kit_theme_write_";
        public const string GenreMeta = "_beep_kit_genre";

        public static bool SetAutoMinimumSize(Godot.Control ctl, Vector2 wanted)
        {
            wanted = new Vector2(Mathf.Max(0f, wanted.X), Mathf.Max(0f, wanted.Y));
            bool kitOwnsMinimum = ctl.CustomMinimumSize == Vector2.Zero;
            if (!kitOwnsMinimum && ctl.HasMeta(AutoMinimumMeta))
            {
                Vector2 previous = ctl.GetMeta(AutoMinimumMeta, Vector2.Zero).AsVector2();
                kitOwnsMinimum = Same(ctl.CustomMinimumSize.X, previous.X)
                              && Same(ctl.CustomMinimumSize.Y, previous.Y);
            }

            if (!kitOwnsMinimum) return false;
            if (SameVector(ctl.CustomMinimumSize, wanted)
                && ctl.HasMeta(AutoMinimumMeta)
                && SameVector(ctl.GetMeta(AutoMinimumMeta, Vector2.Zero).AsVector2(), wanted))
                return false;

            ctl.CustomMinimumSize = wanted;
            ctl.SetMeta(AutoMinimumMeta, wanted);
            return true;
        }

        /// <summary>
        /// Re-measure and repaint after a change that alters a widget's natural size.
        ///
        /// One body, because 27 widgets had each written their own and they did not agree: five
        /// guarded the resize with <c>IsInsideTree()</c> and 22 called <c>UpdateMinimumSize()</c>
        /// regardless. The guard is correct — a widget takes property changes before it enters the
        /// tree — so this is the guarded form. <see cref="KitControl"/> exposes it as a protected
        /// method; the widgets that derive from a native Godot type call this directly, since C#
        /// gives them only one base and it has to be the real control.
        /// </summary>
        public static void RefreshMinimumAndRedraw(Godot.Control ctl, Vector2 minimum)
        {
            if (ctl.IsInsideTree())
            {
                RefreshAutoMinimumSize(ctl, minimum);
                ctl.UpdateMinimumSize();
            }
            ctl.QueueRedraw();
        }

        public static void RefreshAutoMinimumSize(Godot.Control ctl, Vector2 wanted)
        {
            if (SetAutoMinimumSize(ctl, wanted))
                ctl.UpdateMinimumSize();
        }

        public static void RefreshAutoMinimumSize(Godot.Control ctl, Vector2 wanted, bool force)
        {
            if (force && ctl.HasMeta(AutoMinimumMeta))
            {
                Vector2 previous = ctl.GetMeta(AutoMinimumMeta, Vector2.Zero).AsVector2();
                bool kitOwnsMinimum = ctl.CustomMinimumSize == Vector2.Zero
                                   || (Same(ctl.CustomMinimumSize.X, previous.X)
                                    && Same(ctl.CustomMinimumSize.Y, previous.Y));
                if (kitOwnsMinimum)
                    ctl.CustomMinimumSize = Vector2.Zero;
            }

            RefreshAutoMinimumSize(ctl, wanted);
        }

        /// <summary>Blank a control's StyleBoxes so the base class paints nothing, KEEPING the
        /// content margins — Godot sizes a control's text and children from them, so zeroing them
        /// collapses the widget onto its label.</summary>
        public static void Suppress(Godot.Control ctl, string[] states, float frame, float pad,
                                    float vpad = -1f)
        {
            if (vpad < 0f) vpad = frame * 0.5f + pad * 0.4f;
            foreach (string s in states)
                SetEmptyStyleboxOverride(ctl, s, frame + pad, frame + pad, vpad, vpad);
        }

        public static bool SetEmptyStyleboxOverride(Godot.Control ctl, string name)
            => SetEmptyStyleboxOverride(ctl, name, 0f, 0f, 0f, 0f);

        public static bool SetEmptyStyleboxOverride(
            Godot.Control ctl,
            string name,
            float left,
            float right,
            float top,
            float bottom)
        {
            if (ctl.HasThemeStyleboxOverride(name)
                && ctl.GetThemeStylebox(name) is StyleBoxEmpty existing
                && SameMargins(existing, left, right, top, bottom))
                return false;

            if (!BeginThemeOverrideWrite(ctl, "stylebox", name)) return false;
            try
            {
                ctl.AddThemeStyleboxOverride(name, new StyleBoxEmpty
                {
                    ContentMarginLeft = left,
                    ContentMarginRight = right,
                    ContentMarginTop = top,
                    ContentMarginBottom = bottom,
                });
            }
            finally
            {
                EndThemeOverrideWrite(ctl, "stylebox", name);
            }
            return true;
        }

        public static bool SetStyleboxOverrideIfChanged(Godot.Control ctl, string name, StyleBox value)
        {
            if (ctl.HasThemeStyleboxOverride(name) && SameStylebox(ctl.GetThemeStylebox(name), value))
                return false;

            if (!BeginThemeOverrideWrite(ctl, "stylebox", name)) return false;
            try
            {
                ctl.AddThemeStyleboxOverride(name, value);
            }
            finally
            {
                EndThemeOverrideWrite(ctl, "stylebox", name);
            }
            return true;
        }

        public static bool SetBlankIconOverride(Godot.Control ctl, string name)
        {
            if (ctl.HasThemeIconOverride(name) && ctl.GetThemeIcon(name) == Blank)
                return false;

            if (!BeginThemeOverrideWrite(ctl, "icon", name)) return false;
            try
            {
                ctl.AddThemeIconOverride(name, Blank);
            }
            finally
            {
                EndThemeOverrideWrite(ctl, "icon", name);
            }
            return true;
        }

        public static bool SetColorOverrideIfChanged(Godot.Control ctl, string name, Color value)
        {
            if (ctl.HasThemeColorOverride(name) && SameColor(ctl.GetThemeColor(name), value)) return false;
            if (!BeginThemeOverrideWrite(ctl, "color", name)) return false;
            try
            {
                ctl.AddThemeColorOverride(name, value);
            }
            finally
            {
                EndThemeOverrideWrite(ctl, "color", name);
            }
            return true;
        }

        public static bool RemoveColorOverrideIfPresent(Godot.Control ctl, string name)
        {
            if (!ctl.HasThemeColorOverride(name)) return false;
            if (!BeginThemeOverrideWrite(ctl, "color", name)) return false;
            try
            {
                ctl.RemoveThemeColorOverride(name);
            }
            finally
            {
                EndThemeOverrideWrite(ctl, "color", name);
            }
            return true;
        }

        public static bool SetFontSizeOverrideIfChanged(Godot.Control ctl, string name, int value)
        {
            if (ctl.HasThemeFontSizeOverride(name) && ctl.GetThemeFontSize(name) == value) return false;
            if (!BeginThemeOverrideWrite(ctl, "font_size", name)) return false;
            try
            {
                ctl.AddThemeFontSizeOverride(name, value);
            }
            finally
            {
                EndThemeOverrideWrite(ctl, "font_size", name);
            }
            return true;
        }

        public static bool SetConstantOverrideIfChanged(Godot.Control ctl, string name, int value)
        {
            if (ctl.HasThemeConstantOverride(name) && ctl.GetThemeConstant(name) == value) return false;
            if (!BeginThemeOverrideWrite(ctl, "constant", name)) return false;
            try
            {
                ctl.AddThemeConstantOverride(name, value);
            }
            finally
            {
                EndThemeOverrideWrite(ctl, "constant", name);
            }
            return true;
        }

        public static bool SetFontOverrideIfChanged(Godot.Control ctl, string name, Font value)
        {
            if (ctl.HasThemeFontOverride(name) && ctl.GetThemeFont(name) == value) return false;
            if (!BeginThemeOverrideWrite(ctl, "font", name)) return false;
            try
            {
                ctl.AddThemeFontOverride(name, value);
            }
            finally
            {
                EndThemeOverrideWrite(ctl, "font", name);
            }
            return true;
        }

        /// <summary>How far apart two planes must sit before a player can see that one is cut into
        /// the other. Below this they read as one flat shape.</summary>
        private const float MinPlaneSeparation = 0.045f;

        /// <summary>
        /// The colour of a recess — a panel's well, a slot's interior, a slider's track — cut into
        /// a plate of <paramref name="surface"/>.
        ///
        /// Multiplying by a shade under 1 is right on a mid or light skin and does nothing at all
        /// on a dark one. `oilfield_days` has a #121A1B surface; at the 0.79 well shade that lands
        /// on #0E1415, a separation of about two levels out of 255. That is why, on a dark skin,
        /// every panel well and every empty inventory slot rendered as one black void — the recess
        /// was being drawn, it just could not be seen.
        ///
        /// So: multiply when that produces a visible step, and otherwise fall back to
        /// <see cref="WellFace"/>, which lifts a dark surface instead. Separating the two planes is
        /// the point; which direction achieves it is whichever one the skin leaves room for.
        /// </summary>
        /// <summary>
        /// The colour of a panel's TITLE BAR, sitting on a plate of <paramref name="surface"/>.
        ///
        /// A header is the opposite of a well and was being drawn as one: `Tint(face, 0.48)` is the
        /// panel face multiplied DOWN, and on a dark skin that lands near black. Rendered, the
        /// EQUIPMENT and INVENTORY bars read as a gap cut across the top of the panel rather than
        /// as a title bar — a hole where the panel's own name should be.
        ///
        /// A title bar is a RAISED plane: it carries the panel's name, so it should read as sitting
        /// on top of the body, not sunk into it. This lifts when the skin has headroom and falls
        /// back to darkening when it does not, which is the same "separate the two planes, and let
        /// the skin decide which direction" rule <see cref="RecessFace"/> follows in reverse.
        /// </summary>
        /// <param name="shade">The theme's authored header shade. It used to be a multiplier
        /// straight onto the face, which is what produced the near-black bar; it now sets HOW FAR
        /// the header separates from the body, so a theme that asked for a strongly distinct
        /// header still gets one and a theme that asked for a subtle one still gets that. The
        /// authored value keeps meaning something rather than becoming a key nothing reads.</param>
        public static Color HeaderFace(Color surface, float shade = 0.48f)
        {
            if (surface.A <= 0.02f) return surface;

            float lum = UiSurface.Luminance(surface);
            float strength = Mathf.Clamp(1f - shade, 0.15f, 1f);

            // Lift toward white, keeping the hue, by enough to clear the separation floor.
            float want = lum + MinPlaneSeparation * 4.2f * strength;
            if (want <= 1f)
            {
                float t = Mathf.Clamp((want - lum) / Mathf.Max(0.001f, 1f - lum), 0f, 1f);
                return new Color(Mathf.Lerp(surface.R, 1f, t), Mathf.Lerp(surface.G, 1f, t),
                                 Mathf.Lerp(surface.B, 1f, t), surface.A);
            }

            // Already near white: the only way to separate is down.
            float k = 1f - MinPlaneSeparation * 3f * strength;
            return new Color(surface.R * k, surface.G * k, surface.B * k, surface.A);
        }

        public static Color RecessFace(Color surface, float shade)
        {
            if (surface.A <= 0.02f) return surface;

            var darker = new Color(surface.R * shade, surface.G * shade, surface.B * shade, surface.A);
            if (UiSurface.Luminance(surface) - UiSurface.Luminance(darker) >= MinPlaneSeparation)
                return Floor(darker);

            return WellFace(surface);
        }

        /// <summary>
        /// Keep a recess a SURFACE rather than a hole punched through to black.
        ///
        /// The deep readout shade is 0.12, and on a dark skin that lands at effectively zero: the
        /// gem socket rendered as a featureless black disc, and a slot grid as a row of them. The
        /// shade was doing exactly what it says; the result was just unreadable. Scaling the
        /// channels back up to a floor keeps the hue exactly — it is the same colour, turned up —
        /// so a socket still reads as cut deeper than a content well without going out entirely.
        /// </summary>
        private static Color Floor(Color c)
        {
            const float minRecessLuminance = 0.055f;
            float lum = UiSurface.Luminance(c);
            if (lum >= minRecessLuminance || lum <= 0.0005f) return c;

            float k = minRecessLuminance / lum;
            return new Color(Mathf.Min(1f, c.R * k), Mathf.Min(1f, c.G * k), Mathf.Min(1f, c.B * k), c.A);
        }

        public static Color WellFace(Color surface)
        {
            if (surface.A <= 0.02f) return surface;

            float lum = UiSurface.Luminance(surface);
            if (lum < 0.20f)
            {
                float t = Mathf.Clamp((0.20f - lum) / Mathf.Max(0.001f, 1f - lum), 0f, 0.28f);
                return new Color(Mathf.Lerp(surface.R, 1f, t),
                                 Mathf.Lerp(surface.G, 1f, t),
                                 Mathf.Lerp(surface.B, 1f, t),
                                 1f);
            }

            return new Color(surface.R * 0.42f, surface.G * 0.40f, surface.B * 0.46f, 1f);
        }

        public static void ApplyInputDefaults(
            Godot.Control ctl,
            bool autoInputDefaults,
            Godot.Control.MouseFilterEnum? mouseFilter = null,
            Godot.Control.FocusModeEnum? focusMode = null)
        {
            if (!autoInputDefaults) return;
            if (mouseFilter.HasValue && ctl.MouseFilter != mouseFilter.Value)
                ctl.MouseFilter = mouseFilter.Value;
            if (focusMode.HasValue && ctl.FocusMode != focusMode.Value)
                ctl.FocusMode = focusMode.Value;
        }

        private static bool Same(float a, float b) => Mathf.Abs(a - b) < 0.001f;

        private static StringName ThemeWriteMeta(string kind, string name)
            => new($"{ThemeWriteMetaPrefix}{kind}_{name}");

        private static bool BeginThemeOverrideWrite(Godot.Control ctl, string kind, string name)
        {
            StringName meta = ThemeWriteMeta(kind, name);
            if (ctl.HasMeta(meta)) return false;
            ctl.SetMeta(meta, true);
            return true;
        }

        private static void EndThemeOverrideWrite(Godot.Control ctl, string kind, string name)
        {
            StringName meta = ThemeWriteMeta(kind, name);
            if (ctl.HasMeta(meta))
                ctl.RemoveMeta(meta);
        }

        private static bool SameVector(Vector2 a, Vector2 b)
            => Same(a.X, b.X) && Same(a.Y, b.Y);

        private static bool SameColor(Color a, Color b)
            => Same(a.R, b.R) && Same(a.G, b.G) && Same(a.B, b.B) && Same(a.A, b.A);

        private static bool SameStylebox(StyleBox? a, StyleBox b)
        {
            if (a is StyleBoxEmpty ea && b is StyleBoxEmpty eb)
                return SameMargins(ea, eb);

            if (a is StyleBoxFlat fa && b is StyleBoxFlat fb)
            {
                return SameColor(fa.BgColor, fb.BgColor)
                    && SameColor(fa.BorderColor, fb.BorderColor)
                    && SameMargins(fa, fb)
                    && Same(fa.BorderWidthLeft, fb.BorderWidthLeft)
                    && Same(fa.BorderWidthRight, fb.BorderWidthRight)
                    && Same(fa.BorderWidthTop, fb.BorderWidthTop)
                    && Same(fa.BorderWidthBottom, fb.BorderWidthBottom)
                    && Same(fa.CornerRadiusTopLeft, fb.CornerRadiusTopLeft)
                    && Same(fa.CornerRadiusTopRight, fb.CornerRadiusTopRight)
                    && Same(fa.CornerRadiusBottomLeft, fb.CornerRadiusBottomLeft)
                    && Same(fa.CornerRadiusBottomRight, fb.CornerRadiusBottomRight);
            }

            return false;
        }

        private static bool SameMargins(StyleBox a, StyleBox b)
            => Same(a.ContentMarginLeft, b.ContentMarginLeft)
            && Same(a.ContentMarginRight, b.ContentMarginRight)
            && Same(a.ContentMarginTop, b.ContentMarginTop)
            && Same(a.ContentMarginBottom, b.ContentMarginBottom);

        private static bool SameMargins(StyleBox a, float left, float right, float top, float bottom)
            => Same(a.ContentMarginLeft, left)
            && Same(a.ContentMarginRight, right)
            && Same(a.ContentMarginTop, top)
            && Same(a.ContentMarginBottom, bottom);

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
        /// Draw the genre's plate into <paramref name="body"/>: the register's band stack and rim.
        /// Everything a kit widget's face is made of, minus text.
        /// </summary>
        public static void DrawPlate(CanvasItem ci, string genre, Rect2 body, Color face,
                                     KitState state, float rimScale = 1f,
                                     KitWidgetClass widgetClass = KitWidgetClass.Button)
        {
            if (body.Size.X < 3f || body.Size.Y < 3f) return;

            var g = KitGeometry.ForGenre(genre);
            KitShape shape = KitMaterial.WidgetShapeForGenre(genre, widgetClass);

            // THE PIXEL REGISTER'S STAIRCASE. This rule lived only in KitControl.DrawMaterial, so
            // the moment KitPushButton became a Godot Button and started drawing through here, every
            // pixel theme went back to arcs -- measured 0.76 mobility where a staircase is < 0.40.
            // Third time this rule has escaped a draw path; it belongs wherever a silhouette is
            // decided, and both paths now decide it here or in the matching block over there.
            float unitPx = Mathf.Max(8f, 14f * rimScale);
            float cornerFraction = g.CornerFor(widgetClass);
            float cornerPx = Mathf.Min(unitPx * cornerFraction * 3.0f,
                                       Mathf.Min(body.Size.X, body.Size.Y) * 0.5f);
            if (g.Register == KitRegister.Pixel && cornerPx >= Mathf.Max(1f, g.PixelSize)
                && shape is KitShape.Round or KitShape.Pill or KitShape.Ellipse or KitShape.Arch
                    or KitShape.Capsule)
                shape = KitShape.Stepped;
            Color ink = UiSurface.Ink(face);
            float rimPx = Mathf.Max(1f, g.Rim * rimScale);
            float frame = g.FramePx(body.Size.Y);

            // ARTWORK, when the genre declares a sprite set. The band stack below is arithmetic
            // over the surface colour, and on a dark palette it has almost nowhere to go; a
            // nine-slice carries its gloss, gradient and raised lip in the pixels and takes the
            // palette as a multiply. See KitSprite for why the art is neutral, which widget
            // classes it covers, and why a genre whose identity is its silhouette should not
            // take it.
            //
            // The shadow still comes from the THEME rather than from the art: a lip says the plate
            // is raised off its own base, a shadow says it is raised off the screen behind it, and
            // those are different statements. It follows the pressed rect so a sunken control does
            // not keep floating.
            if (KitSprite.TryPlate(ci, g, widgetClass, state, shape, body, face, out KitSprite.Plate art))
            {
                KitShadow.Draw(ci, g.Shadow, Poly(shape, art.Rect, g, unitPx, widgetClass),
                               art.Rect, KitShadow.UnitFor(art.Rect), face);
                KitSprite.Draw(ci, art);
                KitEdge.Draw(ci, g.EdgeRun, body, rimPx, Tint(face, g.OutlineShade), g.Shear, g.Wobble);
                return;
            }

            // SHADOW FIRST, under the whole stack. It is not in the register's layer list on
            // purpose: the register says how a plate is BUILT, the theme says how it is
            // SEPARATED from its ground, and two themes of one genre differ by the second more
            // than the first.
            KitShadow.Draw(ci, g.Shadow, Poly(shape, body, g, unitPx, widgetClass), body, KitShadow.UnitFor(body), face);

            Rect2 cur = body;

            foreach (var layer in KitStacks.For(g.Register))
            {
                // Shade / Bevel / Gloss were SKIPPED here, so every widget that derives from a
                // Godot type (Button, CheckButton, HSlider, ProgressBar, Panel, TabBar) lost its
                // face shading, its bevel and its gloss entirely -- the gloss gate went from three
                // distinguishable constructions to three identical renders and said so.
                // KitControl's stack draws them; this one has to as well, or "which base class a
                // widget happens to have" silently changes how it is lit.
                if (layer.Kind is KitLayerKind.Shade or KitLayerKind.Bevel or KitLayerKind.Gloss)
                {
                    var lit = Poly(shape, cur, g, unitPx, widgetClass);
                    if (lit.Length >= 3) DrawLighting(ci, layer, lit, cur, g, face, unitPx);
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
                         Mathf.Max(1f, rimPx * 0.5f), unitPx, widgetClass);
                else
                {
                    Fill(ci, shape, box, g, c, ink,
                         layer.Rim > 0f ? Mathf.Max(1f, rimPx * layer.Rim) : 0f,
                         unitPx, widgetClass);
                    cur = box;
                }
            }

            // The constructed frame LAST: in the references the edge run sits on top of the
            // surface it encloses, not under it.
            KitEdge.Draw(ci, g.EdgeRun, body, rimPx, Tint(face, g.OutlineShade), g.Shear, g.Wobble);
        }

        /// <summary>
        /// The lighting layers — face shade, bevel, gloss — clipped to the plate's own silhouette.
        ///
        /// Deliberately the same constructions KitControl draws, because the alternative is that a
        /// widget's LIGHTING depends on which base class it happens to derive from. That is
        /// exactly what happened when the kit widgets moved onto Button/HSlider/ProgressBar: they
        /// kept their plate and lost their shading, and only the gloss gate noticed.
        /// </summary>
        private static void DrawLighting(CanvasItem ci, KitLayer layer, Vector2[] poly, Rect2 box,
                                         KitGeometry g, Color face, float unit)
        {
            if (layer.Amount <= 0f || box.Size.Y < 4f) return;

            if (layer.Kind == KitLayerKind.Shade)
            {
                // Vertical falloff: darkest at the bottom, the top left as the peak.
                const int bands = 7;
                float bh = box.Size.Y / bands;
                for (int i = 0; i < bands; i++)
                {
                    float t = (i + 1) / (float)bands;
                    float y = box.Position.Y + bh * i;
                    ClipInto(ci, poly, Band(box, y, y + bh + 1f),
                             new Color(0, 0, 0, layer.Amount * 0.42f * t * t));
                }
                return;
            }

            if (layer.Kind == KitLayerKind.Gloss)
            {
                if (g.Gloss <= 0f) return;
                float h = Mathf.Min(unit * 1.6f, box.Size.Y * 0.45f);
                float a = g.GlossStyle == KitGloss.Linear
                    ? Mathf.Clamp(0.16f * g.Gloss * layer.Amount, 0f, 1f)
                    : Mathf.Clamp(Mathf.Max(0.13f, 0.30f * g.Gloss) * layer.Amount, 0f, 1f);
                if (a < 0.004f) return;

                if (g.GlossStyle == KitGloss.CurvedGlass)
                {
                    // Convex lower boundary, deepest at the centre.
                    const int steps = 24;
                    var pts = new System.Collections.Generic.List<Vector2>
                    {
                        new(box.Position.X - 4f, box.Position.Y - 4f),
                        new(box.Position.X + box.Size.X + 4f, box.Position.Y - 4f),
                    };
                    for (int i = steps; i >= 0; i--)
                    {
                        float t = i / (float)steps;
                        pts.Add(new Vector2(
                            Mathf.Lerp(box.Position.X - 4f, box.Position.X + box.Size.X + 4f, t),
                            box.Position.Y + h * (0.62f + 0.38f * Mathf.Sin(Mathf.Pi * t))));
                    }
                    ClipInto(ci, poly, pts.ToArray(), new Color(1, 1, 1, a));
                    return;
                }
                float top = g.GlossStyle == KitGloss.Linear ? box.Position.Y : box.Position.Y - 4f;
                ClipInto(ci, poly, Band(box, top, box.Position.Y + h), new Color(1, 1, 1, a));
                return;
            }

            // Bevel: light along the top-left edges, dark along the bottom-right.
            if (g.Bevel <= 0f) return;
            float w = Mathf.Max(1f, unit * 0.20f * g.Bevel);
            // The alphas are unchanged; only the HUE now comes from the theme, which declared
            // border_bevel_light and border_bevel_dark all along and had neither ever read.
            var (bevelLight, bevelDark) = UiSurface.Bevel(UiSurface.NearestControl(ci));
            Color hi = bevelLight with { A = 0.22f * g.Bevel * layer.Amount };
            Color lo = bevelDark with { A = 0.26f * g.Bevel * layer.Amount };
            bool allowDark = g.Register != KitRegister.Casual;
            Vector2 c = Vector2.Zero;
            foreach (var v in poly) c += v;
            c /= poly.Length;
            var key = new Vector2(-0.7071f, -0.7071f);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a0 = poly[i], b0 = poly[(i + 1) % poly.Length];
                float len = a0.DistanceTo(b0);
                if (len < 1.5f) continue;
                Vector2 d = (b0 - a0) / len;
                Vector2 n = new(-d.Y, d.X);
                if (n.Dot(a0 - c) < 0f) n = -n;
                bool bright = n.Dot(key) > 0f;
                if (!bright && !allowDark) continue;
                ci.DrawLine(a0, b0, bright ? hi : lo, w);
            }
        }

        private static Vector2[] Band(Rect2 r, float top, float bottom)
        {
            float l = r.Position.X - 4f, rt = r.Position.X + r.Size.X + 4f;
            return new[] { new Vector2(l, top), new Vector2(rt, top),
                           new Vector2(rt, bottom), new Vector2(l, bottom) };
        }

        private static void ClipInto(CanvasItem ci, Vector2[] host, Vector2[] band, Color c)
        {
            if (c.A < 0.003f || host.Length < 3 || band.Length < 3) return;
            foreach (var piece in Geometry2D.IntersectPolygons(host, band))
                if (piece.Length >= 3 && Geometry2D.TriangulatePolygon(piece).Length > 0)
                    ci.DrawColoredPolygon(piece, c);
        }

        /// <summary>State as a SCULPT, not an alpha change — fading a control is the clearest
        /// tell that a UI is a themed form rather than a game.</summary>
        public static Color StateFace(Color s, KitState st)
        {
            if (st != KitState.Disabled)
                s = UiSurface.ControlFace(s);

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
        public static Vector2[] Poly(KitShape shape, Rect2 r, KitGeometry g, float unit = 0f,
                                     KitWidgetClass widgetClass = KitWidgetClass.Button)
        {
            float cornerFraction = g.CornerFor(widgetClass);
            float corner = unit > 0f
                ? Mathf.Min(unit * cornerFraction * 3.0f, Mathf.Min(r.Size.X, r.Size.Y) * 0.5f)
                : Mathf.Min(r.Size.X, r.Size.Y) * cornerFraction;
            return KitControl.OutlinePoly(shape, r, corner, g.Shear, g.Wobble, unit);
        }

        // ── The static surface a Godot-derived kit widget needs ──────────────────────────────
        //
        // KitControl gives its subclasses Geo/FaceColor/DrawShape/... as instance members. A
        // widget that derives from Button, HSlider, CheckButton or ProgressBar instead -- which is
        // what every widget with a real Godot equivalent should do -- cannot inherit those. These
        // are the same operations, taking the Control explicitly, so both families draw through
        // ONE implementation rather than drifting apart.

        /// <summary>The genre that themed this control, falling back to the active game skin.</summary>
        public static string GenreOf(Godot.Control ctl)
        {
            for (Node? n = ctl; n != null; n = n.GetParent())
            {
                if (n is not Godot.Control c || !c.HasMeta(GenreMeta)) continue;
                string genre = c.GetMeta(GenreMeta, "").AsString();
                if (!string.IsNullOrWhiteSpace(genre)) return genre;
            }

            return SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
        }

        /// <summary>The theme's base metric in px — everything decorative is a multiple of it.</summary>
        public static float Unit(Godot.Control ctl) => Mathf.Max(8f, UiSurface.FontSize(ctl));

        /// <summary>The genre's silhouette.</summary>
        public static KitShape Shape(string genre, KitWidgetClass widgetClass = KitWidgetClass.Button)
            => KitMaterial.WidgetShapeForGenre(genre, widgetClass);

        /// <summary>
        /// True when this event is the player's "activate it" input.
        ///
        /// Reads Godot's built-in <c>ui_accept</c> action rather than testing key codes, so one
        /// press works from Enter, Space, a gamepad's A/Cross button, and whatever the player has
        /// remapped. This used to switch on <c>Key.Enter/KpEnter/Space</c> — key codes a controller
        /// can never produce — so every custom-drawn widget in the kit was unreachable by gamepad
        /// while the gameplay components beside them were already action-driven, and
        /// <c>BeepInputMapGenerator</c> had bound a pad button to this very action.
        /// </summary>
        public static bool IsConfirm(InputEvent @event) => @event.IsActionPressed("ui_accept");

        /// <summary>True when this event is the player's "back out" input: <c>ui_cancel</c>, which
        /// is Escape and a gamepad's B/Circle.</summary>
        public static bool IsCancel(InputEvent @event) => @event.IsActionPressed("ui_cancel");

        public static void HookButtonChromeRedraw(BaseButton button, System.Action redraw, ref bool hooked)
        {
            if (hooked) return;

            button.MouseEntered += redraw;
            button.MouseExited += redraw;
            button.FocusEntered += redraw;
            button.FocusExited += redraw;
            button.ButtonDown += redraw;
            button.ButtonUp += redraw;
            button.Pressed += redraw;
            hooked = true;
        }

        public static bool ActivateOnClickOrConfirm(
            Godot.Control ctl,
            InputEvent @event,
            System.Action activate,
            bool interactive = true,
            bool selectFocusOnMouse = true)
        {
            if (!interactive) return false;

            if (IsConfirm(@event))
            {
                activate();
                ctl.AcceptEvent();
                return true;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                if (selectFocusOnMouse)
                    ctl.GrabFocus();
                activate();
                ctl.AcceptEvent();
                return true;
            }

            return false;
        }

        /// <summary>
        /// The direction this event asks for, or zero. <see cref="Jump"/> on an axis means
        /// "all the way to that end" — the Home/End contract every caller's MoveSelection expects.
        ///
        /// Built on Godot's own directional actions, which ship with D-pad bindings, so a
        /// controller drives a slot grid exactly as the arrow keys do. WASD stays as a
        /// keyboard-only extra rather than being bound into the project's <c>ui_*</c> actions,
        /// because binding it there would make walking move the UI selection too.
        /// </summary>
        public static Vector2I DirectionOf(InputEvent @event)
        {
            if (@event.IsActionPressed("ui_home")) return new Vector2I(-Jump, 0);
            if (@event.IsActionPressed("ui_end")) return new Vector2I(Jump, 0);
            if (@event.IsActionPressed("ui_left")) return new Vector2I(-1, 0);
            if (@event.IsActionPressed("ui_right")) return new Vector2I(1, 0);
            if (@event.IsActionPressed("ui_up")) return new Vector2I(0, -1);
            if (@event.IsActionPressed("ui_down")) return new Vector2I(0, 1);

            if (@event is InputEventKey { Pressed: true, Echo: false } key)
                return key.Keycode switch
                {
                    Key.A => new Vector2I(-1, 0),
                    Key.D => new Vector2I(1, 0),
                    Key.W => new Vector2I(0, -1),
                    Key.S => new Vector2I(0, 1),
                    _ => Vector2I.Zero,
                };

            return Vector2I.Zero;
        }

        /// <summary>The "jump to that end" magnitude on a direction axis.</summary>
        public const int Jump = 9999;

        /// <summary>
        /// Move the selection, and consume the event ONLY if it actually moved.
        ///
        /// This is what keeps a widget from trapping the player. Every directional widget used to
        /// call AcceptEvent() for any arrow key, while its own MoveSelection clamped at the ends —
        /// so at the edge of a slot grid the key was eaten and the selection stayed put, and there
        /// was no way to leave the widget by arrow at all. Declining the event at the edge hands it
        /// back to Godot, whose focus traversal then carries the player to the next control. That
        /// matters far more once <see cref="DirectionOf"/> reads the D-pad, because on a controller
        /// the D-pad is the only way out.
        /// </summary>
        public static bool NavigateOrRelease(Godot.Control ctl, InputEvent @event,
                                             System.Func<Vector2I, bool> move)
        {
            Vector2I dir = DirectionOf(@event);
            if (dir == Vector2I.Zero) return false;
            if (!move(dir)) return false;
            ctl.AcceptEvent();
            return true;
        }

        /// <summary>The contrast a focus indicator must clear against the surface behind it.
        /// WCAG 2.2 SC 1.4.11; Xbox Accessibility Guideline 112 asks for the same.</summary>
        public const float MinFocusContrast = 3f;

        /// <summary>
        /// The colour of a focus ring on this control.
        ///
        /// The theme's own <c>border_focus</c> comes first, because that is the colour the
        /// generated Theme already stamps into every native control's focus StyleBox — reading it
        /// here is what stops a themed Button and a kit widget beside it from ringing in two
        /// different colours out of one theme.
        ///
        /// A role is only taken if it clears <see cref="MinFocusContrast"/> against the plate it
        /// will be drawn over; otherwise the next role is tried, and the best of them wins. There
        /// is deliberately no black-or-white backstop: that would paper over a theme whose palette
        /// cannot produce a visible ring, and `kit_focus_contrast_probe` exists to name that theme
        /// instead of hiding it.
        /// </summary>
        public static Color FocusRingColor(Godot.Control ctl)
        {
            Color behind = UiSurface.ControlFace(UiSurface.Of(ctl));
            Color best = UiSurface.Text(ctl);
            float bestRatio = UiSurface.ContrastRatio(best, behind);

            foreach (UiSurface.Role role in FocusRoles)
            {
                if (!UiSurface.TrySemantic(ctl, role, out Color candidate)) continue;
                if (candidate.A < 0.02f) continue;

                float ratio = UiSurface.ContrastRatio(candidate, behind);
                if (ratio >= MinFocusContrast) return candidate with { A = 0.95f };
                if (ratio > bestRatio) { bestRatio = ratio; best = candidate; }
            }

            return best with { A = 0.95f };
        }

        private static readonly UiSurface.Role[] FocusRoles =
            { UiSurface.Role.Focus, UiSurface.Role.Info, UiSurface.Role.Accent };

        public static void DrawFocusRing(Godot.Control ctl, string genre, Rect2 r, KitShape shape,
                                         float widthScale = 1f)
        {
            if (!ctl.HasFocus()) return;
            float w = Mathf.Max(2f, Unit(ctl) * 0.16f * widthScale);
            DrawShape(ctl, genre, r.Grow(w * 0.8f), shape, new Color(0, 0, 0, 0),
                      FocusRingColor(ctl), w);
        }

        /// <summary>
        /// Build the kit's own tooltip panel for a control.
        ///
        /// <see cref="KitTooltip"/> was fully built — a drawn panel with a tail, at the opposite
        /// polarity to the surface it covers — and nothing ever showed one. No kit widget set
        /// <c>TooltipText</c> at all, so Godot never asked for a tooltip in the first place.
        ///
        /// Two things have to be carried across by hand, because Godot parents the returned panel
        /// into a popup OUTSIDE the owner's tree, where neither would be inherited:
        /// the genre meta that <see cref="GenreOf"/> walks ancestors to find, and the generated
        /// Theme that every colour and font size is read from.
        /// </summary>
        public static Godot.Control? MakeTooltip(Godot.Control owner, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var tip = new KitTooltip { Text = text };
            tip.SetMeta(GenreMeta, GenreOf(owner));
            if (InheritedTheme(owner) is { } theme) tip.Theme = theme;
            return tip;
        }

        /// <summary>The nearest Theme in force above this node, which is what a control would have
        /// resolved against had it stayed in the tree.</summary>
        private static Theme? InheritedTheme(Node? node)
        {
            for (Node? cursor = node; cursor != null; cursor = cursor.GetParent())
                if (cursor is Godot.Control control && control.Theme != null)
                    return control.Theme;
            return null;
        }

        public static bool ShouldClearPointerState(Godot.Control ctl, int notification)
            => notification == CanvasItem.NotificationVisibilityChanged && !ctl.IsVisibleInTree();

        /// <summary>
        /// Draw a widget's OWN PLATE — the one surface its content sits on — for the widgets that
        /// derive from a native Godot type and so cannot inherit
        /// <see cref="KitControl.DrawPlate"/>.
        ///
        /// Identical to <see cref="DrawShape(Godot.Control, string, Rect2, KitShape, Color, Color, float, KitWidgetClass)"/>
        /// except that it takes the sprite branch when the genre declares artwork. The
        /// distinction is stated rather than inferred: a slider's track and a tab strip's badge
        /// are drawn with the genre's own class shape too, and neither is a plate.
        ///
        /// As on <see cref="KitControl.DrawPlate"/>, the artwork stands in for the genre's own
        /// silhouette for this class and nothing else — a widget that has chosen a specific form
        /// keeps it.
        /// </summary>
        public static void DrawWidgetPlate(Godot.Control ctl, string genre, Rect2 r, KitShape shape,
                                           Color fill, Color rim, float rimWidth,
                                           KitWidgetClass widgetClass, KitState state)
        {
            if (shape == KitMaterial.WidgetShapeForGenre(genre, widgetClass)
                && KitSprite.TryPlate(ctl, KitGeometry.ForGenre(genre), widgetClass, state, shape, r, fill,
                                      out KitSprite.Plate art))
            {
                KitSprite.Draw(ctl, art);
                return;
            }
            DrawShape(ctl, genre, r, shape, fill, rim, rimWidth, widgetClass);
        }

        /// <summary>Fill a shape inside <paramref name="r"/>, unit-aware.</summary>
        public static void DrawShape(Godot.Control ctl, string genre, Rect2 r, KitShape shape,
                                     Color fill, Color rim, float rimWidth,
                                     KitWidgetClass widgetClass = KitWidgetClass.Button)
        {
            var g = KitGeometry.ForGenre(genre);
            if (r.Size.X < 1f || r.Size.Y < 1f) return;
            rimWidth = KitRim.Width(rimWidth);
            var poly = Poly(shape, r, g, Unit(ctl), widgetClass);
            if (poly.Length < 3) return;
            if (fill.A > 0f) ctl.DrawColoredPolygon(poly, fill);
            if (rimWidth > 0f && rim.A > 0f)
            {
                var closed = new Vector2[poly.Length + 1];
                poly.CopyTo(closed, 0);
                closed[^1] = poly[0];
                ctl.DrawPolyline(closed, rim, rimWidth);
            }
        }

        /// <summary>
        /// A five-pointed star, filled and outlined.
        ///
        /// Shared because two widgets show earned stars and only one of them was drawing a star.
        /// <see cref="KitStarRating"/> had this as a private method; <see cref="KitLevelPath"/>,
        /// whose own class comment says its stars follow KitStarRating, was drawing three
        /// <c>DrawCircle</c> dots under each completed node. A player reading that saw pips, not a
        /// score, and the two widgets disagreed about what a star is on the same screen.
        ///
        /// The inner radius is 0.44 of the outer, which is the proportion KitStarRating was already
        /// using and the one that reads as a star rather than as a pinwheel or a blob.
        /// </summary>
        public static void DrawStar(CanvasItem ci, Vector2 centre, float radius, Color fill, Color ink)
        {
            if (radius <= 0.5f) return;

            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0) ? radius : radius * 0.44f;
                float angle = -Mathf.Pi * 0.5f + i * Mathf.Pi / 5f;
                points[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }
            ci.DrawColoredPolygon(points, fill);

            var closed = new Vector2[11];
            points.CopyTo(closed, 0);
            closed[10] = points[0];
            ci.DrawPolyline(closed, ink, Mathf.Max(1f, radius * 0.12f));
        }

        /// <summary>
        /// How far a corner badge reaches PAST the plate it is pinned to, and therefore how much
        /// room the plate has to give up so the badge can overhang without leaving the control's
        /// own rect. Add this to the top and the trailing edge before drawing the plate.
        /// </summary>
        public static float BadgeOverhang(Godot.Control ctl)
            => BadgeRadius(ctl) * 0.70f;

        private static float BadgeRadius(Godot.Control ctl)
            => Mathf.Clamp(UiSurface.FontSize(ctl, UiSurface.TextRole.Small) * 0.95f, 9f, 15f);

        /// <summary>
        /// A badge STRADDLING a plate's corner — half on the plate, half off it.
        ///
        /// One implementation, because there were three and only one of them was right.
        /// KitAvatarFrame insets its own frame and hangs the badge over the rim, which is the look
        /// every reference sheet uses and the one that reads as a count pinned to an object.
        /// KitPushButton and KitBuildTile instead drew theirs flat inside the plate, sitting on the
        /// control like part of its face, so a cost badge and an owned count looked like decoration
        /// printed on the button rather than a marker attached to it.
        ///
        /// The badge stays inside the CONTROL's rect even while it leaves the PLATE's, so ordinary
        /// Godot containers still lay these out without overlap — the plate gives up the room
        /// (see <see cref="BadgeOverhang"/>) rather than the layout absorbing it.
        ///
        /// A circle when the text is short and a pill when it is not: "3" in a pill reads as a
        /// stretched blob, and "250" in a circle either overflows or ellipsizes to nothing.
        /// </summary>
        public static void DrawCornerBadge(Godot.Control ctl, string genre, Rect2 plate, string text,
                                           UiSurface.Role role, bool topCorner = true)
        {
            if (string.IsNullOrEmpty(text)) return;
            var font = Font(ctl, genre);
            if (font == null) return;

            var g = KitGeometry.ForGenre(genre);
            float radius = BadgeRadius(ctl);
            string label = Case(text, genre);

            int fs = UiSurface.FitRole(ctl, UiSurface.TextRole.Small,
                                       new Vector2(radius * 1.5f, radius * 1.2f), label, font, min: 7);
            Vector2 measured = font.GetStringSize(label, HorizontalAlignment.Left, -1, fs);

            float height = radius * 2f;
            float width = Mathf.Max(height, measured.X + fs * 0.85f);

            // Centre ON the corner, drawn back by a third of the radius so the badge reads as
            // attached to the plate rather than floating off it.
            float cx = plate.End.X - radius * 0.30f;
            float cy = topCorner ? plate.Position.Y + radius * 0.30f : plate.End.Y - radius * 0.30f;

            var r = new Rect2(cx - width * 0.5f, cy - height * 0.5f, width, height);
            // Never outside the control itself: the plate paid for the overhang, the layout did not.
            r.Position = new Vector2(
                Mathf.Clamp(r.Position.X, 0f, Mathf.Max(0f, ctl.Size.X - width)),
                Mathf.Clamp(r.Position.Y, 0f, Mathf.Max(0f, ctl.Size.Y - height)));

            Color fill = UiSurface.SemanticOrDerived(ctl, role);
            if (fill.A < 0.02f) fill = UiSurface.Of(ctl);
            Color ink = UiSurface.Ink(fill);
            float rim = Mathf.Max(1.5f, g.Rim * 0.5f);

            if (Mathf.IsEqualApprox(width, height))
            {
                Vector2 centre = r.Position + r.Size * 0.5f;
                ctl.DrawCircle(centre, radius, fill);
                ctl.DrawArc(centre, radius, 0f, Mathf.Tau, 32, ink, rim);
            }
            else
            {
                DrawShape(ctl, genre, r, KitShape.Pill, fill, ink, rim);
            }

            label = EllipsizeText(font, label, fs, r.Size.X - fs * 0.5f);
            if (string.IsNullOrEmpty(label)) return;
            Vector2 m = font.GetStringSize(label, HorizontalAlignment.Left, -1, fs);
            float baseline = r.Position.Y + (r.Size.Y - font.GetHeight(fs)) * 0.5f + font.GetAscent(fs);
            DrawText(ctl, genre, font,
                     new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, baseline),
                     label, fs, UiSurface.Luminance(fill) > 0.5f
                         ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
        }

        /// <summary>The rim's POLARITY is a genre tell: above 1 a bright carved rim, below 1 the
        /// thick dark outline of the casual family.</summary>
        public static Color Rim(Color face, KitGeometry g) => Tint(face, g.OutlineShade);

        /// <summary>The genre's type family, falling back to the theme default.</summary>
        public static Font? Font(Godot.Control ctl, string genre)
            => KitFonts.Fallback(ctl, KitGeometry.ForGenre(genre).Font);

        public static float PanelHeaderRoom(Godot.Control ctl, string genre, string text,
                                            KitPanelHeaderStyle style, float fontScale = 0.90f,
                                            float hostHeight = 0f, float heightRatio = 0.14f)
        {
            if (string.IsNullOrEmpty(text) || style == KitPanelHeaderStyle.None) return 0f;

            int fs = UiSurface.FontSize(ctl, fontScale, min: 8);
            if (style == KitPanelHeaderStyle.UtilityStrip)
                return Mathf.Max(fs * 1.35f, 14f);

            // Falling back to ctl.Size.Y made this unsafe to call from a sizing path: a caller that
            // omitted hostHeight got the control's CURRENT height folded into a value that becomes
            // part of that control's minimum size. Callers now pass a font-derived height, and the
            // fallback does the same rather than reaching for the live one.
            float h = hostHeight > 0f ? hostHeight : fs * 2.4f;
            return Mathf.Max(fs * 1.32f, h * Mathf.Min(heightRatio, 0.095f)) * 0.5f;
        }

        public static float PanelHeaderOverhang(Godot.Control ctl, string genre, string text,
                                                KitPanelHeaderStyle style, float fontScale = 0.90f,
                                                float hostHeight = 0f, float heightRatio = 0.14f)
            => style == KitPanelHeaderStyle.Banner
                ? PanelHeaderRoom(ctl, genre, text, style, fontScale, hostHeight, heightRatio)
                : 0f;

        public static KitShape PanelHeaderShape(string genre, KitShape? overrideShape = null)
        {
            if (overrideShape.HasValue) return overrideShape.Value;
            return KitGeometry.ForGenre(genre).Register switch
            {
                KitRegister.Carved => KitShape.Ribbon,
                KitRegister.Casual => KitShape.Ellipse,
                _ => KitShape.Rect,
            };
        }

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
            => DrawPanelHeader(ctl, genre, host, text, KitPanelHeaderStyle.Banner, shape, shade,
                               0.90f, heightRatio, widthRatio);

        public static void DrawPanelHeader(Godot.Control ctl, string genre, Rect2 host, string text,
                                           KitPanelHeaderStyle style, KitShape shape,
                                           float shade = 0.44f, float fontScale = 0.90f,
                                           float heightRatio = 0.14f, float widthRatio = 0.62f)
        {
            if (string.IsNullOrEmpty(text) || style == KitPanelHeaderStyle.None
                || host.Size.X < 8f || host.Size.Y < 8f) return;

            var g = KitGeometry.ForGenre(genre);
            var font = Font(ctl, genre);
            if (font == null) return;
            string label = Case(text, genre);
            int fs = UiSurface.FontSize(ctl);
            int titleFs = UiSurface.FontSize(ctl, fontScale, min: 8);

            if (style == KitPanelHeaderStyle.UtilityStrip)
            {
                DrawUtilityPanelHeader(ctl, genre, host, label, font, fs, titleFs, shade);
                return;
            }

            // Floor the height at the type, or the banner clips its own text on a short host.
            float h = Mathf.Max(titleFs * 1.32f, host.Size.Y * Mathf.Min(heightRatio, 0.095f));
            float w = host.Size.X * widthRatio;
            int fit = UiSurface.FitText(ctl, new Vector2(host.Size.X * 0.82f, h * 0.74f),
                                        0.64f, label, font, min: 8, themeMax: fontScale);
            float need = font.GetStringSize(label, HorizontalAlignment.Left, -1, fit).X + fit * 1.35f;
            w = Mathf.Max(host.Size.X * Mathf.Min(widthRatio, 0.54f), Mathf.Min(need, host.Size.X * 0.92f));

            // Centred ON the edge, so half the plate sits outside the host. This is the move
            // containers cannot express and the reason it is drawn rather than parented.
            var r = new Rect2(host.Position.X + (host.Size.X - w) * 0.5f,
                              host.Position.Y - h * 0.5f, w, h);

            Color face = UiSurface.Of(ctl);
            Color plate = HeaderFace(face, shade);
            DrawShape(ctl, genre, r, shape, plate, UiSurface.Ink(face),
                      Mathf.Max(1f, g.Rim * 0.7f * (fs / 14f)));

            label = EllipsizeText(font, label, fit, r.Size.X - fit * 0.95f);
            if (string.IsNullOrEmpty(label)) return;
            Vector2 m = font.GetStringSize(label, HorizontalAlignment.Left, -1, fit);
            Color ink = UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f, 1f)
                : new Color(0.98f, 0.96f, 0.92f, 1f);
            DrawText(ctl, genre, font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                                   r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                     label, fit, ink);
        }

        private static void DrawUtilityPanelHeader(Godot.Control ctl, string genre, Rect2 host,
                                                   string text, Font font, int fs, int titleFs,
                                                   float shade)
        {
            var g = KitGeometry.ForGenre(genre);
            float frame = Mathf.Max(1f, g.FramePx(host.Size.Y));
            float h = Mathf.Max(titleFs * 1.35f, 14f);
            // Real padding. At 6px the title sat hard against the panel's inner edge and read as
            // overflowing text rather than as a titled bar.
            float padX = Mathf.Max(10f, fs * 0.75f);
            var r = new Rect2(host.Position.X + frame, host.Position.Y + frame,
                              Mathf.Max(4f, host.Size.X - frame * 2f), h);
            if (r.Size.X < 4f || r.Size.Y < 4f) return;

            int fit = UiSurface.FitText(ctl, new Vector2(r.Size.X - padX * 2f, h * 0.84f),
                                        0.82f, text, font, min: 8,
                                        themeMax: Mathf.Max(0.45f, titleFs / Mathf.Max(1f, fs)));
            Color face = UiSurface.Of(ctl);
            Color plate = HeaderFace(face, shade);
            DrawShape(ctl, genre, r, KitShape.Rect, plate,
                      UiSurface.Ink(face) with { A = 0.36f },
                      Mathf.Max(1f, g.Rim * 0.25f * (fs / 14f)));
            ctl.DrawLine(new Vector2(r.Position.X, r.End.Y), new Vector2(r.End.X, r.End.Y),
                         UiSurface.Ink(face) with { A = 0.52f },
                         Mathf.Max(1f, g.Rim * 0.35f * (fs / 14f)));

            text = EllipsizeText(font, text, fit, r.Size.X - padX * 2f);
            if (string.IsNullOrEmpty(text)) return;
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fit);
            Color ink = UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f);
            DrawText(ctl, genre, font,
                new Vector2(r.Position.X + padX,
                            r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                text, fit, ink);
        }

        /// <summary>
        /// Draw a string with the theme's TEXT TREATMENT applied.
        ///
        /// Every kit label goes through here, so a theme that declares `text_treatment` changes
        /// its type everywhere at once instead of in whichever widgets remembered to ask. The
        /// offsets are UNIT multiples, so an engraved caption and an engraved title are cut to the
        /// same depth rather than the depth scaling with the glyph.
        /// </summary>
        public static void DrawText(Godot.Control ctl, string genre, Font font, Vector2 at,
                                    string text, int fs, Color ink)
        {
            if (font == null || string.IsNullOrEmpty(text)) return;
            fs = Mathf.Max(1, fs);
            var treat = KitGeometry.ForGenre(genre).TextTreatment;

            // The offset comes from the GLYPH being drawn, not from the control it sits in.
            //
            // It was `Unit(ctl) * 0.075`, the host control's own font size -- so a 10px caption
            // inside a 16px control got the same contour as a 26px title, roughly a tenth of the
            // glyph's height on all four sides. On any face that is what closes the counters and
            // turns small type into blocks.
            float d = Mathf.Max(1f, fs * 0.075f);

            // An OUTLINE is bounded much tighter than the other treatments, because it is the only
            // one that closes a glyph rather than sitting beside it. A contour grows inward from
            // every edge, so at 0.075 of a 16px glyph it eats 1.2px into both sides of a counter
            // only about 3px wide -- measured on the cardgame board, where PLAY rendered with a
            // solid A and BUY with a solid B. One pixel is the smallest contour that reads and the
            // largest a caption-sized glyph can survive; it grows only once there is room.
            float outline = Mathf.Clamp(fs * 0.045f, 1f, 2.5f);

            // Below this, a decorative treatment can only subtract. An outline needs a glyph thick
            // enough to carry a contour and still show its holes; an engrave needs room for two
            // edges. At caption sizes there is neither, so the genre keeps its treatment
            // everywhere it can be read and gives it up where it would destroy the word.
            const int MinTreatedFontSize = 11;
            if (fs < MinTreatedFontSize) treat = KitTextTreat.Plain;

            switch (treat)
            {
                case KitTextTreat.Outlined:
                {
                    // 0.60, not 0.75: a thin contour at high alpha still reads as thick, because
                    // each copy's anti-aliased edge lands inside the counter.
                    Color dark = new(0f, 0f, 0f, 0.60f);
                    foreach (var o in new[] { new Vector2(-outline, 0), new Vector2(outline, 0),
                                              new Vector2(0, -outline), new Vector2(0, outline) })
                        ctl.DrawString(font, at + o, text, HorizontalAlignment.Left, -1, fs, dark);
                    break;
                }
                case KitTextTreat.Engraved:
                    // Dark ABOVE and light BELOW: the glyph reads as cut INTO the surface,
                    // because that is where a top-left key light puts the two edges of a groove.
                    ctl.DrawString(font, at + new Vector2(0, -d), text, HorizontalAlignment.Left,
                                   -1, fs, new Color(0f, 0f, 0f, 0.55f));
                    ctl.DrawString(font, at + new Vector2(0, d), text, HorizontalAlignment.Left,
                                   -1, fs, new Color(1f, 1f, 1f, 0.30f));
                    break;
                case KitTextTreat.Extruded:
                {
                    // A solid side face BELOW the glyph -- stacked, not one offset copy, or the
                    // face reads as a drop shadow with a gap instead of a slab.
                    Color side = new(ink.R * 0.28f, ink.G * 0.26f, ink.B * 0.30f, 1f);
                    for (float i = d * 3f; i >= 1f; i -= 1f)
                        ctl.DrawString(font, at + new Vector2(0, i), text, HorizontalAlignment.Left,
                                       -1, fs, side);
                    break;
                }
            }
            ctl.DrawString(font, at, text, HorizontalAlignment.Left, -1, fs, ink);
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
                string text = EllipsizeText(font, Case(a.Text, genre), fs, r.Size.X * 0.84f);
                if (string.IsNullOrEmpty(text)) continue;
                Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
                DrawText(ctl, genre, font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                                       r.Position.Y + (r.Size.Y + m.Y * 0.6f) * 0.5f),
                         text, fs, UiSurface.Ink(fill));
            }
        }

        public static void DrawEmptyPreview(Godot.Control ctl, string genre, Rect2 bounds,
                                            KitShape shape, string label = "")
        {
            if (!Engine.IsEditorHint() || bounds.Size.X <= 8f || bounds.Size.Y <= 8f)
                return;

            var g = KitGeometry.ForGenre(genre);
            Color face = UiSurface.Of(ctl);
            Color ink = UiSurface.Ink(face);
            float fs = UiSurface.FontSize(ctl);
            float rim = Mathf.Max(1f, g.Rim * 0.55f * (fs / 14f));
            float inset = Mathf.Clamp(Mathf.Min(bounds.Size.X, bounds.Size.Y) * 0.08f, 4f, 12f);
            Rect2 r = bounds.Grow(-inset);
            if (r.Size.X <= 4f || r.Size.Y <= 4f)
                return;

            Color well = RecessFace(face, g.WellShade) with { A = 0.62f };
            DrawShape(ctl, genre, r, shape, well, ink with { A = 0.32f }, rim);

            float y = r.Position.Y + r.Size.Y * 0.5f;
            float x0 = r.Position.X + r.Size.X * 0.24f;
            float x1 = r.End.X - r.Size.X * 0.24f;
            if (x1 > x0 + 2f)
                ctl.DrawLine(new Vector2(x0, y), new Vector2(x1, y),
                             ink with { A = 0.28f }, Mathf.Max(1f, fs * 0.10f));

            if (string.IsNullOrWhiteSpace(label))
                return;

            Font? font = Font(ctl, genre);
            if (font == null)
                return;

            string text = Case(label, genre);
            int size = UiSurface.FitRole(ctl, UiSurface.TextRole.Small,
                                         new Vector2(r.Size.X * 0.70f, r.Size.Y * 0.28f),
                                         text, font, min: 7);
            text = EllipsizeText(font, text, size, r.Size.X * 0.70f);
            if (string.IsNullOrEmpty(text))
                return;

            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
            DrawText(ctl, genre, font,
                     new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                 y + Mathf.Max(fs * 0.55f, m.Y * 0.85f)),
                     text, size, ink with { A = 0.42f });
        }

        public static System.Collections.Generic.List<string> WrapLines(Font font, string text,
                                                                        int fs, float width)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (font == null || string.IsNullOrWhiteSpace(text) || width <= 1f) return lines;
            fs = Mathf.Max(1, fs);

            foreach (string paragraph in text.Replace("\r", "").Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    lines.Add("");
                    continue;
                }

                string line = "";
                foreach (string word in paragraph.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
                {
                    string remaining = word;
                    while (font.GetStringSize(remaining, HorizontalAlignment.Left, -1, fs).X > width
                           && remaining.Length > 1)
                    {
                        int cut = remaining.Length;
                        while (cut > 1
                               && font.GetStringSize(remaining[..cut], HorizontalAlignment.Left, -1, fs).X > width)
                            cut--;
                        string chunk = remaining[..cut];
                        if (!string.IsNullOrEmpty(line))
                        {
                            lines.Add(line);
                            line = "";
                        }
                        lines.Add(chunk);
                        remaining = remaining[cut..];
                    }

                    string trial = string.IsNullOrEmpty(line) ? remaining : line + " " + remaining;
                    if (font.GetStringSize(trial, HorizontalAlignment.Left, -1, fs).X <= width || string.IsNullOrEmpty(line))
                        line = trial;
                    else
                    {
                        lines.Add(line);
                        line = remaining;
                    }
                }
                if (!string.IsNullOrEmpty(line)) lines.Add(line);
            }

            return lines;
        }

        public static void DrawWrappedText(Godot.Control ctl, string genre, Font font, Rect2 box,
                                           string text, int fs, Color ink,
                                           HorizontalAlignment align = HorizontalAlignment.Left,
                                           int maxLines = 0, bool ellipsize = true)
        {
            if (font == null || string.IsNullOrWhiteSpace(text)
                || box.Size.X <= 1f || box.Size.Y <= 1f) return;
            fs = Mathf.Max(1, fs);

            var lines = WrapLines(font, Case(text, genre), fs, box.Size.X);
            if (lines.Count == 0) return;

            float lh = font.GetHeight(fs) * 1.08f;
            int fitLines = Mathf.Max(1, Mathf.FloorToInt(box.Size.Y / lh));
            int count = maxLines > 0 ? Mathf.Min(maxLines, fitLines) : fitLines;
            count = Mathf.Min(count, lines.Count);

            for (int i = 0; i < count; i++)
            {
                string line = lines[i];
                if (ellipsize && i == count - 1 && count < lines.Count)
                    line = EllipsizeText(font, line, fs, box.Size.X);
                Vector2 m = font.GetStringSize(line, HorizontalAlignment.Left, -1, fs);
                float x = align switch
                {
                    HorizontalAlignment.Center => box.Position.X + (box.Size.X - m.X) * 0.5f,
                    HorizontalAlignment.Right => box.End.X - m.X,
                    _ => box.Position.X,
                };
                DrawText(ctl, genre, font,
                         new Vector2(x, box.Position.Y + lh * i + font.GetAscent(fs)),
                         line, fs, ink);
            }
        }

        public static string EllipsizeText(Font font, string text, int fs, float width)
        {
            const string mark = "...";
            fs = Mathf.Max(1, fs);
            if (width <= 1f) return "";
            if (font.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X <= width) return text;
            string t = text;
            while (t.Length > 0
                   && font.GetStringSize(t + mark, HorizontalAlignment.Left, -1, fs).X > width)
                t = t[..^1];
            return string.IsNullOrEmpty(t)
                ? font.GetStringSize(mark, HorizontalAlignment.Left, -1, fs).X <= width ? mark : ""
                : t + mark;
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
                                Color fill, Color rim, float rimWidth, float unit = 0f,
                                KitWidgetClass widgetClass = KitWidgetClass.Button)
        {
            rimWidth = KitRim.Width(rimWidth);
            if (r.Size.X < 1f || r.Size.Y < 1f) return;
            var poly = Poly(shape, r, g, unit, widgetClass);
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
            var g = KitGeometry.ForGenre(GenreOf(ctl));
            var font = KitFonts.Fallback(ctl, g.Font);
            if (font == null) return;
            if (g.UpperCase) text = text.ToUpperInvariant();
            int fs = Mathf.Max(1, UiSurface.FontSize(ctl));
            string[] lines = text.Split('\n');
            float lh = fs * 1.15f;
            float top = box.Position.Y + (box.Size.Y - lh * lines.Length) * 0.5f + fs * 0.82f + dy;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = EllipsizeText(font, lines[i], fs, box.Size.X);
                if (string.IsNullOrEmpty(line)) continue;
                Vector2 m = font.GetStringSize(line, HorizontalAlignment.Left, -1, fs);
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
                    foreach (char ch in line)
                    {
                        string one = ch.ToString();
                        ci.DrawString(font, new Vector2(gx, top + lh * i), one,
                                      HorizontalAlignment.Left, -1, fs, col);
                        gx += font.GetStringSize(one, HorizontalAlignment.Left, -1, fs).X
                            + fs * g.Tracking;
                    }
                }
                else
                    ci.DrawString(font, new Vector2(x, top + lh * i), line,
                                  HorizontalAlignment.Left, -1, fs, col);
            }
        }
    }
}
