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
    public abstract partial class ControllerComponent : EntityComponent
    {
        /// <summary>Resolve the CharacterBody2D this controller drives.
        /// Supports two attachment patterns:
        /// (a) Controller attached directly as the CharacterBody2D's own script.
        /// (b) Controller added as a child Node under a CharacterBody2D parent.
        /// This addon uses both patterns — generated template scenes use (a), ability stacks use (b).</summary>
        protected CharacterBody2D? ResolveBody2D() => this as CharacterBody2D ?? GetParent() as CharacterBody2D;
    }

}
