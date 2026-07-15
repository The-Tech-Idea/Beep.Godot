using System.Collections.Generic;
using Godot;
using Beep.GameBuilder; // BeepFileUtils

namespace Beep.ECS.UI
{
    /// <summary>
    /// File-based skin catalog. Scans the <c>skins/</c> directory tree at runtime
    /// and loads every genre, theme, palette, and geometry profile from JSON —
    /// zero hardcoded data. To add a new genre/theme/palette/geometry, drop a
    /// file in the right folder; this loader picks it up automatically.
    ///
    /// Directory layout (autoloaded by scanning):
    /// <code>
    /// catalogs/skins/
    /// ├── platformer/
    /// │   ├── genre.json          ← tuning + theme shortlist + scene list
    /// │   ├── geometry.json       ← per-genre geometry profile
    /// │   └── themes/
    /// │       └── cartoon/
    /// │           ├── theme.json  ← 22 colors + geometry + animation
    /// │           ├── default.json  ← palette
    /// │           └── warm.json     ← palette
    /// ├── topdown/  (same structure)
    /// ├── shooter/  (same structure)
    /// └── puzzle/   (same structure)
    /// </code>
    ///
    /// Uses <see cref="BeepFileUtils.LoadJson"/> + Godot's <see cref="DirAccess"/>
    /// for scanning — no System.Text.Json, matching the addon's established pattern.
    /// Lazy-cached on first access (follows the BeepParticleGenerator pattern).
    /// </summary>
    public static class SkinCatalog
    {
        private const string SkinsRoot = "res://addons/beep_game_builder_cs/catalogs/skins";

        private static Dictionary<string, GenreDef>? _genres;
        private static readonly object _lock = new();

        // ════════════════════════════════════════════════════════════════
        //  Public API
        // ════════════════════════════════════════════════════════════════

        /// <summary>All genres, keyed by id (e.g. "platformer"). Lazy-loaded on first access.</summary>
        public static Dictionary<string, GenreDef> AllGenres
        {
            get { lock (_lock) { _genres ??= LoadAllGenres(); return _genres; } }
        }

        /// <summary>Get one genre by id, or null.</summary>
        public static GenreDef? GetGenre(string genreId)
            => AllGenres.TryGetValue(genreId.ToLowerInvariant(), out var g) ? g : null;

        /// <summary>Get a theme within a genre by theme id.</summary>
        public static ThemeDef? GetTheme(string genreId, string themeId)
        {
            var genre = GetGenre(genreId);
            if (genre == null) return null;
            return genre.Themes.TryGetValue(themeId.ToLowerInvariant(), out var t) ? t : null;
        }

        /// <summary>Get the per-genre geometry profile.</summary>
        public static GeometryDef? GetGeometry(string genreId)
        {
            var genre = GetGenre(genreId);
            return genre?.Geometry;
        }

        /// <summary>Force a reload (useful in the editor after editing JSON).</summary>
        public static void Reload()
        {
            lock (_lock) { _genres = LoadAllGenres(); }
        }

        // ════════════════════════════════════════════════════════════════
        //  Directory scanning
        // ════════════════════════════════════════════════════════════════

        private static Dictionary<string, GenreDef> LoadAllGenres()
        {
            var result = new Dictionary<string, GenreDef>();

            // Check the skins root exists.
            if (!DirAccess.DirExistsAbsolute(SkinsRoot))
            {
                GD.PushWarning($"[SkinCatalog] Skins directory not found: {SkinsRoot}");
                return result;
            }

            using var dir = DirAccess.Open(SkinsRoot);
            if (dir == null)
            {
                GD.PushWarning($"[SkinCatalog] Could not open skins directory: {SkinsRoot}");
                return result;
            }

            dir.ListDirBegin();
            string entry = dir.GetNext();
            while (entry != "")
            {
                // Skip hidden files (.gdignore, .import, etc.).
                if (!entry.StartsWith(".") && !entry.EndsWith(".import"))
                {
                    string fullPath = $"{SkinsRoot}/{entry}";
                    if (DirAccess.DirExistsAbsolute(fullPath))
                    {
                        string genreId = entry.ToLowerInvariant();
                        var genre = LoadGenre(genreId, fullPath);
                        if (genre != null)
                        {
                            result[genreId] = genre;
                            GD.Print($"[SkinCatalog] Loaded genre: {genreId} ({genre.Themes.Count} themes)");
                        }
                    }
                }
                entry = dir.GetNext();
            }
            dir.ListDirEnd();

            GD.Print($"[SkinCatalog] Total genres loaded: {result.Count}");
            return result;
        }

