using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Base class for components that react to bodies entering/leaving an Area2D —
    /// interaction zones, hazards, pickups, door switches, level-transition gates,
    /// lap gates, melee hitboxes.
    ///
    /// It is the Area2D counterpart to <see cref="ControllerComponent.ResolveBody2D"/>:
    /// attach the component as a CHILD Node of the Area2D it should watch,
    ///
    ///     Zone  (Area2D)
    ///     └─ Trigger  (Node, an AreaTriggerComponent subclass)
    ///
    /// Seven components used to hand-roll <c>GetParent() as Area2D</c> + a manual
    /// BodyEntered/BodyExited subscription. Two were BROKEN by exactly that pattern —
    /// parented to a CharacterBody2D, the cast returned null, subscription never
    /// happened, and the component did nothing without a word. This base resolves the
    /// Area2D once, <b>warns</b> when the parent is wrong (never fails silently), wires
    /// the two body signals, and tears them down in <c>_ExitTree</c>. Subclasses override
    /// <see cref="OnBodyEntered"/> / <see cref="OnBodyExited"/> and nothing else.
    ///
    /// In the Add Node tree these appear as:
    ///   EntityComponent → AreaTriggerComponent → (all body triggers)
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class AreaTriggerComponent : EntityComponent
    {
        /// <summary>The Area2D this trigger watches. Null until <c>_Ready</c>, and null
        /// (with a warning) when the parent is not an Area2D. Exposed so subclasses can
        /// query overlaps or drive the collision shape directly.</summary>
        protected Area2D? TriggerArea { get; private set; }

        /// <summary>Resolves the parent Area2D and subscribes to its body signals.
        /// A subclass that overrides <c>_Ready</c> MUST call <c>base._Ready()</c>, or the
        /// trigger never wires up.</summary>
        public override void _Ready()
        {
            base._Ready();
            TriggerArea = ResolveArea2D();
            if (TriggerArea == null) return;
            TriggerArea.BodyEntered += OnBodyEntered;
            TriggerArea.BodyExited += OnBodyExited;
        }

        /// <summary>Resolve the parent Area2D, warning by name when it is the wrong type
        /// instead of returning a silent null. Mirrors
        /// <see cref="ControllerComponent.ResolveBody2D"/>.</summary>
        protected Area2D? ResolveArea2D()
        {
            if (GetParent() is Area2D area) return area;
            var parent = GetParent();
            GD.PushWarning(
                $"[{Name}] ({GetType().Name}): no Area2D parent — this trigger cannot detect " +
                $"bodies and does nothing. Parent is " +
                $"'{parent?.Name.ToString() ?? "<none>"}' ({parent?.GetType().Name ?? "null"}). " +
                "Add this component as a child of an Area2D.");
            return null;
        }

        /// <summary>A body entered the trigger Area2D. Override to react. Base does nothing.</summary>
        protected virtual void OnBodyEntered(Node2D body) { }

        /// <summary>A body left the trigger Area2D. Override to react. Base does nothing.</summary>
        protected virtual void OnBodyExited(Node2D body) { }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (TriggerArea == null) return;
            TriggerArea.BodyEntered -= OnBodyEntered;
            TriggerArea.BodyExited -= OnBodyExited;
        }
    }
}
