using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Base class for a component that IS A SCREEN — the root Control of a menu, an overlay, or a
    /// laid-out region — rather than a droppable child node.
    ///
    /// WHY THIS EXISTS
    /// ---------------
    /// <see cref="UIComponent"/> derives from <see cref="EntityComponent"/>, which is a
    /// <see cref="Node"/>. That is right for the vast majority of UI components: you drop a
    /// ThemePresetComponent or a ToastComponent into a scene as a child and it acts on its
    /// surroundings. It is wrong for the handful that ARE the screen.
    ///
    /// Three shipped scenes had exactly that mismatch — `LoadGameMenu` and `SaveGameMenu` are the
    /// ROOTS of their scenes and must be Controls or their anchored children have nothing to size
    /// against, and `Alerts` is a laid-out region carrying `size_flags_vertical`. Godot accepts a
    /// Node-derived script on a Control node without complaint, so all three loaded and rendered
    /// correctly; what broke was C#: the managed object is a Node standing in for a Control, so
    /// `GetNode&lt;Control&gt;` fails and the layout API is unreachable. That is the same class of
    /// defect that made the kit's Control-shaped widgets unusable from a project, and it wants the
    /// same answer — inherit the type you actually are.
    ///
    /// This is deliberately NOT a change to UIComponent. Re-basing that would drag ~53 components
    /// that are correctly Nodes onto Control, giving each of them a rect, a focus mode and a mouse
    /// filter they have no use for.
    ///
    /// Mirrors EntityComponent's grouping surface so a screen keeps the same authoring vocabulary.
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class UIScreenComponent : Godot.Control
    {
        /// <summary>Group this screen joins on entering the tree. Same meaning as
        /// <see cref="EntityComponent.ComponentGroup"/>.</summary>
        [Export] public string ComponentGroup { get; set; } = "";

        /// <summary>Switch off without removing. Screens honour it by hiding, because a Control
        /// that stops processing but keeps drawing is the confusing half-state.</summary>
        [Export]
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                if (IsNodeReady()) Visible = value;
            }
        }
        private bool _isActive = true;

        public override void _EnterTree()
        {
            if (!string.IsNullOrEmpty(ComponentGroup)) AddToGroup(ComponentGroup);
        }

        public override void _ExitTree()
        {
            if (!string.IsNullOrEmpty(ComponentGroup)) RemoveFromGroup(ComponentGroup);
        }

        /// <summary>Find a component of type T among this screen's own children. The Node-based
        /// version searches SIBLINGS, because a component sits beside the things it acts on; a
        /// screen owns its contents instead.</summary>
        protected T? FindChildComponent<T>() where T : class
        {
            foreach (var child in GetChildren())
                if (child is T hit) return hit;
            return null;
        }
    }
}