        private static GenreDef? LoadGenre(string genreId, string genrePath)
        {
            // genre.json — genre definition (tuning, theme list, scenes).
            var genreDef = new GenreDef { Id = genreId };
            var genreJson = BeepFileUtils.LoadJson($"{genrePath}/genre.json");
            if (genreJson.Count > 0)
            {
                genreDef.DisplayName = Str(genreJson, "display_name", genreId);
                genreDef.Icon = Str(genreJson, "icon", "🎯");
                genreDef.Description = Str(genreJson, "description");
                genreDef.DefaultTheme = Str(genreJson, "default_theme").ToLowerInvariant();
                genreDef.DefaultGeometryId = Str(genreJson, "default_geometry");
                genreDef.MainScene = Str(genreJson, "main_scene");
                if (genreJson.TryGetValue("scenes", out var scenesVar) && scenesVar.VariantType == Variant.Type.Array)
                    foreach (var s in scenesVar.AsStringArray()) genreDef.Scenes.Add(s);
                if (genreJson.TryGetValue("tuning", out var tuningVar) && tuningVar.VariantType == Variant.Type.Dictionary)
                    genreDef.Tuning = tuningVar.AsGodotDictionary();
                if (genreJson.TryGetValue("nav_wiring", out var navVar) && navVar.VariantType == Variant.Type.Dictionary)
                    genreDef.NavWiring = navVar.AsGodotDictionary();
            }

            // geometry.json — per-genre geometry profile.
            var geoPath = $"{genrePath}/geometry.json";
            if (FileAccess.FileExists(geoPath))
            {
                var geoJson = BeepFileUtils.LoadJson(geoPath);
                if (geoJson.Count > 0)
                    genreDef.Geometry = ParseGeometry(geoJson);
            }

            // themes/ — scan subdirectories, each is a theme.
            var themes = new Dictionary<string, ThemeDef>();
            string themesPath = $"{genrePath}/themes";
            if (DirAccess.DirExistsAbsolute(themesPath))
            {
                using var themesDir = DirAccess.Open(themesPath);
                if (themesDir != null)
                {
                    themesDir.ListDirBegin();
                    string themeEntry = themesDir.GetNext();
                    while (themeEntry != "")
                    {
                        if (!themeEntry.StartsWith("."))
                        {
                            string themeFullPath = $"{themesPath}/{themeEntry}";
                            if (DirAccess.DirExistsAbsolute(themeFullPath))
                            {
                                string themeId = themeEntry.ToLowerInvariant();
                                var theme = LoadTheme(themeId, themeFullPath);
                                if (theme != null) themes[themeId] = theme;
                            }
                        }
                        themeEntry = themesDir.GetNext();
                    }
                    themesDir.ListDirEnd();
                }
            }
            genreDef.Themes = themes;
            return genreDef;
        }

