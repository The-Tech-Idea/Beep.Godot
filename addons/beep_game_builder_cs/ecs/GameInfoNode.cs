using Godot;
using Beep.GameBuilder;       // GameInfo resource

namespace Beep.ECS
{
    /// <summary>
    /// Drop-in node version of <see cref="GameBuilder.GameInfo"/>. Add this to a
    /// scene, set the fields in the inspector, and at <c>_Ready</c> it copies
    /// every value into <c>GameApp.Instance.Info</c> — so the existing
    /// <c>game_info.tres</c> path continues to work but you can also configure
    /// the game right from the scene tree.
    ///
    /// If a game_info.tres file already exists, values from the .tres take
    /// precedence (the node is a convenience editor, not the source of truth).
    /// To use the node AS the source of truth, leave game_info.tres absent.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameInfoNode : Node
    {
        // ── Identity ──
        [Export] public string GameName { get; set; } = "My Game";
        [Export] public string Version { get; set; } = "0.1.0";
        [Export] public string Developer { get; set; } = "";
        [Export] public string Description { get; set; } = "";

        // ── Display ──
        [ExportGroup("Display")]
        [Export] public int TargetResolutionWidth { get; set; } = 1280;
        [Export] public int TargetResolutionHeight { get; set; } = 720;
        [Export] public bool PixelArt { get; set; } = true;
        [Export] public int TargetFps { get; set; } = 60;

        // ── Theme / skin ──
        [ExportGroup("Skin")]
        /// <summary>Genre id (folder name under <c>catalogs/skins/</c>). Mirrors
        /// <see cref="GameBuilder.GameInfo.Genre"/> and is the cascade root for the
        /// theme/palette/geometry dropdowns below.</summary>
        [Export]
        public string GenreId
        {
            get => _genreId;
            set { _genreId = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); }
        }
        private string _genreId = "platformer";

