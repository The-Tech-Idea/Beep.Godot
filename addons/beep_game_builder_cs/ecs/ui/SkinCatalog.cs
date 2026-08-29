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
        // ── Global art register ──────────────────────────────────────────────────────────
        // PROJECT-WIDE, deliberately. These were [Export]s on ThemePresetComponent, so "does
        // this game use heavy game-art chrome" was answered separately by every scene — 40-odd
        // scene files each holding their own copy of one art direction, which had to be edited
        // in lockstep and never was. A game has ONE art register; it is configured in one
        // place.
        public const string SettingChrome     = "beep/ui/game_art_chrome";
        public const string SettingOutline    = "beep/ui/game_art_outline";
        public const string SettingShadow     = "beep/ui/game_art_shadow";
        public const string SettingHudOpacity = "beep/ui/hud_plate_opacity";

        private static bool _registerRead;
        private static bool _chrome = true;
        private static int _outline = 3;
        private static int _shadow = 4;
        private static float _hudOpacity = 0.82f;

        /// <summary>Heavy outline + drop shadow on every generated control. Off gives a flat,
        /// minimal skin. See docs/GAME_UI_KIT_SPEC.md.</summary>
        public static bool GameArtChrome { get { ReadRegister(); return _chrome; } }

        /// <summary>Minimum outline weight in px; a genre's geometry profile may ask for more.</summary>
        public static int GameArtOutline { get { ReadRegister(); return _outline; } }

        /// <summary>Drop shadow size, applied only where the geometry profile asked for none.</summary>
        public static int GameArtShadow { get { ReadRegister(); return _shadow; } }

        /// <summary>Opacity of HUD plates. Lower lets more of the world through.</summary>
        public static float HudPlateOpacity { get { ReadRegister(); return _hudOpacity; } }

        private static void ReadRegister()
        {
            if (_registerRead) return;
            _registerRead = true;
            if (ProjectSettings.HasSetting(SettingChrome))
                _chrome = ProjectSettings.GetSetting(SettingChrome).AsBool();
            if (ProjectSettings.HasSetting(SettingOutline))
                _outline = Mathf.Clamp(ProjectSettings.GetSetting(SettingOutline).AsInt32(), 0, 8);
            if (ProjectSettings.HasSetting(SettingShadow))
                _shadow = Mathf.Clamp(ProjectSettings.GetSetting(SettingShadow).AsInt32(), 0, 12);
            if (ProjectSettings.HasSetting(SettingHudOpacity))
                _hudOpacity = Mathf.Clamp((float)ProjectSettings.GetSetting(SettingHudOpacity).AsDouble(), 0.3f, 1f);
        }

        /// <summary>Re-read the global register — call after the dock changes a setting so a
        /// live editor reflects it without a restart.</summary>
        public static void RefreshRegisterSettings() { _registerRead = false; ReadRegister(); }

        // ── Active skin ──────────────────────────────────────────────────────────────────
        // A game has ONE genre, theme and palette. They live here, not on every scene's
        // ThemePresetComponent, for the same reason the art register does: a per-scene copy
        // drifts, and deciding which copy wins is a rule nobody should have to know.
        public static string ActiveGenre { get; private set; } = "";
        public static string ActiveTheme { get; private set; } = "";
        public static string ActivePalette { get; private set; } = "";
        public static string ActiveGeometry { get; private set; } = "";

        /// <summary>True once a game has published its skin; until then components use their
        /// own exported values, so a scene opened on its own in the editor still renders.</summary>
        public static bool HasActiveSkin => !string.IsNullOrEmpty(ActiveGenre);

        /// <summary>Publish the game's skin. Every ThemePresetComponent reads it.</summary>
        public static void SetActiveSkin(string genre, string theme, string palette, string geometry)
        {
            ActiveGenre = genre ?? "";
            ActiveTheme = theme ?? "";
            ActivePalette = palette ?? "";
            ActiveGeometry = geometry ?? "";

            // Publish the theme's style axes. Without this the whole `kit` block is inert: it
            // parses, it validates, and it reaches nothing -- which is how the font role behaved
            // for a whole stage before the warning count gave it away. Cleared when the theme
            // declares none, so switching to a plain theme drops the previous theme's style
            // instead of inheriting it.
            var def = string.IsNullOrEmpty(ActiveGenre) || string.IsNullOrEmpty(ActiveTheme)
                ? null : GetTheme(ActiveGenre, ActiveTheme);
            Kit.KitStyleJson.Set(ActiveGenre, def?.Kit);
        }

        /// <summary>Publish the active skin directly from the game descriptor.</summary>
        public static void SetActiveSkin(GameInfo info)
        {
            string theme = info.DefaultThemePreset;
            if (string.IsNullOrWhiteSpace(theme))
                theme = GetGenre(info.GenreId)?.DefaultTheme ?? "";

            SetActiveSkin(info.GenreId, theme, info.PaletteName, info.GeometryProfileName);
        }

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
            if (genreJson.Count == 0)
            {
                // Was: fall through and return a GenreDef with an empty DisplayName, MainScene
                // and no themes. LoadAllGenres registered it, the dock listed it, and
                // StampProject wrote a main scene to "res://scenes/main/" — a path ending in
                // a slash. A folder without a readable genre.json is not a genre.
                GD.PushWarning($"[SkinCatalog] Skipping '{genreId}': {genrePath}/genre.json is missing or unreadable.");
                return null;
            }
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

            // The "kit" block: the style axes. Handed to KitStyleJson as-is rather than parsed
            // here, so its schema lives with the code that consumes it.
            if (json.TryGetValue("kit", out var kitVar) && kitVar.VariantType == Variant.Type.Dictionary)
                theme.Kit = kitVar.AsGodotDictionary();

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
        /// <summary>Navigation wiring from genre.json. Flat, one level: Key = GameInfo
        /// property name (e.g. "LevelSelectPath"), Value = scene filename relative to the
        /// genre's UI folder (e.g. "level_select.tscn"). Applied by
        /// BeepGenreGenerator.ApplyNavWiring, which resolves the value to
        /// res://scenes/ui/&lt;genre&gt;/&lt;value&gt;.
        ///
        /// (This previously described a nested "scene filename → dictionary of property→path"
        /// shape, which is not what the loader or any genre.json uses — authoring from it
        /// produced a block where every key tripped ApplyNavWiring's unknown-property warning.)</summary>
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
        /// <summary>The raw "kit" block, if the theme declares one — the style axes
        /// (shadow, outline polarity, corner, shear, font, selection). Kept as the raw
        /// dictionary because <see cref="Kit.KitStyleJson"/> owns its schema and validates it,
        /// including warning about keys this catalog would otherwise drop in silence.</summary>
        public Godot.Collections.Dictionary? Kit;
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
            Shapes = Shapes
        };
    }
}