        private static ThemeDef? LoadTheme(string themeId, string themePath)
        {
            var themeJsonPath = $"{themePath}/theme.json";
            if (!FileAccess.FileExists(themeJsonPath)) return null;

            var json = BeepFileUtils.LoadJson(themeJsonPath);
            if (json.Count == 0) return null;

            var theme = new ThemeDef
            {
                Id = Str(json, "id", themeId).ToLowerInvariant(),
                DisplayName = Str(json, "display_name", themeId),
                Category = Str(json, "category"),
                Description = Str(json, "description")
            };

            // Parse colors block (22 hex strings → Color).
            if (json.TryGetValue("colors", out var colorsVar) && colorsVar.VariantType == Variant.Type.Dictionary)
            {
                var cd = colorsVar.AsGodotDictionary();
                theme.Colors = new ColorSchema
                {
                    SurfacePrimary = HexColor(cd, "surface_primary"),
                    SurfaceHover = HexColor(cd, "surface_hover"),
                    SurfacePressed = HexColor(cd, "surface_pressed"),
                    SurfaceDisabled = HexColor(cd, "surface_disabled"),
                    TextPrimary = HexColor(cd, "text_primary"),
                    TextHover = HexColor(cd, "text_hover"),
                    TextDisabled = HexColor(cd, "text_disabled"),
                    TextOnDark = HexColor(cd, "text_on_dark"),
                    AccentPrimary = HexColor(cd, "accent_primary"),
                    AccentSecondary = HexColor(cd, "accent_secondary"),
                    BorderNormal = HexColor(cd, "border_normal"),
                    BorderHover = HexColor(cd, "border_hover"),
                    BorderFocus = HexColor(cd, "border_focus"),
                    BorderBevelLight = HexColor(cd, "border_bevel_light"),
                    BorderBevelDark = HexColor(cd, "border_bevel_dark"),
                    ShadowColor = HexColor(cd, "shadow_color"),
                    BgPanel = HexColor(cd, "bg_panel"),
                    BgCanvas = HexColor(cd, "bg_canvas"),
                    SemanticSuccess = HexColor(cd, "semantic_success"),
                    SemanticDanger = HexColor(cd, "semantic_danger"),
                    SemanticWarning = HexColor(cd, "semantic_warning"),
                    SemanticInfo = HexColor(cd, "semantic_info")
                };
            }

            // Parse geometry block (12 numbers).
            if (json.TryGetValue("geometry", out var geoVar) && geoVar.VariantType == Variant.Type.Dictionary)
            {
                var gd = geoVar.AsGodotDictionary();
                theme.Geometry = new ThemeGeometry
                {
                    CornerRadius = Int(gd, "corner_radius"),
                    BorderLeft = Int(gd, "border_left"),
                    BorderTop = Int(gd, "border_top"),
                    BorderRight = Int(gd, "border_right"),
                    BorderBottom = Int(gd, "border_bottom"),
                    ShadowSize = Int(gd, "shadow_size"),
                    ShadowOffsetX = Int(gd, "shadow_offset_x"),
                    ShadowOffsetY = Int(gd, "shadow_offset_y"),
                    PadLeft = Int(gd, "pad_left"),
                    PadRight = Int(gd, "pad_right"),
                    PadTop = Int(gd, "pad_top"),
                    PadBottom = Int(gd, "pad_bottom"),
                    FontSize = Int(gd, "font_size", 14)
                };
            }

            // Parse animation block (6 fields).
            if (json.TryGetValue("animation", out var animVar) && animVar.VariantType == Variant.Type.Dictionary)
            {
                var ad = animVar.AsGodotDictionary();
                theme.Animation = new AnimationConfig
                {
                    HoverScaleAmount = Float(ad, "hover_scale", 1.04f),
                    HoverScaleDuration = Float(ad, "hover_duration", 0.15f),
                    PressScaleAmount = Float(ad, "press_scale", 0.96f),
                    PressScaleDuration = Float(ad, "press_duration", 0.08f),
                    EnableShadowLift = Bool(ad, "shadow_lift", true),
                    EnableFocusGlow = Bool(ad, "focus_glow", true)
                };
            }

            // Parse the optional "textures" block — per-node-type StyleBoxTexture specs.
            theme.Textures = ParseTextures(json);

            // Scan palette files (everything except theme.json).
            theme.Palettes = new Dictionary<string, ColorPalette>();
            using var themeDir = DirAccess.Open(themePath);
            if (themeDir != null)
            {
                themeDir.ListDirBegin();
                string palEntry = themeDir.GetNext();
                while (palEntry != "")
                {
                    if (palEntry.EndsWith(".json") && palEntry != "theme.json")
                    {
                        var pal = LoadPalette($"{themePath}/{palEntry}");
                        if (pal != null)
                            theme.Palettes[pal.DisplayName.ToLowerInvariant()] = pal;
                    }
                    palEntry = themeDir.GetNext();
                }
                themeDir.ListDirEnd();
            }

            return theme;
        }

