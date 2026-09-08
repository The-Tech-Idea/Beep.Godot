using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Base for the grid HUD panels. Every one of them binds scene-authored
    /// controls when they exist and only generates a default layout when
    /// GenerateControlsWhenPathsEmpty says to, so a project's own authored HUD
    /// always wins over the generated one.
    ///
    /// It owns the three pieces each panel used to carry its own copy of: the
    /// editor-owner stamp a generated node needs to survive a scene save, the
    /// node-name sanitiser, and the three-tier authored-control lookup
    /// (exported NodePath, then a named child, then a named node under the
    /// parent). None of that is panel-specific - it is the same structural
    /// bootstrap whether the panel lists resources, jobs, workers, or builds.
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class GridPanelComponent : Control
    {
        /// <summary>Whether the panel also builds itself while the editor is open, not only at runtime.</summary>
        [Export] public bool BuildInEditor { get; set; } = true;

        /// <summary>
        /// Opt-in to a generated default layout. Left false, a panel with no
        /// authored controls draws nothing rather than inventing a layout over
        /// a scene the author is still building.
        /// </summary>
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        /// <summary>
        /// A node created at runtime is invisible to the scene file unless the
        /// edited scene root owns it - without this stamp the generated layout
        /// disappears the moment the scene is saved and reloaded.
        /// </summary>
        protected void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }

        /// <summary>
        /// The authored control for a slot: the exported path first, then a
        /// child of this panel by name, then a node of that name under the
        /// panel's parent - so the panel can sit beside the controls it drives
        /// instead of having to own them.
        ///
        /// Several names search by TIER, not name by name: every candidate name
        /// is tried as a child before any is tried under the parent, which is
        /// the order the resource bar has always looked for its authored row
        /// and then its generated one.
        /// </summary>
        protected T? FindControl<T>(NodePath path, params string[] names) where T : Node
        {
            if (!path.IsEmpty && GetNodeOrNull<T>(path) is { } fromPath)
                return fromPath;

            foreach (string name in names)
            {
                if (FindChild(name, recursive: true, owned: false) is T child)
                    return child;
            }

            Node? parent = GetParent();
            if (parent == null)
                return null;

            foreach (string name in names)
            {
                if (parent.FindChild(name, recursive: true, owned: false) is T sibling)
                    return sibling;
            }

            return null;
        }

        /// <summary>
        /// A node name cannot carry the characters a resource, job, build or
        /// worker id legitimately can.
        /// </summary>
        protected static string SafeName(string value, string fallback)
        {
            string result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');
            // Separators and the colon explicitly: GetInvalidFileNameChars is
            // platform-dependent and does not include the backslash on Unix,
            // but a Godot node name may not carry any of them anywhere - and a
            // row key can be a whole node path.
            return result.Replace(' ', '_').Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        }
    }
}
