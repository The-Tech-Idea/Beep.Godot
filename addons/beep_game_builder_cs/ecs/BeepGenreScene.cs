using Godot;
using Beep.ECS.UI;          // SkinCatalog, GenreDef, ThemePresetComponent
using Beep.GameBuilder;      // GameInfo

namespace Beep.ECS
{
    /// <summary>
    /// Genre-aware scene orchestrator. Drop one of these into a scene root, set
    /// <see cref="GenreId"/>, and at runtime it reads the matching <c>genre.json</c>
    /// from <c>catalogs/skins/</c> and applies:
    ///
    /// 1. <see cref="GameInfo.DefaultThemePreset"/> — from genre.DefaultTheme
    ///    (or the explicit override on this component).
    /// 2. Genre-specific tuning (gravity, jump_velocity, move_speed, fire_rate,
    ///    grid_width, grid_height, target_score) → GameInfo fields.
    /// 3. <see cref="GameInfo.GameScenePath"/> — set to this scene's saved path
    ///    when <see cref="RegisterAsMainScene"/> is true.
    /// 4. A sibling <see cref="ThemePresetComponent"/> (if present) gets its
    ///    GenreName / PresetName / PaletteName / GeometryProfileName driven
    ///    from the resolved GameInfo.
    ///
    /// Replaces the file-writing <see cref="Beep.GameBuilder.BeepGenreGenerator"/>.
    /// No code generation — the scene is whatever you composed from components;
    /// this just wires it.
    ///
    /// Required: the <c>GameApp</c> autoload must be registered for the runtime
    /// path to take effect. The dock's App tab shows autoload status.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BeepGenreScene : Node
    {
        // ── Exports ─────────────────────────────────────────────────────────

        /// <summary>Genre id (must match a folder under catalogs/skins/<genre>/).
        /// Leave empty to skip runtime wiring — the scene loads with whatever
        /// the inspector already set.</summary>
        [Export] public string GenreId { get; set; } = "";

        /// <summary>Optional override — defaults to the genre's default_theme.
        /// Set to "modern" or "candy" etc. to pin a specific theme for this scene.</summary>
        [Export] public string DefaultThemePreset { get; set; } = "";

        /// <summary>Optional palette name. Empty = "Default" (no tint).</summary>
        [Export] public string PaletteName { get; set; } = "Default";

        /// <summary>Optional geometry profile name. Empty = genre's geometry or "As-Authored".</summary>
        [Export] public string GeometryProfileName { get; set; } = "As-Authored";

        /// <summary>Optional override — game name shown in UI. Empty = GameInfo.GameName.</summary>
        [Export] public string GameName { get; set; } = "";

        /// <summary>If true, sets GameInfo.GameScenePath to this scene's saved path at _Ready.
        /// Disable for sub-scenes that are not the main scene.</summary>
        [Export] public bool RegisterAsMainScene { get; set; } = true;

        // ── Signal ──────────────────────────────────────────────────────────

        /// <summary>Emitted once at _Ready after all wiring has been applied.</summary>
        [Signal] public delegate void GenreAppliedEventHandler();

        // ── Lifecycle ──────────────────────────────────────────────────────

        public override void _Ready()
        {
            // [Tool] components run in the editor too. Skip the runtime wiring
            // path while editing — it has side effects (writes GameInfo, drives
            // sibling ThemePresetComponent).
            if (Engine.IsEditorHint()) return;
            ApplyGenre();
        }

        /// <summary>Re-applies the genre wiring. Safe to call multiple times;
        /// idempotent. Public so game code can re-tune mid-game (e.g. on locale
        /// change or after a save migration).</summary>
        public void ApplyGenre()
        {
            var app = GameApp.Instance;
            if (app == null)
            {
                GD.PushWarning($"[BeepGenreScene] No GameApp autoload — scene '{SceneFilePath}' will not be wired.");
                return;
            }
            if (app.Info == null)
            {
                GD.PushWarning($"[BeepGenreScene] GameApp.Info is null — set res://game_info.tres as the GameInfo autoload.");
                return;
            }

            // Empty GenreId is allowed — the user just didn't specify a genre.
            // Skip the genre-driven wiring and leave whatever's already in GameInfo.
            if (string.IsNullOrEmpty(GenreId)) { EmitSignal(SignalName.GenreApplied); return; }

            var genre = SkinCatalog.GetGenre(GenreId);
            if (genre == null)
            {
                GD.PushWarning($"[BeepGenreScene] Genre '{GenreId}' not found in skin catalog. Expected a folder under " +
                               $"res://addons/beep_game_builder_cs/catalogs/skins/{GenreId}/");
                return;
            }

            // 1. GameInfo.Genre (C# enum bridge from file-system id string).
            app.Info.Genre = GameInfo.GenreFromId(GenreId);

            // 2. Theme / palette / geometry — explicit overrides win, else genre defaults.
            app.Info.DefaultThemePreset = string.IsNullOrEmpty(DefaultThemePreset)
                ? genre.DefaultTheme
                : DefaultThemePreset;
            if (!string.IsNullOrEmpty(PaletteName)) app.Info.PaletteName = PaletteName;
            if (!string.IsNullOrEmpty(GeometryProfileName)) app.Info.GeometryProfileName = GeometryProfileName;

            // 3. Genre-specific tuning (gravity, jump_velocity, ...).
            ApplyTuning(app.Info, genre);

            // 4. Optional game-name override.
            if (!string.IsNullOrEmpty(GameName)) app.Info.GameName = GameName;

            // 5. Optional main-scene registration — only meaningful at runtime for the
            //    scene that's currently loaded as main.
            if (RegisterAsMainScene)
            {
                string scenePath = Owner?.SceneFilePath ?? SceneFilePath;
                if (!string.IsNullOrEmpty(scenePath))
                    app.Info.GameScenePath = scenePath;
            }

            // 6. Drive a sibling ThemePresetComponent (if any) from the resolved values.
            var theme = FindSiblingThemePreset();
            if (theme != null)
            {
                theme.GenreName = GenreId;
                theme.PresetName = app.Info.DefaultThemePreset;
                theme.PaletteName = app.Info.PaletteName;
                theme.GeometryProfileName = app.Info.GeometryProfileName;
            }

            EmitSignal(SignalName.GenreApplied);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        /// <summary>Find the first <see cref="ThemePresetComponent"/> sibling of
        /// this node. Returns null if none — in which case nothing is themed by
        /// this scene (other components in the scene may have their own
        /// <see cref="ThemePresetComponent"/> or use the autoload-driven
        /// <see cref="GameInfoBinder"/>).</summary>
        private ThemePresetComponent? FindSiblingThemePreset()
        {
            var parent = GetParent();
            if (parent == null) return null;
            foreach (var child in parent.GetChildren())
            {
                if (child is ThemePresetComponent tpc && child != this)
                    return tpc;
            }
            return null;
        }

        /// <summary>Copy the genre's tuning values into <paramref name="info"/>.
        /// All keys are optional; missing keys leave the GameInfo field untouched.
        /// Recognised keys (all live in genre.json's "tuning" block):
        ///   gravity        → info.Gravity
        ///   jump_velocity  → info.JumpVelocity
        ///   move_speed     → info.MoveSpeed
        ///   fire_rate      → info.FireRate
        ///   grid_width     → info.GridWidth
        ///   grid_height    → info.GridHeight
        ///   target_score   → info.TargetScore</summary>
        private static void ApplyTuning(GameInfo info, GenreDef genre)
        {
            if (genre.Tuning.Count == 0) return;
            if (genre.Tuning.TryGetValue("gravity", out var g)) info.Gravity = g.AsSingle();
            if (genre.Tuning.TryGetValue("jump_velocity", out var j)) info.JumpVelocity = j.AsSingle();
            if (genre.Tuning.TryGetValue("move_speed", out var m)) info.MoveSpeed = m.AsSingle();
            if (genre.Tuning.TryGetValue("fire_rate", out var f)) info.FireRate = f.AsSingle();
            if (genre.Tuning.TryGetValue("grid_width", out var gw)) info.GridWidth = gw.AsInt32();
            if (genre.Tuning.TryGetValue("grid_height", out var gh)) info.GridHeight = gh.AsInt32();
            if (genre.Tuning.TryGetValue("target_score", out var ts)) info.TargetScore = ts.AsInt32();
        }
    }
}