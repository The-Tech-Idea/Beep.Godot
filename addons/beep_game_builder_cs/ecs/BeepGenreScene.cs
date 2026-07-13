using Godot;
using Beep.ECS.UI;          // SkinCatalog, GenreDef, ThemePresetComponent
using Beep.GameBuilder;      // GameInfo

namespace Beep.ECS
{
    /// <summary>
    /// Picks a genre at design time and wires the rest at runtime.
    ///
    /// Drop a <see cref="BeepGenreScene"/> into any scene root, set
    /// <see cref="GenreId"/> in the inspector, and at <c>_Ready</c> this node will:
    ///
    /// 1. Resolve <c>catalogs/skins/&lt;GenreId&gt;/genre.json</c>.
    /// 2. Apply the genre's default theme + tuning to <c>GameApp.Info</c>.
    /// 3. If <see cref="AutoInstantiateMainScene"/> is true (default), load
    ///    the genre's <c>main_scene.tscn</c> and add it as a child of self —
    ///    so the genre's already-wired layout (player + HUD + levels) appears
    ///    under this node without any further work.
    /// 4. Drive a sibling <see cref="ThemePresetComponent"/> (if any) from
    ///    the resolved theme / palette / geometry.
    ///
    /// Replaces the file-writing <see cref="Beep.GameBuilder.BeepGenreGenerator"/>.
    /// No code generation — only scene composition via the engine's existing
    /// <c>PackedScene.Instantiate</c>.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BeepGenreScene : Node
    {
        // ── Exports ─────────────────────────────────────────────────────────

        /// <summary>Genre id (folder name under <c>catalogs/skins/</c>).
        /// Empty at design time = no-op.</summary>
        [Export] public string GenreId { get; set; } = "";

        /// <summary>Optional override. Empty = <c>genre.json#default_theme</c>.</summary>
        [Export] public string ThemePreset { get; set; } = "";

        /// <summary>Optional palette name. Empty = "Default" (no tint).</summary>
        [Export] public string PaletteName { get; set; } = "Default";

        /// <summary>Optional geometry profile name. Empty = "As-Authored".</summary>
        [Export] public string GeometryProfileName { get; set; } = "As-Authored";

        /// <summary>If true (default), load <c>genre.json#main_scene</c> and add it
        /// as a child at <c>_Ready</c>. Disable for sub-scenes that don't want
        /// the full genre layout instantiated.</summary>
        [Export] public bool AutoInstantiateMainScene { get; set; } = true;

        /// <summary>If true, <c>GameInfo.GameScenePath</c> is set to this scene's
        /// saved path. Disable for non-main scenes.</summary>
        [Export] public bool RegisterAsMainScene { get; set; } = true;

        // ── Signal ──────────────────────────────────────────────────────────

        /// <summary>Emitted after the genre wiring has run.</summary>
        [Signal] public delegate void GenreAppliedEventHandler();

        // ── Lifecycle ──────────────────────────────────────────────────────

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;   // skip in editor
            ApplyGenre();
        }

        /// <summary>Re-runs the wiring. Public so game code can re-tune mid-game.</summary>
        public void ApplyGenre()
        {
            if (string.IsNullOrEmpty(GenreId)) { EmitSignal(SignalName.GenreApplied); return; }

            var genre = SkinCatalog.GetGenre(GenreId);
            if (genre == null)
            {
                GD.PushWarning($"[BeepGenreScene] Genre '{GenreId}' not found in skin catalog.");
                return;
            }

            ApplyToGameInfo(genre);
            ApplyToSiblingTheme();
            if (AutoInstantiateMainScene) InstantiateMainScene(genre);

            EmitSignal(SignalName.GenreApplied);
        }

        // ── Wiring ─────────────────────────────────────────────────────────

        private void ApplyToGameInfo(GenreDef genre)
        {
            var app = GameApp.Instance;
            if (app?.Info == null) return;     // no autoload → skip silently

            app.Info.Genre = GameInfo.GenreFromId(GenreId);
            app.Info.DefaultThemePreset = string.IsNullOrEmpty(ThemePreset)
                ? genre.DefaultTheme : ThemePreset;
            if (!string.IsNullOrEmpty(PaletteName)) app.Info.PaletteName = PaletteName;
            if (!string.IsNullOrEmpty(GeometryProfileName))
                app.Info.GeometryProfileName = GeometryProfileName;
            ApplyTuning(app.Info, genre);

            if (RegisterAsMainScene)
            {
                string path = Owner?.SceneFilePath ?? SceneFilePath;
                if (!string.IsNullOrEmpty(path)) app.Info.GameScenePath = path;
            }
        }

        private void ApplyToSiblingTheme()
        {
            var parent = GetParent();
            if (parent == null) return;
            // Godot.Collections.Array is not IEnumerable<Node>, so use a manual scan.
            foreach (var child in parent.GetChildren())
            {
                if (child is ThemePresetComponent theme && child != this)
                {
                    theme.GenreName = GenreId;
                    theme.PresetName = string.IsNullOrEmpty(ThemePreset)
                        ? SkinCatalog.GetGenre(GenreId)?.DefaultTheme ?? ""
                        : ThemePreset;
                    theme.PaletteName = PaletteName;
                    theme.GeometryProfileName = GeometryProfileName;
                    return;
                }
            }
        }

        private void InstantiateMainScene(GenreDef genre)
        {
            if (string.IsNullOrEmpty(genre.MainScene)) return;

            // Genre's MainScene path comes from genre.json. The actual file lives
            // either at res://scenes/main/<file> (after a project has stamped one)
            // OR at the addon's own template path. Try the runtime path first;
            // fall back to the template so a fresh project works without setup.
            string scenePath = TryResolveMainScenePath(genre.MainScene);
            if (string.IsNullOrEmpty(scenePath)) return;

            var packed = ResourceLoader.Load<PackedScene>(scenePath);
            if (packed == null) return;

            var instance = packed.Instantiate();
            instance.Name = $"_{GenreId}Main";   // underscore prefix → sorts first
            AddChild(instance);
        }

        private static string TryResolveMainScenePath(string fileName)
        {
            // 1. Stamped into the user's project (the normal case after first run).
            string stamped = $"res://scenes/main/{fileName}";
            if (ResourceLoader.Exists(stamped)) return stamped;

            // 2. Ships with the addon as a template (first-run case).
            string template = $"res://addons/beep_game_builder_cs/templates/scenes/{fileName}";
            if (ResourceLoader.Exists(template)) return template;

            return "";
        }

        /// <summary>Copy the genre's tuning values into <paramref name="info"/>.
        /// Recognised keys: gravity, jump_velocity, move_speed, fire_rate,
        /// grid_width, grid_height, target_score. Missing keys are left untouched.</summary>
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