        private static ColorPalette? LoadPalette(string path)
        {
            var json = BeepFileUtils.LoadJson(path);
            if (json.Count == 0) return null;
            return new ColorPalette
            {
                DisplayName = Str(json, "display_name", "Default"),
                HueShift = Float(json, "hue_shift"),
                SaturationMul = Float(json, "saturation_mul", 1f),
                ValueMul = Float(json, "value_mul", 1f)
            };
        }

        private static GeometryDef ParseGeometry(Godot.Collections.Dictionary json)
        {
            var def = new GeometryDef
            {
                Id = Str(json, "id"),
                DisplayName = Str(json, "display_name"),
                CornerRadius = Int(json, "corner_radius", -1),
                BorderWidth = Int(json, "border_width", -1),
                ShadowSize = Int(json, "shadow_size", -1),
                ShadowOffsetY = Float(json, "shadow_offset_y", -1f),
                ContentPadding = Int(json, "content_padding", -1),
                FontSize = Int(json, "font_size", -1)
            };

            // Parse the optional per-node-type "shapes" block.
            if (json.TryGetValue("shapes", out var shapesVar) && shapesVar.VariantType == Variant.Type.Dictionary)
                def.Shapes = ParseShapes(shapesVar.AsGodotDictionary());

            // Parse the optional background-image block.
            // Schema: { "background_image": "res://path.png", "background_mode": "tile|stretch|center" }
            def.BackgroundImage = Str(json, "background_image");
            def.BackgroundMode = Str(json, "background_mode", "stretch");

            return def;
        }

        /// <summary>Parse the per-node-type shape overrides block from geometry.json.</summary>
        private static ShapeOverrides ParseShapes(Godot.Collections.Dictionary d) => new()
        {
            Panel = new ShapeOverrides.PanelShape
            {
                ShadowReduction = Int(d.ContainsKey("panel") ? d["panel"].AsGodotDictionary() : new Godot.Collections.Dictionary(), "shadow_reduction", 2)
            },
            Input = new ShapeOverrides.InputShape
            {
                InsetX = Int(ShapeSub(d, "input"), "inset_x", 4),
                InsetY = Int(ShapeSub(d, "input"), "inset_y", 3),
                MinX = Int(ShapeSub(d, "input"), "min_x", 4),
                MinY = Int(ShapeSub(d, "input"), "min_y", 2),
                FocusBorderMin = Int(ShapeSub(d, "input"), "focus_border_min", 2)
            },
            Progress = new ShapeOverrides.ProgressShape
            {
                CornerInset = Int(ShapeSub(d, "progress"), "corner_inset", 4),
                Margin = Int(ShapeSub(d, "progress"), "margin", 2)
            },
            Slider = new ShapeOverrides.SliderShape
            {
                GrabberShadow = Int(ShapeSub(d, "slider"), "grabber_shadow", 3),
                GrabberHoverShadow = Int(ShapeSub(d, "slider"), "grabber_hover_shadow", 5),
                ShadowScale = Float(ShapeSub(d, "slider"), "shadow_scale", 0.5f),
                TrackDivisor = Int(ShapeSub(d, "slider"), "track_divisor", 2)
            },
            Scrollbar = new ShapeOverrides.ScrollbarShape
            {
                GrabberDivisor = Int(ShapeSub(d, "scrollbar"), "grabber_divisor", 3),
                GrabberMin = Int(ShapeSub(d, "scrollbar"), "grabber_min", 3)
            },
            Selection = new ShapeOverrides.SelectionShape
            {
                CornerDivisor = Int(ShapeSub(d, "selection"), "corner_divisor", 2),
                CornerMin = Int(ShapeSub(d, "selection"), "corner_min", 2),
                MarginX = Int(ShapeSub(d, "selection"), "margin_x", 4),
                FocusBorder = Int(ShapeSub(d, "selection"), "focus_border", 1)
            },
            Separator = new ShapeOverrides.SeparatorShape
            {
                Separation = Int(ShapeSub(d, "separator"), "separation", 4)
            }
        };

        /// <summary>Get a nested sub-dictionary from the shapes block, or empty if missing.</summary>
        private static Godot.Collections.Dictionary ShapeSub(Godot.Collections.Dictionary d, string key)
            => d.ContainsKey(key) ? d[key].AsGodotDictionary() : new Godot.Collections.Dictionary();

