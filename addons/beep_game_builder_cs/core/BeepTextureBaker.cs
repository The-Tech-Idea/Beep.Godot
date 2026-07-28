using Godot;
using System.Collections.Generic;
using Beep.ECS.UI;

namespace Beep.GameBuilder
{
    /// <summary>
    /// Bakes the 9-patch PNGs that every theme.json ALREADY declares.
    ///
    /// The texture pipeline was complete in code and completely inert on disk: all 50 shipped
    /// themes declare a textures{} block (button_normal / hover / pressed / disabled + panel),
    /// and not one of the 200 unique files existed. Every StyleBoxTexture lookup fell through to
    /// the procedural box, so every per-node texture toggle in the inspector did nothing.
    ///
    /// This writes those exact declared paths, drawing each slot from the theme's OWN
    /// ColorSchema + ThemeGeometry — so no theme.json is edited and no artist is required, and a
    /// developer can still drop their own PNG over any file to replace it.
    ///
    /// EDITOR ONLY: res:// is read-only in an exported game. Call from the dock or MCP.
    ///
    /// Two deliberate properties:
    ///  • The image is sized so its corner regions equal the margins the slot already declares,
    ///    which is what makes it a correct 9-patch — corners stay crisp, the middle stretches.
    ///  • No drop shadow is baked. StyleBoxTexture has no shadow_size, and faking one inside the
    ///    texture would inset the visible edge and change every widget's metrics the moment
    ///    textures are switched on. Depth comes from a vertical gradient instead. If you want
    ///    shadows, stay procedural or supply art with matching expand margins.
    /// </summary>
    public static class BeepTextureBaker
    {
        /// <summary>Stretchable middle, in px. Only needs to be >= 1; a few px keeps the
        /// PNG legible when opened by hand.</summary>
        private const int CenterPx = 8;

        /// <summary>Vertical gradient applied across the fill (top brighter, bottom darker).
        /// Subtle on purpose — the theme's own color must stay recognisable.</summary>
        private const float GradientTop = 1.07f, GradientBottom = 0.93f;

        /// <summary>Bake every theme of every genre in the catalog, plus the tiling page
        /// backgrounds each genre's geometry.json names.</summary>
        public static List<string> BakeAll()
        {
            var log = new List<string>();
            var written = new HashSet<string>();
            foreach (var (genreId, genre) in SkinCatalog.AllGenres)
                foreach (var themeId in genre.Themes.Keys)
                    BakeInto(genreId, themeId, written, log);
            BakeBackgrounds(written, log);
            Finish(written, log);
            return log;
        }

        /// <summary>Bake the tiling page background each genre's geometry.json declares.
        ///
        /// All 8 shipped <c>background_image</c> paths pointed into an empty folder, so
        /// ApplyBackground() returned in silence and no genre ever had a patterned page.
        /// These are deliberately LOW-ALPHA patterns on transparency: the page canvas colour
        /// (bg_canvas) paints first and the tile tints over it, so one pattern reads correctly
        /// against every theme and palette of its genre rather than fighting them.</summary>
        public static List<string> BakeBackgrounds() { var l = new List<string>(); var w = new HashSet<string>(); BakeBackgrounds(w, l); Finish(w, l); return l; }

        private static void BakeBackgrounds(HashSet<string> written, List<string> log)
        {
            foreach (var (genreId, _) in SkinCatalog.AllGenres)
            {
                var geo = SkinCatalog.GetGeometry(genreId);
                string? path = geo?.BackgroundImage;
                if (string.IsNullOrEmpty(path) || !written.Add(path)) continue;

                var img = DrawPattern(path.GetFile().GetBaseName());
                string dir = path.GetBaseDir();
                if (!DirAccess.DirExistsAbsolute(dir) && DirAccess.MakeDirRecursiveAbsolute(dir) != Error.Ok)
                { log.Add($"✗ background {genreId}: cannot create {dir}"); continue; }

                // .jpg cannot carry alpha; those genres get an opaque page instead of a tint.
                var err = path.EndsWith(".jpg") || path.EndsWith(".jpeg")
                    ? img.SaveJpg(path, 0.9f)
                    : img.SavePng(path);
                if (err != Error.Ok) { log.Add($"✗ background {genreId}: save failed ({err}) → {path}"); continue; }
                log.Add($"✓ background {genreId} → {path.GetFile()} (128×128, tiling)");
            }
        }

