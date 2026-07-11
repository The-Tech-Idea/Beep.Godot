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

        [Signal] public delegate void TargetDetectedEventHandler(Node2D target);
        [Signal] public delegate void TargetLostEventHandler();
        [Signal] public delegate void InAttackRangeEventHandler(Node2D target);
        [Signal] public delegate void ReachedWaypointEventHandler(int index);

        private CharacterBody2D? _body;
        private Vector2 _moveDir;
        private int _waypointIndex;
        private Node2D? _currentTarget;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as CharacterBody2D;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || !IsActive) return;
            UpdateAI((float)delta);
            _body.Velocity = _body.Velocity.MoveToward(_moveDir * Speed, 800f * (float)delta);
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
            _currentTarget = FindNearestInGroup(TargetGroup);
            if (_currentTarget != null)
            {
                _moveDir = (_currentTarget.GlobalPosition - _body!.GlobalPosition).Normalized();
                EmitSignal(SignalName.TargetDetected, _currentTarget);
                if (_body!.GlobalPosition.DistanceTo(_currentTarget.GlobalPosition) < AttackRange)
                    EmitSignal(SignalName.InAttackRange, _currentTarget);
            }
            else _moveDir = Vector2.Zero;
        }

        private void UpdatePatrol()
        {
            if (Waypoints.Length == 0) { _moveDir = Vector2.Zero; return; }
            var wp = GetNode<Node2D>(Waypoints[_waypointIndex]);
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
            if (GD.Randf() < 0.02f)
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