        /// <summary>Parse the optional "textures" block from theme.json. Returns
        /// null when the block is absent. Per-slot entries may themselves be
        /// null (slot absent) — callers should use TextureSlotDef?.BuildStyleBox()
        /// which returns null for both cases.</summary>
        private static ThemeTextureSlots? ParseTextures(Godot.Collections.Dictionary json)
        {
            if (!json.TryGetValue("textures", out var texVar)
                || texVar.VariantType != Variant.Type.Dictionary) return null;

            var t = texVar.AsGodotDictionary();
            var slots = new ThemeTextureSlots();
            slots.ButtonNormal   = ParseTextureSlot(t, "button_normal");
            slots.ButtonHover    = ParseTextureSlot(t, "button_hover");
            slots.ButtonPressed  = ParseTextureSlot(t, "button_pressed");
            slots.ButtonDisabled = ParseTextureSlot(t, "button_disabled");
            slots.ButtonFocus    = ParseTextureSlot(t, "button_focus");
            slots.Panel          = ParseTextureSlot(t, "panel");
            slots.Dialog         = ParseTextureSlot(t, "dialog");
            slots.InputNormal    = ParseTextureSlot(t, "input_normal");
            slots.InputFocus     = ParseTextureSlot(t, "input_focus");
            slots.ProgressBg     = ParseTextureSlot(t, "progress_bg");
            slots.ProgressFill   = ParseTextureSlot(t, "progress_fill");
            slots.SliderGrabber  = ParseTextureSlot(t, "slider_grabber");
            slots.ScrollGrabber  = ParseTextureSlot(t, "scroll_grabber");
            slots.Separator      = ParseTextureSlot(t, "separator");
            return slots;
        }

        /// <summary>Parse one texture slot sub-dictionary. Returns null when the
        /// slot key is absent from the textures block.</summary>
        private static TextureSlotDef? ParseTextureSlot(Godot.Collections.Dictionary textures, string slotKey)
        {
            if (!textures.TryGetValue(slotKey, out var sVar)
                || sVar.VariantType != Variant.Type.Dictionary) return null;
            var s = sVar.AsGodotDictionary();
            // texture_path is the only required-ish field; if it's absent the slot is a no-op.
            string? path = Str(s, "texture_path");
            if (string.IsNullOrEmpty(path)) return null;

            return new TextureSlotDef
            {
                Path = path,
                MarginLeft   = Float(s, "margin_left", 0f),
                MarginTop    = Float(s, "margin_top", 0f),
                MarginRight  = Float(s, "margin_right", 0f),
                MarginBottom = Float(s, "margin_bottom", 0f),
                StretchH     = Int(s, "axis_stretch_horizontal", 1),
                StretchV     = Int(s, "axis_stretch_vertical", 1),
                DrawCenter   = Bool(s, "draw_center", true),
                Modulate     = HexColor(s, "modulate"),
                ContentMarginLeft   = Float(s, "content_margin_left", -1f),
                ContentMarginRight  = Float(s, "content_margin_right", -1f),
                ContentMarginTop    = Float(s, "content_margin_top", -1f),
                ContentMarginBottom = Float(s, "content_margin_bottom", -1f),
                ExpandMarginLeft   = Float(s, "expand_margin_left", 0f),
                ExpandMarginRight  = Float(s, "expand_margin_right", 0f),
                ExpandMarginTop    = Float(s, "expand_margin_top", 0f),
                ExpandMarginBottom = Float(s, "expand_margin_bottom", 0f),
            };
        }

        /// <summary>Parse a #RRGGBB or #RRGGBBAA hex string into a Godot Color.</summary>
        private static Color HexColor(Godot.Collections.Dictionary d, string key)
        {
            string hex = Str(d, key, "#FFFFFFFF");
            return Color.FromString(hex, new Color(1, 1, 1, 1));
        }

        // ── Safe dictionary accessors (Godot.Collections.Dictionary has no .Get(key, default)) ──

        private static string Str(Godot.Collections.Dictionary d, string key, string def = "")
            => d.ContainsKey(key) ? d[key].AsString() : def;