        /// <summary>A seamless 128×128 tile chosen by file name. Every pattern wraps at the
        /// edges — these are tiled, so a seam would repeat across the whole screen.</summary>
        private static Image DrawPattern(string name)
        {
            const int S = 128;
            var img = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
            bool opaque = name == "parchment";
            var baseCol = opaque ? new Color(0.78f, 0.71f, 0.55f, 1f) : new Color(0, 0, 0, 0);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    img.SetPixel(x, y, baseCol);

            void Ink(int x, int y, float a, bool light = true)
            {
                x = ((x % S) + S) % S; y = ((y % S) + S) % S;      // wrap = seamless
                var prev = img.GetPixel(x, y);
                var c = light ? new Color(1, 1, 1, 1) : new Color(0, 0, 0, 1);
                img.SetPixel(x, y, new Color(
                    prev.R * (1 - a) + c.R * a, prev.G * (1 - a) + c.G * a,
                    prev.B * (1 - a) + c.B * a, opaque ? 1f : Mathf.Max(prev.A, a)));
            }

            switch (name)
            {
                case "city_grid":
                case "strategy_grid":
                    for (int i = 0; i < S; i++)
                        for (int k = 0; k < S; k += 32) { Ink(i, k, 0.10f); Ink(k, i, 0.10f); }
                    for (int i = 0; i < S; i++) { Ink(i, 0, 0.16f); Ink(0, i, 0.16f); }
                    break;
                case "racing_lines":                                   // 45° stripes
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                            if (((x + y) % 24) < 3) Ink(x, y, 0.07f);
                    break;
                case "card_felt":                                      // fine dotted felt
                    for (int y = 0; y < S; y += 4)
                        for (int x = 0; x < S; x += 4)
                            Ink(x + ((y / 4) % 2 == 0 ? 0 : 2), y, 0.05f, light: false);
                    break;
                case "survival_canvas":                                // woven canvas
                    for (int i = 0; i < S; i++)
                        for (int k = 0; k < S; k += 8) { Ink(i, k, 0.05f); Ink(k + 4, i, 0.05f, light: false); }
                    break;
                case "rpg_tile":                                       // stone blocks, staggered rows
                    for (int y = 0; y < S; y += 32)
                    {
                        for (int i = 0; i < S; i++) Ink(i, y, 0.10f, light: false);   // mortar course
                        int off = (y / 32) % 2 == 0 ? 0 : 32;                          // stagger alternate rows
                        for (int j = 0; j < 32; j++)
                        {
                            Ink(off, y + j, 0.10f, light: false);
                            Ink(off + 64, y + j, 0.10f, light: false);
                        }
                    }
                    break;
                case "sky_tile":                                       // soft vertical wash
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                            Ink(x, y, 0.06f * (1f - (float)y / (S - 1)));
                    break;
                case "parchment":                                      // mottled opaque paper
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                        {
                            float n = Mathf.Sin(x * 0.21f) * Mathf.Sin(y * 0.17f)
                                    + Mathf.Sin((x + y) * 0.07f) * 0.5f;
                            Ink(x, y, Mathf.Abs(n) * 0.06f, light: n > 0);
                        }
                    break;
                default:                                               // unknown name: faint grid
                    for (int i = 0; i < S; i++)
                        for (int k = 0; k < S; k += 32) { Ink(i, k, 0.08f); Ink(k, i, 0.08f); }
                    break;
            }
            return img;
        }

        /// <summary>Bake one theme. genre/theme are catalog ids ("racing", "arcade").</summary>
        public static List<string> BakeTheme(string genreId, string themeId)
        {
            var log = new List<string>();
            var written = new HashSet<string>();
            BakeInto(genreId, themeId, written, log);
            Finish(written, log);
            return log;
        }

