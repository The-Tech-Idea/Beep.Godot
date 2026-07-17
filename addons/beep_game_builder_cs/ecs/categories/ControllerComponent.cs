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
        ///
        /// Attach the controller as a CHILD Node of the CharacterBody2D:
        ///
        ///     Player  (CharacterBody2D)
        ///     └─ Controller  (Node, this script)
        ///
        /// It cannot be the body's own script: this type derives from Node, so C# can
        /// never treat it as a CharacterBody2D. The genre templates used to attach it
        /// directly to the body, which left this returning null and the player unable
        /// to move.</summary>
        protected CharacterBody2D? ResolveBody2D()
        {
            if (GetParent() is CharacterBody2D body) return body;
            GD.PushWarning($"[{Name}] No CharacterBody2D parent — add this controller as a child of the body.");
            return null;
        }
    }

}