        private static int Int(Godot.Collections.Dictionary d, string key, int def = 0)
            => d.ContainsKey(key) ? d[key].AsInt32() : def;

        private static float Float(Godot.Collections.Dictionary d, string key, float def = 0f)
            => d.ContainsKey(key) ? d[key].AsSingle() : def;

        private static bool Bool(Godot.Collections.Dictionary d, string key, bool def = false)
            => d.ContainsKey(key) ? d[key].AsBool() : def;
    }

    // ════════════════════════════════════════════════════════════════
    //  Data definitions (plain classes — loaded from JSON at runtime)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// One slot in the textures{} block of theme.json. Mirrors every
    /// StyleBoxTexture property so the engine can build the exact 9-patch
    /// the author specified. Defaults match StyleBoxTexture defaults, so a
    /// partial entry paints the texture 1:1 with no margins.
    /// </summary>
    public class TextureSlotDef
    {
        /// <summary>res:// path to the PNG. Null/empty disables this slot.</summary>
        public string? Path;

        // 9-patch margins — how many px from each edge stay fixed when stretching.
        public float MarginLeft = 0, MarginTop = 0, MarginRight = 0, MarginBottom = 0;

        // AxisStretchMode: 0=Stretch, 1=Tile, 2=TileFit (matches Godot enum order).
        public int StretchH = 1, StretchV = 1;

        /// <summary>Whether to paint the center tile (true) or just the 9-patch borders (false).</summary>
        public bool DrawCenter = true;

        /// <summary>Color tint applied over the texture.</summary>
        public Color Modulate = new(1, 1, 1, 1);

        // Content margins — negative = leave default (StyleBoxTexture falls back to texture_margin_).
        public float ContentMarginLeft = -1, ContentMarginRight = -1,
                    ContentMarginTop = -1, ContentMarginBottom = -1;

        // Expand margins (push the box outward from its content rect).
        public float ExpandMarginLeft = 0, ExpandMarginRight = 0,
                    ExpandMarginTop = 0, ExpandMarginBottom = 0;

        /// <summary>Build the live StyleBoxTexture. Returns null if no texture_path
        /// is set OR the resource fails to load — callers fall back to procedural.</summary>
        public StyleBoxTexture? BuildStyleBox()
        {
            if (string.IsNullOrEmpty(Path) || !ResourceLoader.Exists(Path)) return null;
            var tex = ResourceLoader.Load<Texture2D>(Path);
            if (tex == null) return null;
            var sb = new StyleBoxTexture { Texture = tex };
            sb.TextureMarginLeft   = MarginLeft;
            sb.TextureMarginTop    = MarginTop;
            sb.TextureMarginRight  = MarginRight;
            sb.TextureMarginBottom = MarginBottom;
            sb.AxisStretchHorizontal = (StyleBoxTexture.AxisStretchMode)StretchH;
            sb.AxisStretchVertical   = (StyleBoxTexture.AxisStretchMode)StretchV;
            sb.DrawCenter = DrawCenter;
            sb.ModulateColor = Modulate;
            if (ContentMarginLeft   >= 0) sb.ContentMarginLeft   = ContentMarginLeft;
            if (ContentMarginRight  >= 0) sb.ContentMarginRight  = ContentMarginRight;
            if (ContentMarginTop    >= 0) sb.ContentMarginTop    = ContentMarginTop;
            if (ContentMarginBottom >= 0) sb.ContentMarginBottom = ContentMarginBottom;
            sb.ExpandMarginLeft   = ExpandMarginLeft;
            sb.ExpandMarginTop    = ExpandMarginTop;
            sb.ExpandMarginRight  = ExpandMarginRight;
            sb.ExpandMarginBottom = ExpandMarginBottom;
            return sb;
        }
    }