        /// <summary>Bake every theme of one genre.</summary>
        public static List<string> BakeGenre(string genreId)
        {
            var log = new List<string>();
            var written = new HashSet<string>();
            var genre = SkinCatalog.GetGenre(genreId);
            if (genre == null) { log.Add($"✗ unknown genre '{genreId}'"); return log; }
            foreach (var themeId in genre.Themes.Keys)
                BakeInto(genreId, themeId, written, log);
            Finish(written, log);
            return log;
        }

        private static void Finish(HashSet<string> written, List<string> log)
        {
            log.Add($"— {written.Count} texture(s) written");
            if (written.Count == 0) return;
            // Godot will not load a PNG it has not imported. Without this the freshly baked
            // files stay invisible to ResourceLoader until the next manual filesystem scan,
            // which looks exactly like the bake having done nothing.
            if (Engine.IsEditorHint())
                EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
            else
                log.Add("! not in the editor — run a filesystem scan before these load");
        }

        private static void BakeInto(string genreId, string themeId, HashSet<string> written, List<string> log)
        {
            var theme = SkinCatalog.GetTheme(genreId, themeId);
            if (theme == null) { log.Add($"✗ {genreId}/{themeId}: not in the catalog"); return; }
            if (theme.Textures is not { } tex)
            {
                log.Add($"· {genreId}/{themeId}: no textures{{}} block — nothing to bake");
                return;
            }

            var c = theme.Colors;
            string label = $"{genreId}/{themeId}";

            // Order matters. A theme may point two slots at ONE file — the shipped themes all
            // aim button_disabled at button_normal.png and vary it with modulate, which is why
            // 250 slot declarations resolve to only 200 files. Baking normal first means the
            // shared file gets the normal art and disabled tints it, as the author intended.
            Bake(label, "button_normal",   tex.ButtonNormal,   theme, c.SurfacePrimary,  c.BorderNormal,    written, log);
            Bake(label, "button_hover",    tex.ButtonHover,    theme, c.SurfaceHover,    c.BorderHover,     written, log);
            Bake(label, "button_pressed",  tex.ButtonPressed,  theme, c.SurfacePressed,  c.AccentSecondary, written, log);
            Bake(label, "button_focus",    tex.ButtonFocus,    theme, c.SurfacePrimary,  c.BorderFocus,     written, log);
            Bake(label, "button_disabled", tex.ButtonDisabled, theme, c.SurfaceDisabled, Fade(c.BorderNormal, 0.4f), written, log);
            Bake(label, "panel",           tex.Panel,          theme, c.BgPanel,         c.BorderNormal,    written, log);
            Bake(label, "dialog",          tex.Dialog,         theme, c.BgPanel,         c.BorderFocus,     written, log);
            Bake(label, "input_normal",    tex.InputNormal,    theme, c.SurfacePressed,  c.BorderNormal,    written, log);
            Bake(label, "input_focus",     tex.InputFocus,     theme, c.SurfacePressed,  c.BorderFocus,     written, log);
            Bake(label, "progress_bg",     tex.ProgressBg,     theme, c.SurfaceDisabled, c.BorderNormal,    written, log);
            Bake(label, "progress_fill",   tex.ProgressFill,   theme, c.AccentPrimary,   c.AccentPrimary,   written, log);
            Bake(label, "separator",       tex.Separator,      theme, c.BorderNormal,    c.BorderNormal,    written, log);
        }

