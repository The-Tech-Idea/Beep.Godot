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

        private bool _missingInputWarned;

        /// <summary>True only when every named input action exists in the InputMap. Reading an
        /// absent action via Input.GetAxis/IsActionPressed spams a per-frame Godot error, so
        /// controllers gate their input on this and skip the frame when it's false — warning
        /// once, helpfully, the way GenreScreenComponent does (rather than the raw error spam).
        /// The generator installs these actions, so this only trips when a template is run
        /// before a project is generated.</summary>
        protected bool InputActionsAvailable(params string[] actions)
        {
            foreach (var action in actions)
            {
                if (InputMap.HasAction(action)) continue;
                if (!_missingInputWarned)
                {
                    GD.PushWarning($"[{Name}] Input action '{action}' is not in the InputMap — {GetType().Name} can't read input and will stay idle. Generate a project (the generator installs the input map) or add the action.");
                    _missingInputWarned = true;
                }
                return false;
            }
            return true;
        }
    }

}
