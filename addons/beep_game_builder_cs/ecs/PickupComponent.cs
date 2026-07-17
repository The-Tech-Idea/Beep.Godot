using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Pickup component. Blind — attach to any Area2D to make it collectible.
    /// Works for coins, health packs, keys, power-ups, loot drops.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PickupComponent : GameplayComponent
    {
        [Export] public string ItemId { get; set; } = "coin";
        [Export] public int Quantity { get; set; } = 1;
        [Export] public float FloatAmplitude { get; set; } = 5f;
        [Export] public float FloatSpeed { get; set; } = 2f;
        [Export] public bool AutoRotate { get; set; } = true;
        [Export] public float RespawnSeconds { get; set; } = 0f; // 0 = no respawn

        /// <summary>Points to award on collection. 0 = award nothing (the pickup is
        /// inventory-only, and the Collected signal is the whole story).
        ///
        /// This is the edge that was missing between the components and the run: GameFlow
        /// has always tracked Score and fired LevelComplete at TargetScore, but nothing in
        /// the addon ever called AddScore, so no generated game could score a point or
        /// finish a level — which in turn made the results/level-select screens
        /// unreachable, since GameFlow's signal is their only entry point.</summary>
        [Export] public int ScoreValue { get; set; } = 0;

        /// <summary>Who to award to. Empty = search the tree for the first GameFlowComponent.
        /// Set this when a scene has more than one.</summary>
        [Export] public NodePath GameFlowPath { get; set; } = new("");

        [Signal] public delegate void CollectedEventHandler(string itemId, int quantity);
        [Signal] public delegate void RespawnedEventHandler();

        private Vector2 _startPos;
        private float _time;
        private bool _collected;
        private float _respawnTimer;

        private Area2D? _area;

        public override void _Ready()
        {
            base._Ready();
            if (GetParent() is Node2D parent) _startPos = parent.Position;
            _area = GetParent() as Area2D;
            if (_area != null) _area.BodyEntered += OnBodyEntered;
        }

        private void OnBodyEntered(Node2D body) => Collect();

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint()) return;
            if (_collected)
            {
                if (RespawnSeconds > 0)
                {
                    _respawnTimer -= (float)delta;
                    if (_respawnTimer <= 0) Respawn();
                }
                return;
            }
            _time += (float)delta;
            if (GetParent() is Node2D p)
            {
                p.Position = _startPos + new Vector2(0, Mathf.Sin(_time * FloatSpeed) * FloatAmplitude);
                if (AutoRotate) p.Rotation += (float)delta * 2f;
            }
        }

        /// <summary>The run's GameFlow. Resolved lazily and cached: pickups are usually
        /// instanced into a level under a LevelContainer, so the flow is a cousin rather
        /// than a sibling and can't be found until the tree is built.</summary>
        private GameFlowComponent? ResolveGameFlow()
        {
            if (_flow != null && GodotObject.IsInstanceValid(_flow)) return _flow;

            if (!GameFlowPath.IsEmpty)
                _flow = GetNodeOrNull<GameFlowComponent>(GameFlowPath);

            // Search from the current scene, not from a sibling: the pickup lives inside the
            // level instance while GameFlow sits on the main scene alongside it.
            if (_flow == null && GetTree()?.CurrentScene is { } scene)
                _flow = EntityComponent.FindComponent<GameFlowComponent>(scene, true);

            return _flow;
        }
        private GameFlowComponent? _flow;

        private void Collect()
        {
            if (_collected || !IsActive) return;
            _collected = true;
            EmitSignal(SignalName.Collected, ItemId, Quantity);

            if (ScoreValue > 0)
            {
                var flow = ResolveGameFlow();
                if (flow != null) flow.AddScore(ScoreValue);
                else GD.PushWarning($"[{Name}] ScoreValue is {ScoreValue} but no GameFlowComponent was found — the points go nowhere. Add one to the scene, or set GameFlowPath.");
            }
            if (GetParent() is Node2D p) { p.Visible = false; p.ProcessMode = ProcessModeEnum.Disabled; }
            if (RespawnSeconds > 0) _respawnTimer = RespawnSeconds;
            else if (GetParent() is Node parent) parent.QueueFree();
        }

        private void Respawn()
        {
            _collected = false;
            _time = 0;
            if (GetParent() is Node2D p) { p.Visible = true; p.ProcessMode = ProcessModeEnum.Inherit; p.Position = _startPos; }
            EmitSignal(SignalName.Respawned);
        }

        public override void _ExitTree()
        {
            if (_area != null)
                _area.BodyEntered -= OnBodyEntered;
        }
    }
}