        private static void Bake(string label, string slotName, TextureSlotDef? slot, ThemeDef theme,
                                 Color fill, Color border, HashSet<string> written, List<string> log)
        {
            if (slot?.Path is not { } path || string.IsNullOrEmpty(path)) return;
            // "baked": false marks art a human drew. The baker only knows how to draw a plain
            // rounded box, so baking such a slot is pure destruction — and this runs from a dock
            // button and an MCP command, where "bake everything" is one careless click.
            if (!slot.Baked)
            {
                log.Add($"· {label} {slotName}: authored art (\"baked\": false) — skipped");
                return;
            }
            if (!written.Add(path))
            {
                log.Add($"· {label} {slotName}: shares {path.GetFile()} with an earlier slot — kept");
                return;
            }

            int mL = Mathf.Max(0, (int)slot.MarginLeft),  mT = Mathf.Max(0, (int)slot.MarginTop);
            int mR = Mathf.Max(0, (int)slot.MarginRight), mB = Mathf.Max(0, (int)slot.MarginBottom);
            int w = mL + mR + CenterPx, h = mT + mB + CenterPx;

            var g = theme.Geometry;
            // The corner curve must fit inside the frozen corner region, or the 9-patch would
            // slice through the curve and stretch it.
            int marginMin = Mathf.Min(Mathf.Min(mL, mT), Mathf.Min(mR, mB));
            float radius = Mathf.Clamp(g.CornerRadius, 0, Mathf.Min(marginMin, Mathf.Min(w, h) / 2));
            float borderW = Mathf.Max(0, Mathf.Max(Mathf.Max(g.BorderLeft, g.BorderRight),
                                                   Mathf.Max(g.BorderTop, g.BorderBottom)));

            var img = Draw(w, h, radius, borderW, fill, border);

            string dir = path.GetBaseDir();
            if (!DirAccess.DirExistsAbsolute(dir))
            {
                var mkErr = DirAccess.MakeDirRecursiveAbsolute(dir);
                if (mkErr != Error.Ok) { log.Add($"✗ {label} {slotName}: cannot create {dir} ({mkErr})"); return; }
            }

            var err = img.SavePng(path);
            if (err != Error.Ok) { log.Add($"✗ {label} {slotName}: SavePng failed ({err}) → {path}"); return; }
            log.Add($"✓ {label} {slotName} → {path.GetFile()} ({w}×{h}, patch {mL}/{mT}/{mR}/{mB}, r{radius:0})");
        }

        /// <summary>Draw one rounded-rect 9-patch tile, anti-aliased via the rounded-box
        /// signed distance field: coverage = how much of the pixel falls inside the shape.</summary>
        private static Image Draw(int w, int h, float radius, float borderW, Color fill, Color border)
        {
            var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            var half = new Vector2(w / 2f, h / 2f);
            var clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < h; y++)
            {
                float t = h > 1 ? (float)y / (h - 1) : 0f;
                Color shaded = Shade(fill, Mathf.Lerp(GradientTop, GradientBottom, t));
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f - half.X, y + 0.5f - half.Y);
                    float d = SdRoundBox(p, half, radius);
                    float aOut = Mathf.Clamp(0.5f - d, 0f, 1f);      // outside edge, 1px AA
                    if (aOut <= 0f) { img.SetPixel(x, y, clear); continue; }

                    // Inset by the border width: aIn is how much of the pixel is interior fill.
                    float aIn = borderW > 0f ? Mathf.Clamp(0.5f - (d + borderW), 0f, 1f) : 1f;

                    img.SetPixel(x, y, new Color(
                        shaded.R * aIn + border.R * (1f - aIn),
                        shaded.G * aIn + border.G * (1f - aIn),
                        shaded.B * aIn + border.B * (1f - aIn),
                        (shaded.A * aIn + border.A * (1f - aIn)) * aOut));
                }
            }
            return img;
        }

        /// <summary>Signed distance from p to a rounded box centred on the origin.
        /// Negative inside. The standard iq formulation.</summary>
        private static float SdRoundBox(Vector2 p, Vector2 halfExtent, float radius)
        {
            Vector2 q = p.Abs() - halfExtent + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(q.X, 0f), Mathf.Max(q.Y, 0f)).Length()
                 + Mathf.Min(Mathf.Max(q.X, q.Y), 0f) - radius;
        }

        private static Color Shade(Color c, float f)
            => new(Mathf.Clamp(c.R * f, 0f, 1f), Mathf.Clamp(c.G * f, 0f, 1f), Mathf.Clamp(c.B * f, 0f, 1f), c.A);

        private static Color Fade(Color c, float a) => new(c.R, c.G, c.B, a);
    }
}
