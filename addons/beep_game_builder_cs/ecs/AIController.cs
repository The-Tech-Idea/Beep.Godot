using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Simple AI controller component. Attach to any CharacterBody2D.
    /// Blind — works for enemies, NPCs, patrol guards, wandering animals.
    /// Modes: Patrol (between waypoints), Chase (follow target), Wander (random).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AIController : ControllerComponent
    {
        public enum AIMode { Idle, Patrol, Chase, Wander, Flee }

        [Export] public AIMode Mode { get; set; } = AIMode.Wander;
        [Export] public float Speed { get; set; } = 100f;
        [Export] public float DetectionRange { get; set; } = 200f;
        [Export] public float AttackRange { get; set; } = 40f;
        [Export] public NodePath[] Waypoints { get; set; } = System.Array.Empty<NodePath>();
        [Export] public string TargetGroup { get; set; } = "players";
        [Export] public bool StunBlocksMovement { get; set; } = true;
        [Export] public float WanderChangeRate { get; set; } = 0.02f;

        [Signal] public delegate void TargetDetectedEventHandler(Node2D target);
        [Signal] public delegate void TargetLostEventHandler();
        [Signal] public delegate void InAttackRangeEventHandler(Node2D target);
        [Signal] public delegate void ReachedWaypointEventHandler(int index);

        private CharacterBody2D? _body;
        private StatusEffectComponent? _statusEffects;
        private StatsComponent? _stats;
        private AggroComponent? _aggro;
        private AttackComponent? _attack;
        private Vector2 _moveDir;
        private int _waypointIndex;
        private Node2D? _currentTarget;
        private Node2D? _lastTarget;
        private AIMode _lastMode = AIMode.Idle;
        private AIMode _baseMode;   // the mode to return to when a chase ends

        public override void _Ready()
        {
            base._Ready();
            _body = ResolveBody2D();
            _baseMode = Mode;
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            _stats = GetSiblingComponent<StatsComponent>();
            // Being hit makes a wandering/patrolling enemy turn and chase its attacker: a sibling
            // AggroComponent (fed by HealthComponent) names the threat, and its target overrides the
            // nearest-in-group pick below.
            _aggro = GetSiblingComponent<AggroComponent>();
            if (_aggro != null) _aggro.TargetAcquired += OnAggroTarget;
            // The attack verb — called from UpdateChase when adjacent. Without this the enemy
            // pursued its target and emitted InAttackRange but never actually swung.
            _attack = GetSiblingComponent<AttackComponent>();

            // AIController owns Velocity + MoveAndSlide; a sibling MovementComponent would integrate
            // the body a second time each frame. Warn (MovementComponent warns the other way too).
            if (GetSiblingComponent<MovementComponent>() != null)
                GD.PushWarning($"[{Name}] AIController and a sibling MovementComponent both drive the body — remove the MovementComponent (AIController owns movement) or they will fight over MoveAndSlide.");
        }

        private void OnAggroTarget(Node2D target) => Mode = AIMode.Chase;

        public override void _ExitTree()
        {
            if (_aggro != null) _aggro.TargetAcquired -= OnAggroTarget;
            base._ExitTree();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;

            bool isStunned = StunBlocksMovement && _statusEffects != null && _statusEffects.HasEffect("stun");

            // Proactively enter Chase when a target comes within DetectionRange — not only after
            // being hit. Without this a Wander/Patrol enemy walked right past the player. Idle is
            // excluded: an Idle NPC (sleeping/decorative/talk-to) should NOT auto-chase.
            if (!isStunned && Mode != AIMode.Chase && Mode != AIMode.Flee && Mode != AIMode.Idle
                && FindNearestInGroup(TargetGroup) != null)
                Mode = AIMode.Chase;

            // Reset state on mode change.
            if (Mode != _lastMode)
            {
                _moveDir = Vector2.Zero;
                _lastMode = Mode;
            }

            if (!isStunned)
                UpdateAI((float)delta);

            float finalSpeed = _stats?.GetValue("move_speed", Speed) ?? Speed;
            _body.Velocity = _body.Velocity.MoveToward(_moveDir * finalSpeed, 800f * (float)delta);
            _body.MoveAndSlide();
        }

        private void UpdateAI(float delta)
        {
            switch (Mode)
            {
                case AIMode.Chase:
                    UpdateChase();
                    break;
                case AIMode.Patrol:
                    UpdatePatrol();
                    break;
                case AIMode.Wander:
                    UpdateWander();
                    break;
                case AIMode.Flee:
                    UpdateFlee();
                    break;
            }
        }

        private void UpdateChase()
        {
            // Chase the thing that hurt us if aggro named one; otherwise the nearest in the group.
            _currentTarget = (_aggro?.CurrentTarget) ?? FindNearestInGroup(TargetGroup);
            if (_currentTarget != null && GodotObject.IsInstanceValid(_currentTarget))
            {
                _moveDir = (_currentTarget.GlobalPosition - _body!.GlobalPosition).Normalized();
                if (_lastTarget != _currentTarget)
                {
                    EmitSignal(SignalName.TargetDetected, _currentTarget);
                    _lastTarget = _currentTarget;
                }
                if (_body!.GlobalPosition.DistanceTo(_currentTarget.GlobalPosition) < AttackRange)
                {
                    EmitSignal(SignalName.InAttackRange, _currentTarget);
                    _attack?.Attack(_currentTarget.GlobalPosition);   // actually swing (AttackComponent honors its own cooldown)
                    _moveDir = Vector2.Zero;                          // stop to attack when adjacent
                }
            }
            else
            {
                if (_lastTarget != null)
                {
                    EmitSignal(SignalName.TargetLost);
                    _lastTarget = null;
                }
                _moveDir = Vector2.Zero;
                Mode = _baseMode;   // nothing to chase — resume patrol/wander
            }
        }

        private void UpdatePatrol()
        {
            if (Waypoints.Length == 0) { _moveDir = Vector2.Zero; return; }
            var wp = GetNodeOrNull<Node2D>(Waypoints[_waypointIndex]);
            if (wp == null) return;
            _moveDir = (wp.GlobalPosition - _body!.GlobalPosition).Normalized();
            if (_body!.GlobalPosition.DistanceTo(wp.GlobalPosition) < 10f)
            {
                EmitSignal(SignalName.ReachedWaypoint, _waypointIndex);
                _waypointIndex = (_waypointIndex + 1) % Waypoints.Length;
            }
        }

        private void UpdateWander()
        {
            if (GD.Randf() < WanderChangeRate)
                _moveDir = new Vector2(GD.Randf() * 2 - 1, GD.Randf() * 2 - 1).Normalized();
        }

        private void UpdateFlee()
        {
            var threat = FindNearestInGroup(TargetGroup);
            if (threat != null)
                _moveDir = (_body!.GlobalPosition - threat.GlobalPosition).Normalized();
            else _moveDir = Vector2.Zero;
        }

        private Node2D? FindNearestInGroup(string group)
        {
            var nodes = GetTree().GetNodesInGroup(group);
            Node2D? nearest = null;
            float minDist = DetectionRange;
            foreach (var node in nodes)
            {
                if (node is Node2D n)
                {
                    float d = _body!.GlobalPosition.DistanceTo(n.GlobalPosition);
                    if (d < minDist) { minDist = d; nearest = n; }
                }
            }
            return nearest;
        }
    }
}
