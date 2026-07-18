using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Spawner component. Blind — attach to any Node to spawn entities on a timer or trigger.
    /// Works for enemy spawners, item generators, wave systems, particle emitters.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SpawnerComponent : WorldComponent
    {
        [Export] public PackedScene? SpawnScene { get; set; }
        [Export] public float SpawnInterval { get; set; } = 3f;
        [Export] public int MaxSpawned { get; set; } = 10;
        [Export] public bool SpawnOnStart { get; set; } = true;
        [Export] public Vector2 SpawnOffset { get; set; } = Vector2.Zero;
        [Export] public Vector2 SpawnRandomRange { get; set; } = Vector2.Zero;
        /// <summary>
        /// Group each spawned body joins. Defaults to "enemies" because that is what the
        /// targeting components look for — AIController and TurretComponent scan "players",
        /// ProjectileModifierComponent (Homing) scans "enemies". The old default, "spawned",
        /// was a group nothing in the addon ever read, so homing and turrets found no targets
        /// in every genre.
        /// </summary>
        [Export] public string SpawnGroup { get; set; } = "enemies";

        [Signal] public delegate void SpawnedEventHandler(Node entity);
        [Signal] public delegate void SpawnLimitReachedEventHandler();
        [Signal] public delegate void AllDespawnedEventHandler();

        private float _timer;
        private int _spawnedCount;
        private System.Collections.Generic.HashSet<Node> _activeSpawned = new();

        public override void _Ready()
        {
            base._Ready();
            _timer = SpawnOnStart ? 0 : SpawnInterval;
            // SpawnScene is explicitly named in CLAUDE.md as a must-warn null export: a spawner with
            // no scene sits inert on its timer and used to say nothing.
            if (!Engine.IsEditorHint() && SpawnScene == null)
                GD.PushWarning($"[{Name}] SpawnerComponent has no SpawnScene assigned — it will spawn nothing. Assign a scene to spawn.");
        }

        public override void _Process(double delta)
        {
            // Runtime only: Spawn() adds instances into the grandparent. IsActive defaults
            // true, so without this a [Tool] spawner with a SpawnScene would spawn nodes into
            // the scene on a timer just from being opened in the editor.
            if (Engine.IsEditorHint()) return;
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

            // A Node2D parent spawns into the GRANDPARENT (so the instance is a world sibling, not glued
            // to the spawner); a non-Node2D parent spawns as its own child. Resolve the target first so
            // we don't count/emit for an instance that never entered the tree (spawner at scene root).
            var parent = GetParent();
            var addTarget = parent is Node2D ? parent.GetParent() : parent;
            if (addTarget == null)
            {
                GD.PushWarning($"[{Name}] SpawnerComponent's parent has no parent to spawn into (is the spawner at the scene root?) — cannot spawn.");
                return null;
            }

            var inst = SpawnScene.Instantiate<Node>();
            // Parent FIRST, then set GlobalPosition. Setting it before AddChild made it a LOCAL
            // position the new parent's transform re-derived, so a spawner under a level container
            // offset from origin spawned everything shifted by that offset.
            addTarget.AddChild(inst);
            if (parent is Node2D parent2D && inst is Node2D n2d)
            {
                Vector2 randomOffset = SpawnRandomRange == Vector2.Zero ? Vector2.Zero :
                    new Vector2(
                        (GD.Randf() * 2f - 1f) * SpawnRandomRange.X,
                        (GD.Randf() * 2f - 1f) * SpawnRandomRange.Y
                    );
                n2d.GlobalPosition = parent2D.GlobalPosition + SpawnOffset + randomOffset;
            }

            inst.AddToGroup(SpawnGroup);
            _activeSpawned.Add(inst);
            _spawnedCount++;
            EmitSignal(SignalName.Spawned, inst);
            inst.TreeExiting += () => OnSpawnedExiting(inst);
            return inst;
        }

        private void OnSpawnedExiting(Node inst)
        {
            // A spawned enemy can outlive the spawner (it's reparented up to the level, then the
            // spawner is freed on scene teardown) — its TreeExiting would then fire this on a
            // disposed spawner. Bail if we're gone.
            if (!GodotObject.IsInstanceValid(this)) return;
            _activeSpawned.Remove(inst);
            _spawnedCount--;
            if (_spawnedCount <= 0) EmitSignal(SignalName.AllDespawned);
        }
    }
}
