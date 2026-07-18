using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS
{
    /// <summary>
    /// Weighted loot drop table component. Blind — attach to any entity.
    /// Rolls on death/destroy to spawn items. Works for enemies, chests, crates, bosses.
    /// Supports seasonal/weather-based drops, difficulty scaling, and auto-cleanup.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DropTableComponent : GameplayComponent
    {
        [Export] public int MinDrops { get; set; } = 1;
        [Export] public int MaxDrops { get; set; } = 3;
        [Export] public float DropChance { get; set; } = 1f;
        [Export] public float DifficultyWeightMultiplier { get; set; } = 1.0f;
        [Export] public float ScatterRadius { get; set; } = 30f;
        [Export] public float DropLifetimeSeconds { get; set; } = 300f;  // 5 minutes
        [Export] public int MaxPlacementAttempts { get; set; } = 3;
        [Export] public float MinimumSpacing { get; set; } = 20f;

        /// <summary>
        /// The weighted loot entries, authored in the inspector as an array of
        /// <see cref="DropTableEntry"/> .tres. Previously a private list with no [Export], fillable
        /// only by an AddEntry() nothing called — so Roll() always returned empty.
        /// </summary>
        [Export] public DropTableEntry[] Entries { get; set; } = System.Array.Empty<DropTableEntry>();

        [Signal] public delegate void DropSpawnedEventHandler(GameItem item, Vector2 position);
        [Signal] public delegate void TableRolledEventHandler(int dropCount);

        private readonly List<Node2D> _spawnedDrops = new();
        private SeasonalComponent? _seasonal;
        private WeatherSystemComponent? _weather;
        private HealthComponent? _health;

        public override void _Ready()
        {
            base._Ready();
            // Auto-discover seasonal and weather systems
            var root = GetTree().Root;
            _seasonal = EntityComponent.FindComponent<SeasonalComponent>(root, true);
            _weather = EntityComponent.FindComponent<WeatherSystemComponent>(root, true);

            // Loot-on-death: a sibling HealthComponent's Died rolls the table. This is the drop
            // half of the loop — DestructibleComponent breaks the body, this drops the loot, both
            // off the same Died, no double-roll. An entity with no HealthComponent (a chest opened
            // by interaction) simply calls Roll() itself.
            _health = GetSiblingComponent<HealthComponent>();
            if (_health != null) _health.Died += Roll;
        }

        public override void _ExitTree()
        {
            if (_health != null) _health.Died -= Roll;
            // Do NOT free the spawned drops here. They were reparented to the level and must OUTLIVE
            // this component — which is freed the instant its dying entity is (a destructible's
            // Break() QueueFrees the body on the same Died that rolled the loot). Freeing them here
            // destroyed the loot the moment it dropped. The level frees them on scene change, and
            // each carries its own SceneTreeTimer lifetime (see ScheduleDropCleanup).
            _spawnedDrops.Clear();
            base._ExitTree();
        }

        public void Roll()
        {
            if (!IsActive) return;
            if (Entries.Length == 0)
            {
                GD.PushWarning(
                    $"DropTableComponent ('{Name}'): Roll() called with no Entries — nothing to " +
                    "drop. Assign at least one DropTableEntry .tres in the inspector, or remove " +
                    "the component from this entity.");
                return;
            }
            if (GD.Randf() > DropChance) return;

            // GD.RandRange(int, int) is INCLUSIVE on both ends, so MinDrops..MaxDrops is right —
            // the old (int)RandRange(Min, Max+1) assumed the exclusive double overload and dropped
            // one too many (a "max 3" table could drop 4).
            int count = GD.RandRange(MinDrops, MaxDrops);
            EmitSignal(SignalName.TableRolled, count);

            var parent = GetParent() as Node2D;
            var centerPos = parent?.GlobalPosition ?? Vector2.Zero;

            for (int i = 0; i < count; i++)
            {
                var entry = PickWeighted();
                if (entry?.Item == null) continue;

                var scene = entry.Item.WorldScene;
                if (scene == null)
                {
                    GD.PushWarning(
                        $"[{Name}] Drop '{entry.Item.DisplayName}' has no WorldScene — it has no " +
                        "node form to drop. Set GameItem.WorldScene (e.g. an Area2D with a PickupComponent).");
                    continue;
                }

                var inst = scene.Instantiate<Node2D>();
                var spawnPos = FindGoodSpawnPoint(centerPos);
                inst.GlobalPosition = spawnPos;

                // Stamp the dropped node so collecting it yields THIS item. Without this a generic
                // world scene would drop, be picked up, and add nothing (or the wrong thing).
                var pickup = EntityComponent.FindComponent<PickupComponent>(inst, true);
                if (pickup != null) pickup.Item = entry.Item;
                else GD.PushWarning($"[{Name}] '{entry.Item?.DisplayName}' dropped, but its WorldScene has no PickupComponent — the drop can't be collected. Add a PickupComponent to the item's WorldScene.");

                if (parent?.GetParent() is { } dropParent) dropParent.AddChild(inst);
                else { GD.PushWarning($"[{Name}] nowhere to place the drop (owner has no Node2D grandparent) — freeing it."); inst.QueueFree(); continue; }
                _spawnedDrops.Add(inst);

                // Schedule auto-cleanup after lifetime
                ScheduleDropCleanup(inst);

                EmitSignal(SignalName.DropSpawned, entry.Item, inst.GlobalPosition);
            }
        }

        /// <summary>Find a spawn point with scatter radius, avoiding existing drops.</summary>
        private Vector2 FindGoodSpawnPoint(Vector2 center)
        {
            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                Vector2 direction = Vector2.FromAngle((float)GD.Randf() * Mathf.Tau);
                float distance = (float)GD.Randf() * ScatterRadius;
                Vector2 candidate = center + direction * distance;

                // Drop already-freed entries (their lifetime timer fired) before spacing against them.
                _spawnedDrops.RemoveAll(d => !GodotObject.IsInstanceValid(d));
                bool tooClose = false;
                foreach (var drop in _spawnedDrops)
                {
                    if (candidate.DistanceTo(drop.GlobalPosition) < MinimumSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose) return candidate;
            }

            // Fallback: random placement if all attempts fail
            return center + Vector2.FromAngle((float)GD.Randf() * Mathf.Tau) * ScatterRadius;
        }

        private void ScheduleDropCleanup(Node2D drop)
        {
            // A SceneTreeTimer is owned by the tree, NOT by this component — so a drop's lifetime
            // still elapses after the spawner that dropped it has died and been freed. A Timer node
            // child of this component died with it, orphaning the drop forever. The stale entry in
            // _spawnedDrops is pruned lazily in FindGoodSpawnPoint (touching it here can hit a
            // disposed component after the spawner is gone).
            var timer = GetTree().CreateTimer(DropLifetimeSeconds);
            timer.Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(drop) && !drop.IsQueuedForDeletion())
                    drop.QueueFree();
            };
        }

        private DropTableEntry? PickWeighted()
        {
            // Filter entries by current season/weather. An unrestricted entry (AnySeason /
            // AnyWeather) always passes; a restricted one passes only when it matches the live
            // system, or when no such system exists to contradict it.
            var validEntries = Entries.Where(e => e != null &&
                (e.AnySeason || _seasonal == null || e.Season == _seasonal.CurrentSeason) &&
                (e.AnyWeather || _weather == null || e.Weather == _weather.CurrentWeather))
                .ToList();

            if (validEntries.Count == 0) return null;

            float total = 0;
            foreach (var e in validEntries)
                total += e.Weight * DifficultyWeightMultiplier;

            float roll = (float)GD.RandRange(0, total);
            float cumulative = 0;

            foreach (var e in validEntries)
            {
                cumulative += e.Weight * DifficultyWeightMultiplier;
                if (roll <= cumulative) return e;
            }

            return validEntries[^1];
        }
    }
}
