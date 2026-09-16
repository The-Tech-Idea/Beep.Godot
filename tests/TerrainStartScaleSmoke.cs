using Beep.ECS;
using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

public partial class TerrainStartScaleSmoke : Node
{
    public bool Run()
    {
        foreach (var size in new[] { new Vector2I(17, 19), new Vector2I(97, 73), new Vector2I(128, 128) })
        foreach (int wanted in new[] { 0, 1, 6 })
        {
            var world = MakeWorld(size.X, size.Y);
            var settings = default(TerrainGenerationSettings) with { StartPositionCount = wanted };
            var expected = Reference(world, settings);
            TerrainStartPositionStage.Apply(world, settings, TerrainStartKitRules.Capture(settings));
            if (!expected.SequenceEqual(world.StartPositions)) return Fail($"Start selection changed: {size}, wanted={wanted}");
        }
        var huge = MakeWorld(1024, 1024);
        var six = default(TerrainGenerationSettings) with { StartPositionCount = 6 };
        // Captured before measuring: the kit rules are main-thread input, not stage work.
        var kit = TerrainStartKitRules.Capture(six);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        var reference = Reference(huge, six);
        long referenceMs = watch.ElapsedMilliseconds;
        long referenceBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        TerrainStartPositionStage.Apply(huge, six, kit);
        long actualMs = watch.ElapsedMilliseconds;
        long actualBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        if (!reference.SequenceEqual(huge.StartPositions) || huge.StartPositions.Count != 6) return Fail("Million-cell starts changed");
        if (actualBytes >= referenceBytes / 2) return Fail("Candidate storage did not reduce allocation by at least half");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        int previous = huge.StartPositions.Count;
        try { TerrainStartPositionStage.Apply(huge, six, kit, cancelled.Token); return Fail("Cancelled stage executed"); }
        catch (OperationCanceledException) { }
        if (huge.StartPositions.Count != previous) return Fail("Cancelled stage modified starts");
        GD.Print($"[terrain-start-scale] 1024x1024: old={referenceMs}ms/{referenceBytes} bytes; compact={actualMs}ms/{actualBytes} bytes; identical starts");
        return true;
    }

    private static TerrainGenerationBuffer MakeWorld(int width, int height)
    {
        var world = new TerrainGenerationBuffer(width, height, 1);
        string[] kinds = { "grass", "dry_grass", "desert", "tundra", "jungle", "snow", "ice", "rock", "lava", "shallow_water" };
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = world.CellIndex(x, y);
            world.CellTerrain[i] = kinds[(x / 9 + y / 7) % kinds.Length];
            world.CellRelief[i] = (TerrainRelief)((x / 5 + y / 11) % 3);
            world.CellWater[i] = world.CellTerrain[i] == "shallow_water" ? (WaterBody)(1 + (x / 7) % 3) : WaterBody.None;
            world.CellContinent[i] = 1 + x / Math.Max(1, width / 3);
        }
        return world;
    }

    // Original dictionary-based selection retained only as a regression/measurement oracle.
    private static List<Vector2I> Reference(TerrainGenerationBuffer world, TerrainGenerationSettings settings)
    {
        var result = new List<Vector2I>();
        int wanted = settings.RequestedStartPositionCount;
        if (wanted == 0) return result;
        var candidates = new List<Vector2I>();
        var scores = new Dictionary<Vector2I, float>();
        var continents = new Dictionary<Vector2I, int>();
        for (int y = 0; y < world.CellsHigh; y++)
        for (int x = 0; x < world.CellsWide; x++)
        {
            int i = world.CellIndex(x, y);
            if (world.CellWater[i] != WaterBody.None || world.CellRelief[i] == TerrainRelief.Mountains
                || world.CellTerrain[i] is "snow" or "ice" or "rock" or "lava") continue;
            var cell = new Vector2I(x, y);
            candidates.Add(cell);
            scores[cell] = Score(world, x, y);
            continents[cell] = world.CellContinent[i];
        }
        candidates.Sort((a, b) => scores[b].CompareTo(scores[a]));
        float separation = Mathf.Max(4f, Mathf.Min(world.CellsWide, world.CellsHigh) / (float)Mathf.Max(2, wanted) * 1.6f);
        var used = new HashSet<int>();
        foreach (bool once in new[] { true, false })
        foreach (var candidate in candidates)
        {
            if (result.Count >= wanted) break;
            int continent = continents[candidate];
            if (once && !used.Add(continent)) continue;
            bool farEnough = true;
            foreach (var existing in result)
            {
                float dx = existing.X - candidate.X, dy = existing.Y - candidate.Y;
                if (dx * dx + dy * dy < separation * separation) { farEnough = false; break; }
            }
            if (!farEnough)
            {
                if (once) used.Remove(continent);
                continue;
            }
            result.Add(candidate);
            used.Add(continent);
        }
        return result;
    }

    private static float Score(TerrainGenerationBuffer world, int x, int y)
    {
        float food = 0, production = 0, fresh = 0, sea = 0;
        int count = 0;
        for (int dy = -3; dy <= 3; dy++)
        for (int dx = -3; dx <= 3; dx++)
        {
            if (!world.CellInBounds(x + dx, y + dy) || dx * dx + dy * dy > 9) continue;
            count++;
            int i = world.CellIndex(x + dx, y + dy);
            food += world.CellTerrain[i] switch { "grass" => 1f, "dry_grass" => .7f, "jungle" => .6f,
                "swamp" => .3f, "shallow_water" => .5f, "tundra" => .15f, _ => 0f };
            production += world.CellRelief[i] switch { TerrainRelief.Hills => 1f, TerrainRelief.Mountains => .35f, _ => .15f };
            if (world.CellWater[i] is WaterBody.River or WaterBody.Lake) fresh = 1;
            if (world.CellWater[i] == WaterBody.Ocean) sea = 1;
        }
        return count == 0 ? float.NegativeInfinity : food / count * 1.6f + production / count * .9f + fresh * .8f + sea * .35f;
    }

    private static bool Fail(string message) { GD.PushError("[terrain-start-scale] " + message); return false; }
}
