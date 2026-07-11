using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Base class for all UI components — buttons, menus, dialogs, themes, effects,
    /// HUD elements, settings, tables, notifications, etc.
    ///
    /// Exists purely to organize components into a category folder in Godot's
    /// Add Node dialog. In the tree they appear as:
    ///   EntityComponent → UIComponent → (all UI components)
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class UIComponent : EntityComponent { }
}
