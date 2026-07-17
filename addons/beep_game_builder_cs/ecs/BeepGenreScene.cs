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
        [Export]
        public string GenreId
        {
            // Theme/palette/geometry options all hang off the genre — refresh the list
            // so those dropdowns re-cascade.
            get => _genreId;
            set { _genreId = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); }
        }
        private string _genreId = "";

        /// <summary>Optional override. Empty = <c>genre.json#default_theme</c>.</summary>
        [Export]
        public string ThemePreset
        {
            get => _themePreset;
            set { _themePreset = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); }
        }
        private string _themePreset = "";

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

            app.Info.GenreId = GenreId;
            app.Info.DefaultThemePreset = string.IsNullOrEmpty(ThemePreset)
                ? genre.DefaultTheme : ThemePreset;
            if (!string.IsNullOrEmpty(PaletteName)) app.Info.PaletteName = PaletteName;
            if (!string.IsNullOrEmpty(GeometryProfileName))
                app.Info.GeometryProfileName = GeometryProfileName;
            // Shared with the generator rather than forked — the local copy recognised only
            // the 7 gameplay keys, so weather/season/save tuning never reached this path.
            BeepGenreGenerator.ApplyTuning(app.Info, genre);

            // Point the genre-specific scene paths at THIS genre's screens, exactly as the
            // generator does. Without this, a project set up the README way (drop in a
            // BeepGenreScene instead of running Generate) keeps GameInfo's hardcoded
            // defaults — which name the puzzle/platformer scenes — so every genre would
            // still finish a level on the puzzle end screen.
            BeepGenreGenerator.ApplyNavWiring(app.Info, genre);

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

        // ── Inspector dropdowns ─────────────────────────────────────────────
        // Values come from the skin catalog at edit time. GenreId and ThemePreset
        // use EnumSuggestion (editable) because "" is a meaningful value for both —
        // a closed dropdown could not express it.

        public override void _ValidateProperty(Godot.Collections.Dictionary property)
        {
            base._ValidateProperty(property);

            switch ((string)property["name"])
            {
                case nameof(GenreId):
                    UI.SkinPropertyHints.ApplyEnumSuggestion(property, UI.SkinPropertyHints.GenreHint(_genreId));
                    break;
                case nameof(ThemePreset):
                    UI.SkinPropertyHints.ApplyEnumSuggestion(property, UI.SkinPropertyHints.ThemeHint(_genreId, _themePreset));
                    break;
                case nameof(PaletteName):
                    UI.SkinPropertyHints.ApplyEnum(property, UI.SkinPropertyHints.PaletteHint(_genreId, _themePreset, PaletteName));
                    break;
                case nameof(GeometryProfileName):
                    UI.SkinPropertyHints.ApplyEnum(property, UI.SkinPropertyHints.GeometryHint(_genreId, GeometryProfileName));
                    break;
            }
        }
    }
}
