using Beep.ECS;
using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class TerrainResourceScaleSmoke : Node
{
    public bool Run()
    {
        foreach (ResourceSet set in Enum.GetValues<ResourceSet>())
        foreach (int seed in new[] { 0, 31415, -817 })
        foreach (Vector2I size in new[] { new Vector2I(1, 33), new Vector2I(37, 2), new Vector2I(79, 47) })
        {
            var settings = default(TerrainGenerationSettings) with { Seed = seed, ResourceSet = set, ResourceDensity = 4 };
            var actual = MakeWorld(size);
            var expected = MakeWorld(size);
            ApplyReference(expected, settings);
            TerrainResourceStage.Apply(actual, settings);
            for (int i = 0; i < actual.Resource.Length; i++)
                if (actual.Resource[i] != expected.Resource[i] || actual.CellLiquidResource[i] != expected.CellLiquidResource[i])
                    return Fail($"Placement changed: set={set}, seed={seed}, size={size}, cell={i}");
        }

        // Rebuilding after an authored catalog edit must not reuse stale choices.
        using var resource = new ResourceDefinition { Id = "first", TerrainKinds = new() { "grass" } };
        using var catalog = new ResourceCatalog { Resources = new() { resource } };
        var custom = default(TerrainGenerationSettings) with { ResourceCatalog = catalog, ResourceDensity = 4 };
        var before = MakeWorld(new Vector2I(31, 31));
        TerrainResourceStage.Apply(before, custom);
        resource.Id = "second";
        var after = MakeWorld(new Vector2I(31, 31));
        TerrainResourceStage.Apply(after, custom);
        int changed = 0;
        for (int i = 0; i < before.Resource.Length; i++)
        {
            if (before.Resource[i] != "first") continue;
            if (after.Resource[i] != "second") return Fail("Catalog edit was not reflected in next build");
            changed++;
        }
        if (changed == 0) return Fail("Custom resource fixture placed no resources");

        var large = MakeWorld(new Vector2I(1024, 1024));
        var largeSettings = default(TerrainGenerationSettings) with { Seed = 31415, ResourceDensity = 4 };
        long allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        TerrainResourceStage.Apply(large, largeSettings);
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        int placed = 0;
        for (int y = 0; y < 1024; y++)
        for (int x = 0; x < 1024; x++)
        {
            int index = y * 1024 + x;
            string id = large.Resource[index].Length > 0 ? large.Resource[index] : large.CellLiquidResource[index];
            if (id.Length == 0) continue;
            placed++;
            if (large.CellWater[index] != WaterBody.None && large.Resource[index].Length > 0)
                return Fail("Surface resource placed in water");
            if (large.CellWater[index] == WaterBody.None && large.CellLiquidResource[index].Length > 0)
                return Fail("Liquid resource placed on land");
            for (int ny = Math.Max(0, y - 3); ny <= y; ny++)
            for (int nx = Math.Max(0, x - 3); nx <= Math.Min(1023, x + 3); nx++)
            {
                int other = ny * 1024 + nx;
                if (other >= index || (nx - x) * (nx - x) + (ny - y) * (ny - y) >= 16) continue;
                if (large.Resource[other] == id || large.CellLiquidResource[other] == id)
                    return Fail($"Spacing violation at {x},{y}");
            }
        }
        if (placed < 10000) return Fail("Million-cell fixture did not exercise dense placement");
        GD.Print($"[terrain-resource-scale] 1024x1024: {placed} deposits, stage={watch.ElapsedMilliseconds}ms, managed allocation={allocated} bytes");
        return true;
    }

    private static TerrainGenerationBuffer MakeWorld(Vector2I size)
    {
        var world = new TerrainGenerationBuffer(size.X, size.Y, 1);
        string[] kinds = { "grass", "desert", "tundra", "jungle", "shallow_water", "deep_water", "snow", "rock" };
        for (int y = 0; y < size.Y; y++)
        for (int x = 0; x < size.X; x++)
        {
            int i = y * size.X + x;
            world.CellTerrain[i] = kinds[(x / 7 + y / 11) % kinds.Length];
            world.CellWater[i] = TerrainTileSets.IsWaterKind(world.CellTerrain[i]) ? WaterBody.Ocean : WaterBody.None;
            world.CellRelief[i] = (TerrainRelief)((x / 3 + y / 5) % 3);
        }
        return world;
    }

    // Original exhaustive algorithm retained only as an independent test oracle.
    private static void ApplyReference(TerrainGenerationBuffer world, TerrainGenerationSettings settings)
    {
        var catalog = settings.ResourceCatalog ?? ResourceCatalogs.For(settings.ResourceSet);
        var placed = new List<(int X, int Y, string Id)>();
        for (int y = 0; y < world.CellsHigh; y++)
        for (int x = 0; x < world.CellsWide; x++)
        {
            int i = y * world.CellsWide + x;
            bool land = world.CellWater[i] == WaterBody.None;
            float density = (land ? 0.085f : 0.085f * 0.22f) * settings.ResourceDensity;
            if (TerrainGeometry.Hash01(x, y, settings.Seed + 63601) > density) continue;
            var stratum = land ? ResourceStratum.Surface : ResourceStratum.Liquid;
            bool Supports(ResourceDefinition definition)
            {
                if (definition.Stratum != stratum || (definition.RequiresRelief && (TerrainRelief)definition.RequiredRelief != world.CellRelief[i])) return false;
                foreach (string terrain in definition.TerrainKinds)
                    if (terrain == world.CellTerrain[i]) return true;
                return false;
            }
            float total = 0;
            foreach (ResourceDefinition definition in catalog.Resources)
                if (Supports(definition)) total += definition.Weight;
            if (total <= 0) continue;
            float roll = TerrainGeometry.Hash01(x, y, settings.Seed + 63611) * total;
            string chosen = "";
            foreach (ResourceDefinition definition in catalog.Resources)
            {
                if (!Supports(definition)) continue;
                roll -= definition.Weight;
                if (roll <= 0) { chosen = definition.Id; break; }
            }
            if (chosen.Length == 0) continue;
            bool clear = true;
            foreach (var prior in placed)
                if (prior.Id == chosen && (prior.X - x) * (prior.X - x) + (prior.Y - y) * (prior.Y - y) < 16)
                { clear = false; break; }
            if (!clear) continue;
            if (land) world.Resource[i] = chosen;
            else world.CellLiquidResource[i] = chosen;
            placed.Add((x, y, chosen));
        }
    }

    private static bool Fail(string message)
    {
        GD.PushError("[terrain-resource-scale] " + message);
        return false;
    }
}
