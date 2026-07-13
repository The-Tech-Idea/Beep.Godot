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
    public enum GameGenre { Platformer, TopDown, Shooter, Puzzle }

    /// <summary>Convert a genre id string ("platformer", "topdown", ...) to the enum.
    /// Uses Enum.Parse so any new genre added to the enum + a matching genre.json
    /// folder works without editing this method. Falls back to Platformer.</summary>
    public static GameGenre GenreFromId(string genreId)
    {
        // Title-case the id ("platformer" → "Platformer") to match enum member names.
        if (string.IsNullOrEmpty(genreId)) return GameGenre.Platformer;
        string pascal = char.ToUpperInvariant(genreId[0]) + genreId[1..].ToLowerInvariant();
        return System.Enum.TryParse<GameGenre>(pascal, ignoreCase: true, out var g) ? g : GameGenre.Platformer;
    }

    // ── Identity ──
    /// <summary>Displayed in the main-menu title and the OS window title.</summary>
    [Export] public string GameName { get; set; } = "My Game";
    [Export] public string Version { get; set; } = "0.1.0";
    [Export] public string Developer { get; set; } = "";
    [Export] public GameGenre Genre { get; set; } = GameGenre.Platformer;
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
    [Export] public string DefaultThemePreset { get; set; } = "modern";

    /// <summary>Palette id (e.g. "warm", "cool"). Must match a palette .json in the
    /// selected theme's folder. "Default" = no tint.</summary>
    [Export] public string PaletteName { get; set; } = "Default";

    /// <summary>Geometry profile display name from the genre's geometry.json.
    /// "As-Authored" = use the theme's own geometry.</summary>
    [Export] public string GeometryProfileName { get; set; } = "As-Authored";

    // ── Scene paths (consumed by NavigationComponent + GameFlowComponent) ──
    [Export] public string MainMenuPath { get; set; } = "res://scenes/ui/main_menu.tscn";
    [Export] public string GameScenePath { get; set; } = "res://scenes/main/main.tscn";
    [Export] public string SettingsScenePath { get; set; } = "res://scenes/ui/settings_menu.tscn";
    [Export] public string GameOverScenePath { get; set; } = "res://scenes/ui/game_over.tscn";

    // ── Genre-specific scene paths (set by BeepGenreScene's nav_wiring at runtime) ──
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

    /// <summary>Genre → the default theme id from the file-based skin catalog's genre.json.</summary>
    public static string RecommendedTheme(GameGenre genre)
    {
        var g = Beep.ECS.UI.SkinCatalog.GetGenre(genre.ToString().ToLowerInvariant());
        return g?.DefaultTheme ?? "modern";
    }

    /// <summary>Genre → theme id shortlist from the file-based skin catalog.
    /// Read from genre.json's themes[] array. The dock's theme picker shows these.</summary>
    public static string[] RecommendedThemes(GameGenre genre)
    {
        var g = Beep.ECS.UI.SkinCatalog.GetGenre(genre.ToString().ToLowerInvariant());
        if (g == null) return new[] { "modern" };
        var result = new System.Collections.Generic.List<string>();
        foreach (var t in g.Themes.Values)
            result.Add(t.Id);
        return result.ToArray();
    }
}
