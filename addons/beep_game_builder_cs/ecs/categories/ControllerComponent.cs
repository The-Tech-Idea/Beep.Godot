using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Base class for all controller components — PlatformerController,
    /// TopDownController, AIController, ShooterController, camera controllers.
    ///
    /// Exists purely to organize components into a category folder in Godot's
    /// Add Node dialog. In the tree they appear as:
    ///   EntityComponent → ControllerComponent → (all controllers)
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class ControllerComponent : EntityComponent { }
}
