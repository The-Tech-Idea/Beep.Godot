using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Spawner component. Blind — attach to any Node to spawn entities on a timer or trigger.
    /// Works for enemy spawners, item generators, wave systems, particle emitters.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SpawnerComponent : EntityComponent
    {
        [Export] public PackedScene? SpawnScene { get; set; }
        [Export] public float SpawnInterval { get; set; } = 3f;
        [Export] public int MaxSpawned { get; set; } = 10;
        [Export] public bool SpawnOnStart { get; set; } = true;
        [Export] public Vector2 SpawnOffset { get; set; } = Vector2.Zero;
        [Export] public string SpawnGroup { get; set; } = "spawned";

        [Signal] public delegate void SpawnedEventHandler(Node entity);
        [Signal] public delegate void SpawnLimitReachedEventHandler();
        [Signal] public delegate void AllDespawnedEventHandler();

        private float _timer;
        private int _spawnedCount;

        public override void _Ready()
        {
            base._Ready();
            _timer = SpawnOnStart ? 0 : SpawnInterval;
        }

        public override void _Process(double delta)
        {
            if (!IsActive || SpawnScene == null) return;
            if (_spawnedCount >= MaxSpawned) return;

            _timer -= (float)delta;
            if (_timer <= 0)
            {
                _timer = SpawnInterval;
                Spawn();
            }
        }

        public Node? Spawn()
        {
            if (SpawnScene == null || !IsActive) return null;
            if (_spawnedCount >= MaxSpawned) { EmitSignal(SignalName.SpawnLimitReached); return null; }

            var inst = SpawnScene.Instantiate<Node>();
            if (GetParent() is Node2D parent)
            {
                if (inst is Node2D n2d) n2d.GlobalPosition = parent.GlobalPosition + SpawnOffset;
                parent.GetParent()?.AddChild(inst);
            }
            else GetParent()?.AddChild(inst);

            inst.AddToGroup(SpawnGroup);
            _spawnedCount++;
            EmitSignal(SignalName.Spawned, inst);
            inst.TreeExiting += () =>
            {
                _spawnedCount--;
                if (_spawnedCount <= 0) EmitSignal(SignalName.AllDespawned);
            };
            return inst;
        }
    }
}