        [Export]
        public string DefaultThemePreset
        {
            // Palette options depend on the selected theme — refresh so it re-cascades.
            get => _defaultThemePreset;
            set { _defaultThemePreset = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); }
        }
        private string _defaultThemePreset = "modern";

        [Export] public string PaletteName { get; set; } = "Default";
        [Export] public string GeometryProfileName { get; set; } = "As-Authored";

        // ── Scene paths ──
        [ExportGroup("Scene Paths")]
        [Export] public string MainMenuPath { get; set; } = "res://scenes/ui/main_menu.tscn";
        [Export] public string GameScenePath { get; set; } = "res://scenes/main/main.tscn";
        [Export] public string SettingsScenePath { get; set; } = "res://scenes/ui/settings_menu.tscn";
        [Export] public string GameOverScenePath { get; set; } = "res://scenes/ui/game_over.tscn";

        // ── Platformer tuning ──
        [ExportGroup("Platformer")]
        [Export] public float Gravity { get; set; } = 980f;
        [Export] public float JumpVelocity { get; set; } = -400f;

        // ── Movement ──
        [ExportGroup("Movement")]
        [Export] public float MoveSpeed { get; set; } = 200f;

        // ── Shooter ──
        [ExportGroup("Shooter")]
        [Export] public float FireRate { get; set; } = 0.2f;

        // ── Puzzle ──
        [ExportGroup("Puzzle")]
        [Export] public int GridWidth { get; set; } = 8;
        [Export] public int GridHeight { get; set; } = 8;
        [Export] public int TargetScore { get; set; } = 1000;

        /// <summary>Set to false to skip auto-pushing values into
        /// <c>GameApp.Instance.Info</c> at <c>_Ready</c>.</summary>
        [Export] public bool AutoApplyToGameInfo { get; set; } = true;

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;
            if (!AutoApplyToGameInfo) return;

            var app = GameApp.Instance;
            if (app == null) return;

            // Create a fresh GameInfo resource if none is loaded — the
            // node IS the first source of truth for this run.
            if (app.Info == null)
            {
                app.Info = new GameBuilder.GameInfo();
                GD.Print("[GameInfoNode] Created new GameInfo resource — no game_info.tres was loaded.");
            }
            var info = app.Info;

            // Copy every field. The resource's own exports win if they differ
            // (they were loaded from game_info.tres by GameApp at boot).
            if (info.GameName == "My Game" && GameName != "My Game") info.GameName = GameName;
            if (info.Version == "0.1.0" && Version != "0.1.0") info.Version = Version;
            if (string.IsNullOrEmpty(info.Developer) && !string.IsNullOrEmpty(Developer)) info.Developer = Developer;
            if (string.IsNullOrEmpty(info.Description) && !string.IsNullOrEmpty(Description)) info.Description = Description;
            if (info.TargetResolutionWidth == 1280 && TargetResolutionWidth != 1280) info.TargetResolutionWidth = TargetResolutionWidth;
            if (info.TargetResolutionHeight == 720 && TargetResolutionHeight != 720) info.TargetResolutionHeight = TargetResolutionHeight;
            if (info.PixelArt == true && PixelArt == false) info.PixelArt = PixelArt;
            if (info.TargetFps == 60 && TargetFps != 60) info.TargetFps = TargetFps;
            if (info.GenreId == "platformer" && GenreId != "platformer") info.GenreId = GenreId;
            if (info.DefaultThemePreset == "modern" && DefaultThemePreset != "modern") info.DefaultThemePreset = DefaultThemePreset;
            if (info.PaletteName == "Default" && PaletteName != "Default") info.PaletteName = PaletteName;
            if (string.IsNullOrEmpty(info.GeometryProfileName) && !string.IsNullOrEmpty(GeometryProfileName)) info.GeometryProfileName = GeometryProfileName;
            if (string.IsNullOrEmpty(info.MainMenuPath) && !string.IsNullOrEmpty(MainMenuPath)) info.MainMenuPath = MainMenuPath;
            if (string.IsNullOrEmpty(info.GameScenePath) && !string.IsNullOrEmpty(GameScenePath)) info.GameScenePath = GameScenePath;
            if (string.IsNullOrEmpty(info.SettingsScenePath) && !string.IsNullOrEmpty(SettingsScenePath)) info.SettingsScenePath = SettingsScenePath;
            if (string.IsNullOrEmpty(info.GameOverScenePath) && !string.IsNullOrEmpty(GameOverScenePath)) info.GameOverScenePath = GameOverScenePath;
            if (Mathf.Abs(info.Gravity - 980f) < 0.01f && Mathf.Abs(Gravity - 980f) > 0.01f) info.Gravity = Gravity;
            if (Mathf.Abs(info.JumpVelocity - (-400f)) < 0.01f && Mathf.Abs(JumpVelocity - (-400f)) > 0.01f) info.JumpVelocity = JumpVelocity;
            if (Mathf.Abs(info.MoveSpeed - 200f) < 0.01f && Mathf.Abs(MoveSpeed - 200f) > 0.01f) info.MoveSpeed = MoveSpeed;
            if (Mathf.Abs(info.FireRate - 0.2f) < 0.0001f && Mathf.Abs(FireRate - 0.2f) > 0.0001f) info.FireRate = FireRate;
            if (info.GridWidth == 8 && GridWidth != 8) info.GridWidth = GridWidth;
            if (info.GridHeight == 8 && GridHeight != 8) info.GridHeight = GridHeight;
            if (info.TargetScore == 1000 && TargetScore != 1000) info.TargetScore = TargetScore;

            // Point the genre-specific scene paths at this genre's screens, the same way the
            // generator and BeepGenreScene do. This is the third way GenreId reaches
            // GameInfo; without it, a project configured through this node keeps the
            // hardcoded defaults (which name the puzzle/platformer scenes) and every genre
            // would still finish a level on the puzzle end screen.
            if (UI.SkinCatalog.GetGenre(info.GenreId) is { } genre)
                GameBuilder.BeepGenreGenerator.ApplyNavWiring(info, genre);

            // Save the resource so the autoload can re-read it on restart.
            if (ResourceSaver.Save(info, GameBuilder.GameInfo.TresPath) == Error.Ok)
                GD.Print($"[GameInfoNode] Saved game_info.tres (auto-applied to GameApp.Info).");
        }

        // ── Inspector dropdowns ─────────────────────────────────────────────
        // Options are read from the skin catalog's folder tree at edit time.

        public override void _ValidateProperty(Godot.Collections.Dictionary property)
        {
            base._ValidateProperty(property);

            switch ((string)property["name"])
            {
                case nameof(GenreId):
                    UI.SkinPropertyHints.ApplyEnum(property, UI.SkinPropertyHints.GenreHint(_genreId));
                    break;
                case nameof(DefaultThemePreset):
                    UI.SkinPropertyHints.ApplyEnum(property, UI.SkinPropertyHints.ThemeHint(_genreId, _defaultThemePreset));
                    break;
                case nameof(PaletteName):
                    UI.SkinPropertyHints.ApplyEnum(property, UI.SkinPropertyHints.PaletteHint(_genreId, _defaultThemePreset, PaletteName));
                    break;
                case nameof(GeometryProfileName):
                    UI.SkinPropertyHints.ApplyEnum(property, UI.SkinPropertyHints.GeometryHint(_genreId, GeometryProfileName));
                    break;
            }
        }
    }
}
