using Godot;
using Beep.ECS.UI;          // SkinCatalog, GenreDef, ThemePresetComponent
using Beep.GameBuilder;      // GameInfo

namespace Beep.ECS
{
    /// <summary>
    /// Picks a genre at design time and wires the shared MainGame shell at runtime.
    ///
    /// Drop a <see cref="BeepGenreScene"/> into any scene root, set
    /// <see cref="GenreId"/> in the inspector, and at <c>_Ready</c> this node will:
    ///
    /// 1. Resolve <c>catalogs/skins/&lt;GenreId&gt;/genre.json</c>.
    /// 2. Apply the genre's default theme + tuning to <c>GameApp.Info</c>.
    /// 3. If <see cref="AutoInstantiateMainScene"/> is true (default), load the
    ///    resolved game scene. In current projects this is the shared MainGame
    ///    shell, which then loads the genre's level content under stable roots.
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

        /// <summary>If true (default), load the resolved game scene and add it as a child
        /// at <c>_Ready</c>. Disable for sub-scenes that only want genre/theme wiring.</summary>
        [Export] public bool AutoInstantiateMainScene { get; set; } = true;

        /// <summary>If true, <c>GameInfo.GameScenePath</c> is pointed at the shared
        /// MainGame shell. Disable for non-game configuration scenes.</summary>
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
            if (app == null)
            {
                if (!Engine.IsEditorHint())
                    GD.PushWarning($"[{Name}] BeepGenreScene found no GameApp autoload — genre/theme/scene-path wiring into GameInfo is skipped. Enable the GameApp autoload so the scene picks up this genre's config.");
                return;
            }

            var info = app.ActiveInfo;
            info.GenreId = GenreId;
            info.DefaultThemePreset = string.IsNullOrEmpty(ThemePreset)
                ? genre.DefaultTheme : ThemePreset;
            if (!string.IsNullOrEmpty(PaletteName)) info.PaletteName = PaletteName;
            if (!string.IsNullOrEmpty(GeometryProfileName))
                info.GeometryProfileName = GeometryProfileName;
            // Shared with the generator rather than forked — the local copy recognised only
            // the 7 gameplay keys, so weather/season/save tuning never reached this path.
            BeepGenreGenerator.ApplyTuning(info, genre);

            // Point the genre-specific scene paths at THIS genre's screens, exactly as the
            // generator does. Without this, a project set up the README way (drop in a
            // BeepGenreScene instead of running Generate) keeps GameInfo's hardcoded
            // defaults — which name the puzzle/platformer scenes — so every genre would
            // still finish a level on the puzzle end screen.
            BeepGenreGenerator.ApplyNavWiring(info, genre);

            if (RegisterAsMainScene)
            {
                info.GameScenePath = GameInfo.DefaultGameScenePath;
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
            // Load the shared shell when it exists. GameInfo.ResolveGameScenePath still
            // falls back to the legacy genre scene only for unstamped projects.
            string scenePath = GameApp.Instance?.ActiveInfo.ResolveGameScenePath() ?? "";
            if (string.IsNullOrEmpty(scenePath)) return;

            var packed = ResourceLoader.Load<PackedScene>(scenePath);
            if (packed == null)
            {
                if (!Engine.IsEditorHint())
                    GD.PushWarning($"[{Name}] BeepGenreScene resolved '{scenePath}' but failed to load it as a PackedScene — the genre's main layout will not appear.");
                return;
            }

            string childName = "_MainGame";   // underscore prefix sorts first
            // Idempotent: ApplyGenre is public and documented re-runnable, so remove a prior
            // main-scene instance before adding a new one — otherwise a second call stacks a
            // duplicate genre layout. RemoveChild is immediate (frees the name), then QueueFree.
            if (GetNodeOrNull(childName) is { } existing)
            {
                RemoveChild(existing);
                existing.QueueFree();
            }
            var instance = packed.Instantiate();
            instance.Name = childName;
            AddChild(instance);
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
