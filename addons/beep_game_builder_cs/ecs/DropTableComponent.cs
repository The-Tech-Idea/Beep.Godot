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

        [Signal] public delegate void DropSpawnedEventHandler(PackedScene scene, Vector2 position);
        [Signal] public delegate void TableRolledEventHandler(int dropCount);

        private readonly List<DropEntry> _entries = new();
        private readonly List<Node2D> _spawnedDrops = new();
        private SeasonalComponent? _seasonal;
        private WeatherSystemComponent? _weather;

        public class DropEntry
        {
            public PackedScene? Scene;
            public float Weight = 1f;
            public SeasonalComponent.Season? RestrictToSeason;      // null = all seasons
            public WeatherSystemComponent.WeatherType? RestrictToWeather;  // null = all weather
        }

        public override void _Ready()
        {
            base._Ready();
            // Auto-discover seasonal and weather systems
            var root = GetTree().Root;
            _seasonal = EntityComponent.FindComponent<SeasonalComponent>(root, true);
            _weather = EntityComponent.FindComponent<WeatherSystemComponent>(root, true);
        }

        public void AddEntry(PackedScene scene, float weight = 1f,
            SeasonalComponent.Season? restrictToSeason = null,
            WeatherSystemComponent.WeatherType? restrictToWeather = null)
        {
            _entries.Add(new DropEntry
            {
                Scene = scene,
                Weight = weight,
                RestrictToSeason = restrictToSeason,
                RestrictToWeather = restrictToWeather
            });
        }

        public void Clear() => _entries.Clear();

        public override void _ExitTree()
        {
            // Clean up any lingering spawned drops
            foreach (var drop in _spawnedDrops)
                drop?.QueueFree();
            _spawnedDrops.Clear();
            base._ExitTree();
        }

        public void Roll()
        {
            if (!IsActive || _entries.Count == 0) return;
            if (GD.Randf() > DropChance) return;

            int count = (int)GD.RandRange(MinDrops, MaxDrops + 1);
            EmitSignal(SignalName.TableRolled, count);

            var parent = GetParent() as Node2D;
            var centerPos = parent?.GlobalPosition ?? Vector2.Zero;

            for (int i = 0; i < count; i++)
            {
                var entry = PickWeighted();
                if (entry?.Scene == null) continue;

                var inst = entry.Scene.Instantiate<Node2D>();
                var spawnPos = FindGoodSpawnPoint(centerPos);
                inst.GlobalPosition = spawnPos;

                parent?.GetParent()?.AddChild(inst);
                _spawnedDrops.Add(inst);

                // Schedule auto-cleanup after lifetime
                ScheduleDropCleanup(inst);

                EmitSignal(SignalName.DropSpawned, entry.Scene, inst.GlobalPosition);
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

                // Check if far enough from other drops
                bool tooClose = false;
                foreach (var drop in _spawnedDrops)
                {
                    if (drop != null && candidate.DistanceTo(drop.GlobalPosition) < MinimumSpacing)
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
            var timer = new Timer();
            AddChild(timer);
            timer.WaitTime = DropLifetimeSeconds;
            timer.OneShot = true;
            timer.Timeout += () =>
            {
                if (drop != null && !drop.IsQueuedForDeletion())
                {
                    drop.QueueFree();
                    _spawnedDrops.Remove(drop);
                }
                timer.QueueFree();
            };
            timer.Start();
        }

        private DropEntry? PickWeighted()
        {
            // Filter entries by current season/weather
            var validEntries = _entries.Where(e =>
                (e.RestrictToSeason == null || e.RestrictToSeason == _seasonal?.CurrentSeason) &&
                (e.RestrictToWeather == null || e.RestrictToWeather == _weather?.CurrentWeather))
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
