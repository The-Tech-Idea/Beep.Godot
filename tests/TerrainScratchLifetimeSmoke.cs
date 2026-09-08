using Beep.ECS;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;

public partial class TerrainScratchLifetimeSmoke : Node
{
    public bool Run()
    {
        var lazy = new TerrainGenerationBuffer(66, 70, 2);
        if (lazy.CellPayloadBytes != 0) return Fail("Gameplay outputs allocated before any stage used them");
        int cells = 33 * 35;
        if (lazy.CellTerrain.Length != cells || lazy.CellTerrain.Any(kind => kind != "grass")
            || lazy.CellPayloadBytes != (long)cells * IntPtr.Size) return Fail("Lazy terrain defaults or allocation incorrect");
        var terrain = lazy.CellTerrain;
        terrain[0] = "desert";
        if (!ReferenceEquals(terrain, lazy.CellTerrain) || lazy.CellTerrain[0] != "desert")
            return Fail("Output getter lost prior stage edits");
        if (lazy.Resource.Any(id => id != "") || lazy.Feature.Any(id => id != "")
            || lazy.CellLiquidResource.Any(id => id != "") || lazy.CellUndergroundResource.Any(id => id != "")
            || lazy.CellInlandTerrain.Any(id => id != null) || lazy.CellShade.Any(shade => shade != 1f)
            || lazy.CellWater.Any(water => water != WaterBody.None) || lazy.CellRelief.Any(relief => relief != TerrainRelief.Flat)
            || lazy.CellElevation.Any(height => height != 0) || lazy.CellContinent.Any(id => id != 0)
            || lazy.CellUndergroundRichness.Any(value => value != 0) || lazy.CellUndergroundDepth.Any(value => value != 0))
            return Fail("Lazy output initialization changed defaults");
        if (lazy.CellPayloadBytes != (long)cells * (6 * IntPtr.Size + 19))
            return Fail("Gameplay output payload accounting incorrect");
        foreach (int samples in new[] { 1, 2, 4, 8 })
        {
            var actual = ClimateWorld(samples);
            var reference = ClimateWorld(samples);
            long fineBytes = actual.ScratchPayloadBytes;
            actual.CompactClimate();
            for (int cell = 0; cell < actual.CellTerrain.Length; cell++)
                if (actual.TemperatureAtCell(cell) != reference.TemperatureAtCell(cell)
                    || actual.MoistureAtCell(cell) != reference.MoistureAtCell(cell)) return Fail("Cell climate sample changed");
            if (actual.ScratchPayloadBytes != 40 * 32 * 8 || fineBytes != actual.ScratchPayloadBytes * samples * samples)
                return Fail("Compacted climate retains full-resolution payload");
            actual.CompactClimate();
            try { _ = actual.Temperature; return Fail("Fine climate was silently reallocated"); }
            catch (InvalidOperationException) { }
            var settings = default(TerrainGenerationSettings) with { Size = new(40, 32), Seed = 31415, FeatureDensity = 1 };
            using var noise = TerrainNoiseSet.Create(settings);
            using var referenceNoise = TerrainNoiseSet.Create(settings);
            TerrainFeatureStage.Apply(actual, noise, settings);
            TerrainFeatureStage.Apply(reference, referenceNoise, settings);
            if (!actual.Feature.SequenceEqual(reference.Feature)) return Fail("Compaction changed vegetation placement");
            if (!actual.Feature.Any(feature => feature.Length > 0)) return Fail("Vegetation parity fixture produced no features");
            actual.ReleaseClimate();
            if (actual.ScratchPayloadBytes != 0) return Fail("Retired climate remains rooted");
            try { _ = actual.MoistureAtCell(0); return Fail("Retired climate could be read"); }
            catch (InvalidOperationException) { }
        }
        var cancelled = ClimateWorld(4);
        long before = cancelled.ScratchPayloadBytes;
        using var token = new CancellationTokenSource();
        token.Cancel();
        try { cancelled.CompactClimate(token.Token); return Fail("Cancelled compaction executed"); }
        catch (OperationCanceledException) { }
        if (cancelled.ScratchPayloadBytes != before || cancelled.Temperature.Length != cancelled.Count)
            return Fail("Cancelled compaction changed the source");

        // The oracle keeps the original separate median buffer, sharing only
        // the unchanged drainage/diffusion kernels with production.
        var diffuse = typeof(TerrainErosionStage).GetMethod("Diffuse", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Action<TerrainGenerationBuffer, float[], float, CancellationToken>>();
        foreach (float strength in new[] { 0f, .25f, 1f, 4f })
        foreach (int width in new[] { 1, 17, 64 })
        {
            var actual = ErosionWorld(width, 31);
            var reference = ErosionWorld(width, 31);
            ReferenceErosion(reference, strength, diffuse);
            TerrainErosionStage.Apply(actual, default(TerrainGenerationSettings) with { ErosionStrength = strength });
            if (!actual.Elevation.SequenceEqual(reference.Elevation)) return Fail("Erosion scratch reuse changed heights");
        }
        GD.Print("[terrain-scratch-lifetime] exact climate/vegetation parity at 1/2/4/8 samples; release/cancel and erosion reuse OK");
        return true;
    }

    private static TerrainGenerationBuffer ClimateWorld(int samples)
    {
        var world = new TerrainGenerationBuffer(40 * samples, 32 * samples, samples);
        for (int i = 0; i < world.Count; i++)
        {
            world.Temperature[i] = (i * 31 % 997) / 997f;
            world.Moisture[i] = (i * 19 % 991) / 991f;
        }
        string[] kinds = { "grass", "dry_grass", "tundra", "desert", "jungle", "swamp" };
        for (int i = 0; i < world.CellTerrain.Length; i++)
        {
            world.CellTerrain[i] = kinds[(i / 13) % kinds.Length];
            world.CellRelief[i] = i % 11 == 0 ? TerrainRelief.Mountains : TerrainRelief.Flat;
            world.CellWater[i] = i % 17 == 0 ? WaterBody.Lake : WaterBody.None;
        }
        return world;
    }

    private static TerrainGenerationBuffer ErosionWorld(int width, int height)
    {
        var world = new TerrainGenerationBuffer(width, height, 1);
        for (int i = 0; i < world.Count; i++)
        {
            world.Land[i] = i % 7 != 0;
            world.Elevation[i] = world.Land[i] ? .1f + (i * 19 % 101) / 130f : 0;
        }
        return world;
    }

    private static void ReferenceErosion(TerrainGenerationBuffer world, float strength,
        Action<TerrainGenerationBuffer, float[], float, CancellationToken> diffuse)
    {
        if (strength <= 0) return;
        var to = new int[world.Count];
        var order = new int[world.Count];
        var flow = new float[world.Count];
        int land = TerrainFlow.Accumulate(world, to, order, flow);
        if (land == 0) return;
        var sorted = new float[land];
        for (int i = 0; i < land; i++) sorted[i] = flow[order[i]];
        Array.Sort(sorted);
        float typical = Mathf.Max(1f, sorted[land / 2]);
        for (int i = 0; i < land; i++) flow[order[i]] = Mathf.Min(3f, Mathf.Pow(flow[order[i]] / typical, .5f));
        float dial = Mathf.Clamp(strength, 0, 4);
        float incision = .12f * dial;
        var settled = new float[world.Count];
        for (int pass = 0; pass < 12; pass++)
        {
            for (int i = 0; i < land; i++)
            {
                int index = order[i], outlet = to[index];
                if (outlet < 0) continue;
                float slope = Mathf.Max(0, world.Elevation[index] - world.Elevation[outlet]);
                if (slope <= 0) continue;
                float lowering = incision * flow[index] * slope;
                world.Elevation[index] = Mathf.Max(world.Elevation[outlet], world.Elevation[index] - lowering);
            }
            diffuse(world, settled, dial, default);
        }
    }

    private static bool Fail(string message) { GD.PushError("[terrain-scratch-lifetime] " + message); return false; }
}