    /// <summary>All texture slots declared by a theme.json's "textures" block.
    /// Null = theme ships without textures; per-slot null = that slot uses
    /// procedural StyleBoxFlat.</summary>
    public class ThemeTextureSlots
    {
        // Button states
        public TextureSlotDef? ButtonNormal;
        public TextureSlotDef? ButtonHover;
        public TextureSlotDef? ButtonPressed;
        public TextureSlotDef? ButtonDisabled;
        public TextureSlotDef? ButtonFocus;
        // Other nodes
        public TextureSlotDef? Panel;
        public TextureSlotDef? Dialog;
        public TextureSlotDef? InputNormal;
        public TextureSlotDef? InputFocus;
        public TextureSlotDef? ProgressBg;
        public TextureSlotDef? ProgressFill;
        public TextureSlotDef? SliderGrabber;
        public TextureSlotDef? ScrollGrabber;
        public TextureSlotDef? Separator;

        /// <summary>True if any slot has a texture_path set.</summary>
        public bool AnyTexture =>
            ButtonNormal != null || ButtonHover != null || ButtonPressed != null
            || ButtonDisabled != null || ButtonFocus != null || Panel != null || Dialog != null
            || InputNormal != null || InputFocus != null
            || ProgressBg != null || ProgressFill != null
            || SliderGrabber != null || ScrollGrabber != null || Separator != null;
    }

    public class GenreDef
    {
        public string Id = "";
        public string DisplayName = "";
        public string Icon = "🎯";
        public string Description = "";
        public string DefaultTheme = "";
        public string DefaultGeometryId = "";
        public string MainScene = "";
        public List<string> Scenes = new();
        public Godot.Collections.Dictionary Tuning = new();
        /// <summary>Scene-to-navigation mapping from genre.json. Key = scene filename
        /// (e.g. "main_menu.tscn"), Value = Dictionary of property→res:// path.</summary>
        public Godot.Collections.Dictionary NavWiring = new();
        public GeometryDef? Geometry;
        public Dictionary<string, ThemeDef> Themes = new();
    }

    public class ThemeDef
    {
        public string Id = "";
        public string DisplayName = "";
        public string Category = "";
        public string Description = "";
        public ColorSchema Colors;
        public ThemeGeometry Geometry;
        public AnimationConfig Animation; // populated from theme.json's "animation" block
        public Dictionary<string, ColorPalette> Palettes = new();
        /// <summary>Per-node-type StyleBoxTexture specs from the "textures" block.
        /// Null when the theme ships without textures.</summary>
        public ThemeTextureSlots? Textures;
    }

    /// <summary>Per-theme geometry template extracted from theme.json (replaces GetButtonNormal).</summary>
    public struct ThemeGeometry
    {
        public int CornerRadius;
        public int BorderLeft, BorderTop, BorderRight, BorderBottom;
        public int ShadowSize;
        public int ShadowOffsetX, ShadowOffsetY;
        public int PadLeft, PadRight, PadTop, PadBottom;
        public int FontSize;
    }

    /// <summary>Per-genre geometry override profile (from geometry.json).</summary>
    public class GeometryDef
    {
        public string Id = "";
        public string DisplayName = "";
        public int CornerRadius = -1;
        public int BorderWidth = -1;
        public int ShadowSize = -1;
        public float ShadowOffsetY = -1f;
        public int ContentPadding = -1;
        public int FontSize = -1;

        /// <summary>Per-node-type shape overrides from the "shapes" sub-block. May be
        /// null if the genre omitted the block — callers should treat null as "use
        /// defaults".</summary>
        public ShapeOverrides? Shapes;

        /// <summary>Background option (texture path for a full-canvas backdrop,
        /// drawn behind all panels). Null = no background image.</summary>
        public string? BackgroundImage;

        /// <summary>How to render the background image: "tile", "stretch", or
        /// "center". Only meaningful when <see cref="BackgroundImage"/> is set.</summary>
        public string BackgroundMode = "stretch";

        /// <summary>Convert to the runtime GeometryProfile (reuses the existing ApplyTo logic).</summary>
        public GeometryProfile ToProfile() => new()
        {
            DisplayName = DisplayName,
            CornerRadius = CornerRadius,
            BorderWidth = BorderWidth,
            ShadowSize = ShadowSize,
            ShadowOffsetY = ShadowOffsetY,
            ContentPadding = ContentPadding,
            FontSize = FontSize,
            Shapes = Shapes,
            BackgroundImage = BackgroundImage,
            BackgroundMode = BackgroundMode
        };
    }
}
