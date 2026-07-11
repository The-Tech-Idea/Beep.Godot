using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Runtime binder between the <c>GameInfo</c> autoload and scene nodes.
    /// Attach as a child of any UI root. On _Ready it reads GameInfo and pushes
    /// values into the configured nodes — so a dev edits game_info.tres ONCE and
    /// every menu reflects it. Without this, scene .tscn files hold baked literals.
    ///
    /// What it binds (each optional — leave the NodePath empty to skip):
    /// - Game name  → a Label (e.g. the main-menu title)
    /// - Version    → a Label (e.g. "v0.1.0")
    /// - Genre      → a Label (display name)
    /// - Theme      → a sibling ThemePresetComponent (sets Preset from GameInfo.DefaultThemePreset)
    /// - Window     → the OS window title (set to GameName)
    ///
    /// Usage in a scene:
    ///   [node name="GameInfoBinder" type="Node" parent="."]
    ///   script = GameInfoBinder
    ///   title_label_path = NodePath("Center/MenuVBox/TitleLabel")
    ///   version_label_path = NodePath("Center/MenuVBox/VersionLabel")
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameInfoBinder : UIComponent
    {
        [Export] public NodePath TitleLabelPath { get; set; } = new("");
        [Export] public NodePath VersionLabelPath { get; set; } = new("");
        [Export] public NodePath GenreLabelPath { get; set; } = new("");
        /// <summary>Path to the ThemePresetComponent sibling whose Preset is driven by GameInfo.</summary>
        [Export] public NodePath ThemeComponentPath { get; set; } = new("");
        [Export] public bool SetWindowTitle { get; set; } = false;
        /// <summary>If true and the title label is set, prefix it to the existing text (useful for results screens).</summary>
        [Export] public bool AppendGameName { get; set; } = false;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(Bind));
        }

        public void Bind()
        {
            var info = GameBuilder.GameInfo.Instance;
            if (info == null)
            {
                GD.PushWarning("[GameInfoBinder] No GameInfo autoload found — scene will show placeholder values.");
                return;
            }

            var parent = GetParent();

            // Title.
            if (!TitleLabelPath.IsEmpty && parent.GetNodeOrNull<Label>(TitleLabelPath) is { } title)
                title.Text = AppendGameName ? $"{title.Text} — {info.GameName}" : info.GameName;

            // Version.
            if (!VersionLabelPath.IsEmpty && parent.GetNodeOrNull<Label>(VersionLabelPath) is { } ver)
                ver.Text = $"v{info.Version}";

            // Genre display.
            if (!GenreLabelPath.IsEmpty && parent.GetNodeOrNull<Label>(GenreLabelPath) is { } genre)
                genre.Text = info.Genre.ToString();

            // Theme + palette + geometry + skin — drive the sibling ThemePresetComponent from GameInfo/GameApp.
            if (!ThemeComponentPath.IsEmpty && parent.GetNodeOrNull<ThemePresetComponent>(ThemeComponentPath) is { } theme)
            {
                // File-based: pass the genre + theme name directly. The catalog resolves it.
                theme.GenreName = info.Genre.ToString().ToLowerInvariant();
                theme.PresetName = info.DefaultThemePreset.ToLowerInvariant();
                theme.PaletteName = info.PaletteName;
                theme.GeometryProfileName = info.GeometryProfileName;
                // Push the UISkin from GameApp if one is set there.
                var app = GameApp.Instance;
                if (app != null && app.Skin != null) theme.Skin = app.Skin;
            }

            // OS window title.
            if (SetWindowTitle && GetTree().Root is Window root)
                root.Title = info.GameName;
        }
    }
}
