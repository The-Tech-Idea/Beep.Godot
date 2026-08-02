using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Runtime binder between the active <c>GameInfo</c> resource and scene nodes.
    /// Attach as a child of any UI root. On _Ready it reads GameApp.Info and pushes
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
                GD.PushWarning("[GameInfoBinder] No GameApp.Info resource found — scene will show placeholder values.");
                return;
            }

            var parent = GetParent();

            // Title.
            if (Resolve<Label>(parent, TitleLabelPath, "TitleLabelPath") is { } title)
                title.Text = AppendGameName ? $"{title.Text} — {info.GameName}" : info.GameName;

            // Version.
            if (Resolve<Label>(parent, VersionLabelPath, "VersionLabelPath") is { } ver)
                ver.Text = $"v{info.Version}";

            // Genre display — show the catalog's display name, falling back to the raw
            // id if the genre folder isn't loaded.
            if (Resolve<Label>(parent, GenreLabelPath, "GenreLabelPath") is { } genre)
                genre.Text = SkinCatalog.GetGenre(info.GenreId)?.DisplayName ?? info.GenreId;

            // Theme + palette + geometry + skin — drive the sibling ThemePresetComponent from GameInfo/GameApp.
            if (Resolve<ThemePresetComponent>(parent, ThemeComponentPath, "ThemeComponentPath") is { } theme)
            {
                // One game, one skin. The genre/theme/palette live in ONE global
                // (SkinCatalog.ActiveGenre and friends); this just publishes them from
                // GameInfo. No per-scene override, no rule about which wins.
                SkinCatalog.SetActiveSkin(info.GenreId,
                                          info.DefaultThemePreset.ToLowerInvariant(),
                                          info.PaletteName,
                                          info.GeometryProfileName);
                // Re-apply explicitly. The component already themed itself in _Ready, before
                // this ran, so publishing alone leaves it on its fallback skin — which is what
                // made an rpg project render in platformer/modern. Assigning GenreName used to
                // trigger this as a side effect of the setter; a plain global has no setter.
                theme.ApplyTheme();
                // Push the UISkin from GameApp if one is set there.
                var app = GameApp.Instance;
                if (app != null && app.Skin != null) theme.Skin = app.Skin;
            }

            // OS window title.
            if (SetWindowTitle && GetTree().Root is Window root)
                root.Title = info.GameName;
        }

        /// <summary>Resolve a node under the parent, warning when a SET path fails to resolve
        /// (the classic "path set but wrong node/type → binding silently dropped" case). An empty
        /// path is an intentional skip and stays silent.</summary>
        private T? Resolve<T>(Node parent, NodePath path, string exportName) where T : Node
        {
            if (path.IsEmpty) return null;
            var node = parent.GetNodeOrNull<T>(path);
            if (node != null) return node;

            // Fall back to the path's last segment as a NAME. These paths are baked into every
            // shipped .tscn ("Margin/VBox/Header/TitleLabel"), so restyling a screen — or opening a
            // project generated from an older template — breaks the path while the node itself is
            // still right there under a different parent. Matching on name binds both layouts.
            string leaf = path.GetNameCount() > 0 ? path.GetName(path.GetNameCount() - 1) : "";
            if (!string.IsNullOrEmpty(leaf)
                && parent.FindChild(leaf, recursive: true, owned: false) is T found)
                return found;

            GD.PushWarning($"[{Name}] GameInfoBinder.{exportName} = '{path}' did not resolve to a {typeof(T).Name} under '{parent.Name}' (no node named '{leaf}' either) — that binding is skipped. Fix the path or clear it.");
            return null;
        }
    }
}
