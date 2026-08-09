using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Boids flocking — Reynolds' three rules (separation, alignment, cohesion) for groups that
    /// move like a flock, school, or swarm instead of independent chasers. Attach as a child of a
    /// CharacterBody2D; every flockmate shares <see cref="FlockGroup"/>.
    ///
    ///     Bird  (CharacterBody2D)
    ///     └─ Flock  (FlockingComponent, FlockGroup = "birds")
    ///
    /// Uses <see cref="SteeringBehavior.Limit"/> to cap the blended result. Neighbors are found by
    /// scanning <see cref="FlockGroup"/> each physics tick and filtered by distance — fine for tens
    /// of agents; for hundreds, gate NeighborRadius down or move to a spatial hash.
    ///
    /// In the Add Node tree: EntityComponent → GameplayComponent → FlockingComponent
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class FlockingComponent : GameplayComponent
    {
        [ExportGroup("Flock")]
        /// <summary>Group holding every flockmate (the parent bodies, not components). Each member's
        /// EntityGroup or SpawnerComponent.SpawnGroup should add the body to this group.</summary>
        [Export] public string FlockGroup { get; set; } = "";
        [Export] public float MaxSpeed { get; set; } = 150f;
        /// <summary>How far a body can see its flockmates.</summary>
        [Export] public float NeighborRadius { get; set; } = 120f;
        /// <summary>Bodies closer than this push apart (separation).</summary>
        [Export] public float SeparationRadius { get; set; } = 40f;

        [ExportGroup("Rule Weights")]
        [Export] public float SeparationWeight { get; set; } = 1.5f;
        [Export] public float AlignmentWeight { get; set; } = 1.0f;
        [Export] public float CohesionWeight { get; set; } = 1.0f;
        /// <summary>How sharply velocity turns toward the blended desired velocity (0-1 per tick).</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float SteerLerp { get; set; } = 0.15f;

        private CharacterBody2D? _body;
        private Vector2 _velocity;
        private bool _warnedNoGroup;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as CharacterBody2D;
            if (Engine.IsEditorHint()) return;
            if (_body == null)
                GD.PushWarning($"[{Name}] FlockingComponent needs a CharacterBody2D parent to move; got '{GetParent()?.GetType().Name ?? "null"}'. The agent will stay inert.");
            if (string.IsNullOrEmpty(FlockGroup) && !Engine.IsEditorHint())
                GD.PushWarning($"[{Name}] FlockGroup is empty — the agent has no flockmates to flock with and will fly straight. Set FlockGroup to the group its flockmates share.");
            _velocity = Vector2.FromAngle(GD.Randf() * Mathf.Tau) * MaxSpeed;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive || _body == null) return;

            Vector2 desired = ComputeDesired();
            _velocity = _velocity.Lerp(desired, SteerLerp);
            _velocity = SteeringBehavior.Limit(_velocity, MaxSpeed);

            _body.Velocity = _velocity;
            _body.MoveAndSlide();
            _velocity = _body.Velocity;   // collisions may have altered it; keep the truth
        }

        /// <summary>Blend the three rules into one desired velocity. Zero neighbors → keep current
        /// heading so a lone agent doesn't snap to a stop.</summary>
        private Vector2 ComputeDesired()
        {
            if (_body == null || string.IsNullOrEmpty(FlockGroup)) return _velocity;

            Vector2 pos = _body.GlobalPosition;
            Vector2 separation = Vector2.Zero;
            Vector2 alignment = Vector2.Zero;
            Vector2 cohesionCenter = Vector2.Zero;
            int neighbors = 0;

            foreach (var n in GetTree().GetNodesInGroup(FlockGroup))
            {
                if (n is not Node2D other || other == _body || !GodotObject.IsInstanceValid(other)) continue;
                float dist = pos.DistanceTo(other.GlobalPosition);
                if (dist > NeighborRadius) continue;

                neighbors++;
                alignment += other is CharacterBody2D cb ? cb.Velocity : Vector2.Zero;
                cohesionCenter += other.GlobalPosition;
                if (dist < SeparationRadius && dist > 0.0001f)
                    separation += (pos - other.GlobalPosition).Normalized() / dist;  // closer = stronger
            }

            if (neighbors == 0) return _velocity;

            // Alignment: match the flock's average heading.
            Vector2 alignDesired = alignment.LengthSquared() > 0.0001f
                ? alignment.Normalized() * MaxSpeed : _velocity;
            // Cohesion: steer toward the flock's center of mass.
            Vector2 cohesionDesired = SteeringBehavior.Arrive(
                pos, cohesionCenter / neighbors, MaxSpeed, NeighborRadius * 0.5f);
            // Separation: away from crowding neighbors, scaled to max speed.
            Vector2 separationDesired = separation.LengthSquared() > 0.0001f
                ? separation.Normalized() * MaxSpeed : Vector2.Zero;

            return alignDesired * AlignmentWeight
                 + cohesionDesired * CohesionWeight
                 + separationDesired * SeparationWeight;
        }
    }
}
