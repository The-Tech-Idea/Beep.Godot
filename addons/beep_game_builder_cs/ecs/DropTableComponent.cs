using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Weighted loot drop table component. Blind — attach to any entity.
    /// Rolls on death/destroy to spawn items. Works for enemies, chests, crates, bosses.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DropTableComponent : EntityComponent
    {
        [Export] public int MinDrops { get; set; } = 1;
        [Export] public int MaxDrops { get; set; } = 3;
        [Export] public float DropChance { get; set; } = 1f; // 1.0 = always

        [Signal] public delegate void DropSpawnedEventHandler(PackedScene scene, Vector2 position);
        [Signal] public delegate void TableRolledEventHandler(int dropCount);

        private readonly List<DropEntry> _entries = new();

        public class DropEntry
        {
            public PackedScene? Scene;
            public float Weight = 1f;
        }

        public void AddEntry(PackedScene scene, float weight = 1f)
        {
            _entries.Add(new DropEntry { Scene = scene, Weight = weight });
        }

        public void Clear() => _entries.Clear();

        public void Roll()
        {
            if (!IsActive || _entries.Count == 0) return;
            if (GD.Randf() > DropChance) return;

            int count = (int)GD.RandRange(MinDrops, MaxDrops + 1);
            EmitSignal(SignalName.TableRolled, count);

            var parent = GetParent<Node2D>();

            for (int i = 0; i < count; i++)
            {
                var entry = PickWeighted();
                if (entry?.Scene == null) continue;

                var inst = entry.Scene.Instantiate<Node2D>();
                inst.GlobalPosition = parent?.GlobalPosition ?? Vector2.Zero;
                inst.GlobalPosition += new Vector2(
                    (float)GD.RandRange(-20, 20),
                    (float)GD.RandRange(-10, 10));

                parent?.GetParent()?.AddChild(inst);
                EmitSignal(SignalName.DropSpawned, entry.Scene, inst.GlobalPosition);
            }
        }

        private DropEntry? PickWeighted()
        {
            if (_entries.Count == 0) return null;
            float total = 0;
            foreach (var e in _entries) total += e.Weight;
            float roll = (float)GD.RandRange(0, total);
            float cumulative = 0;
            foreach (var e in _entries)
            {
                cumulative += e.Weight;
                if (roll <= cumulative) return e;
            }
            return _entries[^1];
        }
    }
}
