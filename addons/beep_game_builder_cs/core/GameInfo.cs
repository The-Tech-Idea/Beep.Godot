using Godot;

namespace Beep.GameBuilder;

/// <summary>
/// Central game descriptor read by every scene template. Registered as the
/// "GameInfo" autoload so any node — C# or GDScript — can access it via
/// /root/GameInfo. GDScript reads it transparently:
///   var info = get_node("/root/GameInfo")
///   title.text = info.game_name
/// Saved as res://game_info.tres so it round-trips through the inspector.
/// </summary>
[Tool]
[GlobalClass]
public partial class GameInfo : Resource
{
    // ── Identity ──
    /// <summary>Displayed in the main-menu title and the OS window title.</summary>
    [Export] public string GameName { get; set; } = "My Game";
    [Export] public string Version { get; set; } = "0.1.0";
    [Export] public string Developer { get; set; } = "";
    /// <summary>Genre id — the folder name under <c>catalogs/skins/</c>. Comes from the
    /// skin catalog, so adding a genre is still just dropping a folder; there is no
    /// hardcoded genre list to keep in sync. Cascade root for the theme/palette/
    /// geometry dropdowns below.</summary>
    [Export]
    public string GenreId
    {
        get => _genreId;
        set { _genreId = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); }
    }
    private string _genreId = "platformer";
    [Export] public string Description { get; set; } = "";

    // ── Display ──
    [Export] public int TargetResolutionWidth { get; set; } = 1280;
    [Export] public int TargetResolutionHeight { get; set; } = 720;
    [Export] public bool PixelArt { get; set; } = true;
    [Export] public int TargetFps { get; set; } = 60;

    /// <summary>Convenience accessor built from the two exported int fields.</summary>
    public Vector2I TargetResolution => new(TargetResolutionWidth, TargetResolutionHeight);

    // ── Theme (all resolved from the file-based skin catalog: skins/<genre>/...) ──
    /// <summary>Theme preset id (e.g. "cartoon", "modern"). Must match a theme.json
    /// in the selected genre's themes/ folder.</summary>
    [Export]
    public string DefaultThemePreset
    {
        // Palette options depend on the selected theme — refresh so it re-cascades.
        get => _defaultThemePreset;
        set { _defaultThemePreset = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); }
    }
    private string _defaultThemePreset = "modern";

    /// <summary>Palette id (e.g. "warm", "cool"). Must match a palette .json in the
    /// selected theme's folder. "Default" = no tint.</summary>
    [Export] public string PaletteName { get; set; } = "Default";

    /// <summary>Optional texture-based UI skin resource. When set, the theme engine
    /// builds StyleBoxTexture (9-patch) for all UI nodes instead of procedural StyleBoxFlat.
    /// Set in the inspector or via the one-click dock.</summary>
    [Export] public Beep.ECS.UI.UISkin? Skin { get; set; }

    /// <summary>Geometry profile display name from the genre's geometry.json.
    /// "As-Authored" = use the theme's own geometry.</summary>
    [Export] public string GeometryProfileName { get; set; } = "As-Authored";

    // ── Shared scene paths — every genre uses these. Read by the per-scene navigation
    //    scripts in ecs/scenes/ and by GameFlowComponent. (NavigationComponent does NOT
    //    consume them; it is not attached to any scene.) ──
    [Export] public string MainMenuPath { get; set; } = "res://scenes/ui/main_menu.tscn";
    [Export] public string GameScenePath { get; set; } = "res://scenes/main/main.tscn";
    [Export] public string SettingsScenePath { get; set; } = "res://scenes/ui/settings_menu.tscn";
    [Export] public string GameOverScenePath { get; set; } = "res://scenes/ui/game_over.tscn";

    /// <summary>Pause overlay, instanced on top of gameplay by GameFlowComponent when the
    /// "pause" action fires — not navigated to, so the game scene stays loaded underneath.</summary>
    [Export] public string PauseMenuPath { get; set; } = "res://scenes/ui/pause_menu.tscn";

    // ── Genre-specific scene paths.
    //    Set at GENERATION time from the selected genre's `nav_wiring` block in genre.json
    //    (BeepGenreGenerator.ApplyNavWiring), which clears them all first and then applies
    //    only what that genre declares. The defaults below are placeholders for the
    //    inspector — do NOT rely on them: a genre that omits a path leaves it empty, which
    //    means "this genre has no such screen". See docs/FILE_FORMATS.md#nav_wiring. ──
    [ExportGroup("Genre Scenes")]
    // Platformer
    [Export] public string LevelSelectPath { get; set; } = "res://scenes/ui/platformer/level_select.tscn";
    [Export] public string LevelResultsPath { get; set; } = "res://scenes/ui/platformer/level_results.tscn";
    // Shooter
    [Export] public string CharacterSelectPath { get; set; } = "res://scenes/ui/shooter/character_select.tscn";
    [Export] public string LevelUpPath { get; set; } = "res://scenes/ui/shooter/level_up_choice.tscn";
    [Export] public string RunResultsPath { get; set; } = "res://scenes/ui/shooter/run_results.tscn";
    [Export] public string CodexPath { get; set; } = "res://scenes/ui/shooter/codex.tscn";
    // Puzzle
    [Export] public string LevelMapPath { get; set; } = "res://scenes/ui/puzzle/level_map.tscn";
    [Export] public string PreLevelPath { get; set; } = "res://scenes/ui/puzzle/pre_level.tscn";
    [Export] public string LevelCompletePath { get; set; } = "res://scenes/ui/puzzle/level_complete.tscn";
    [Export] public string LevelFailedPath { get; set; } = "res://scenes/ui/puzzle/level_failed.tscn";

    // ── Genre tuning (controllers read the values relevant to them) ──
    [ExportGroup("Platformer")]
    [Export] public float Gravity { get; set; } = 980f;
    [Export] public float JumpVelocity { get; set; } = -400f;
    [ExportGroup("Movement")]
    [Export] public float MoveSpeed { get; set; } = 200f;
    [ExportGroup("Shooter")]
    [Export] public float FireRate { get; set; } = 0.2f;
    [ExportGroup("Puzzle")]
    [Export] public int GridWidth { get; set; } = 8;
    [Export] public int GridHeight { get; set; } = 8;
    [Export] public int TargetScore { get; set; } = 1000;
    [ExportGroup("Weather")]
    [Export] public bool EnableWeather { get; set; } = false;
    [Export] public ECS.WeatherSystemComponent.WeatherType DefaultWeather { get; set; } = ECS.WeatherSystemComponent.WeatherType.Clear;
    [Export] public bool EnableDayNightCycle { get; set; } = false;

    [ExportGroup("Seasons")]
    [Export] public bool EnableSeasons { get; set; } = true;
    [Export] public ECS.SeasonalComponent.Season DefaultSeason { get; set; } = ECS.SeasonalComponent.Season.Spring;
    [Export] public double DaysPerSeason { get; set; } = 7.0;

    [ExportGroup("Climate")]
    [Export] public bool EnableTemperature { get; set; } = false;
    [Export] public float AmbientTemperature { get; set; } = 20f;
    [Export] public bool EnableWeatherForecast { get; set; } = true;
    [Export] public int ForecastDays { get; set; } = 7;

    [ExportGroup("Save/Load")]
    [Export] public bool EnableGameStateManager { get; set; } = true;
    [Export] public int MaxSaveSlots { get; set; } = 5;
    [Export] public string SaveDirectory { get; set; } = "user://saves";
    [Export] public bool AutosaveEnabled { get; set; } = true;
    [Export] public float AutosaveIntervalSeconds { get; set; } = 300f;

    /// <summary>Grid dimensions as a Vector2I (built from exported ints).</summary>
    public Vector2I GridSize => new(GridWidth, GridHeight);

    /// <summary>Canonical save path for the GameInfo resource.</summary>
    public const string TresPath = "res://game_info.tres";

    /// <summary>Default path for the main menu scene — used when the user
    /// leaves GameInfo.MainMenuPath empty.</summary>
    public const string DefaultMainMenuPath = "res://scenes/ui/main_menu.tscn";

    /// <summary>Default path for the game scene (the scene set as the project's
    /// main scene).</summary>
    public const string DefaultGameScenePath = "res://scenes/main/main.tscn";

    /// <summary>Default path for the settings menu scene.</summary>
    public const string DefaultSettingsScenePath = "res://scenes/ui/settings_menu.tscn";

    /// <summary>Default path for the game-over scene.</summary>
    public const string DefaultGameOverScenePath = "res://scenes/ui/game_over.tscn";

    /// <summary>
    /// The active GameInfo — loaded from game_info.tres by the GameApp autoload.
    /// Returns GameApp.Instance?.Info, or null if GameApp hasn't loaded yet.
    /// </summary>
    public static GameInfo? Instance => ECS.GameApp.Instance?.Info;

    /// <summary>Genre → the default theme id, straight from that genre's genre.json.
    /// Returns "" when the genre isn't in the catalog — there is no hardcoded theme to
    /// fall back to, since themes are whatever folders exist.</summary>
    public static string RecommendedTheme(string genreId)
        => Beep.ECS.UI.SkinCatalog.GetGenre(genreId)?.DefaultTheme ?? "";

    /// <summary>Genre → every theme id the catalog found in that genre's themes/ folder.
    /// Empty when the genre isn't in the catalog.</summary>
    public static string[] RecommendedThemes(string genreId)
    {
        var g = Beep.ECS.UI.SkinCatalog.GetGenre(genreId);
        if (g == null) return System.Array.Empty<string>();
        var result = new System.Collections.Generic.List<string>();
        foreach (var t in g.Themes.Values)
            result.Add(t.Id);
        return result.ToArray();
    }

    // ── Inspector dropdowns ─────────────────────────────────────────────────
    // Every option below is read from the skin catalog's folder tree at edit time —
    // nothing here is hardcoded. GenreId is the cascade root.

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        base._ValidateProperty(property);

        switch ((string)property["name"])
        {
            case nameof(GenreId):
                Beep.ECS.UI.SkinPropertyHints.ApplyEnum(property,
                    Beep.ECS.UI.SkinPropertyHints.GenreHint(_genreId));
                break;
            case nameof(DefaultThemePreset):
                Beep.ECS.UI.SkinPropertyHints.ApplyEnum(property,
                    Beep.ECS.UI.SkinPropertyHints.ThemeHint(_genreId, _defaultThemePreset));
                break;
            case nameof(PaletteName):
                Beep.ECS.UI.SkinPropertyHints.ApplyEnum(property,
                    Beep.ECS.UI.SkinPropertyHints.PaletteHint(_genreId, _defaultThemePreset, PaletteName));
                break;
            case nameof(GeometryProfileName):
                Beep.ECS.UI.SkinPropertyHints.ApplyEnum(property,
                    Beep.ECS.UI.SkinPropertyHints.GeometryHint(_genreId, GeometryProfileName));
                break;
        }
    }
}
