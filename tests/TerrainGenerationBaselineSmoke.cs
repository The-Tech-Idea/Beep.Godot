using Beep.ECS;
using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// The generation pipeline's fingerprint and its allocation bill, for the
/// baseline probe. Snapshot hashes every layer the generator publishes for one
/// (seed, size, shape) so a later run can be compared layer by layer; the
/// allocation query runs the same build and reads the managed bytes this
/// thread allocated across it, stage by stage. Generation is single-threaded
/// inside the builder, so the per-thread counter is the exact bill.
/// </summary>
public partial class TerrainGenerationBaselineSmoke : Node
{
    // A world a game would ask for: the axes go through ApplyMapSetup, and the
    // dials that gate whole stages are on so every stage is in the hash. The
    // generator's own defaults leave coherence, scale rules and lake shores off.
    private static TerrainGenerationSettings Settings(Vector2I size, int seed, int shape)
    {
        var generator = new TerrainGeneratorComponent
        {
            BoundsSize = size, Seed = seed,
            UseClimateBiomeMaps = true, UseScaleRules = true,
            BiomeCoherencePasses = 2, LakeShoreWidth = 0.5f,
        };
        try
        {
            generator.ApplyMapSetup(shape, 1, 1, 1, 1, 1);
            return generator.CaptureGenerationSettings();
        }
        finally { generator.Free(); }
    }

    /// <summary>SHA-256 per published layer, keyed by layer name.</summary>
    public Godot.Collections.Dictionary Snapshot(Vector2I size, int seed, int shape)
    {
        GeneratedTerrainField field = TerrainFieldBuilder.Build(Settings(size, seed, shape));
        int samples = field.Diagnostics.SamplesPerCell;
        var layers = new Godot.Collections.Dictionary();

        layers["terrain"] = Cells(size, (h, c) => Text(h, field.TerrainAtCell(c)));
        layers["inland_terrain"] = Cells(size, (h, c) => Text(h, field.InlandTerrainAtCell(c)));
        layers["water_source"] = Cells(size, (h, c) => Text(h, field.WaterSourceAtCell(c)));
        layers["continent"] = Cells(size, (h, c) => Int(h, field.ContinentAtCell(c)));
        layers["resource"] = Cells(size, (h, c) => Text(h, field.ResourceAtCell(c)));
        layers["liquid_resource"] = Cells(size, (h, c) => Text(h, field.LiquidResourceAtCell(c)));
        layers["underground_resource"] = Cells(size, (h, c) => Text(h, field.UndergroundResourceAtCell(c)));
        layers["underground_richness"] = Cells(size, (h, c) => Single(h, field.UndergroundRichnessAtCell(c)));
        layers["underground_depth"] = Cells(size, (h, c) => Int(h, field.UndergroundDepthAtCell(c)));
        layers["relief"] = Cells(size, (h, c) => Int(h, (int)field.ReliefAtCell(c)));
        layers["elevation"] = Cells(size, (h, c) => Single(h, field.ElevationAtCell(c)));
        layers["feature"] = Cells(size, (h, c) => Text(h, field.FeatureAtCell(c)));

        // The painter's view: the sub-tile samples, at their centres.
        var fine = new Vector2I(field.Diagnostics.FieldWidth, field.Diagnostics.FieldHeight);
        layers["sample_terrain"] = Samples(fine, samples, (h, p) => Text(h, field.TerrainAtPosition(p)));
        layers["sample_water"] = Samples(fine, samples, (h, p) => Int(h, field.IsWaterAtPosition(p) ? 1 : 0));
        layers["sample_shade"] = Samples(fine, samples, (h, p) => Single(h, field.ShadeAtPosition(p)));

        using (IncrementalHash starts = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            foreach (Vector2I start in field.StartPositions) { Int(starts, start.X); Int(starts, start.Y); }
            Single(starts, field.BeachWidth);
            Single(starts, field.LakeShoreWidth);
            layers["starts_and_shores"] = Convert.ToHexString(starts.GetHashAndReset());
        }
        return layers;
    }

    /// <summary>
    /// Managed bytes this thread allocated across one build, with the split per
    /// stage written into <paramref name="perStage"/> (stage name to bytes).
    /// </summary>
    public long AllocatedBytes(Vector2I size, int seed, int shape, Godot.Collections.Dictionary perStage)
    {
        TerrainGenerationSettings settings = Settings(size, seed, shape);
        TerrainResourceRules resources = TerrainResourceRules.Capture(settings);
        TerrainGenerationSettings detached = settings with { ResourceCatalog = null };
        var stages = new Dictionary<string, long>();
        string stage = "Buffer";
        long start = GC.GetAllocatedBytesForCurrentThread();
        long mark = start;
        void Progress(string next, int completed)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            stages[stage] = stages.GetValueOrDefault(stage) + (now - mark);
            stage = next;
            mark = now;
        }
        GeneratedTerrainField field = TerrainFieldBuilder.BuildPrepared(detached, resources, default, Progress);
        long end = GC.GetAllocatedBytesForCurrentThread();
        stages[stage] = stages.GetValueOrDefault(stage) + (end - mark);
        GC.KeepAlive(field);
        foreach ((string name, long bytes) in stages) perStage[name] = bytes;
        return end - start;
    }

    private static string Cells(Vector2I size, Action<IncrementalHash, Vector2I> visit)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int y = 0; y < size.Y; y++)
        for (int x = 0; x < size.X; x++)
            visit(hash, new Vector2I(x, y));
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string Samples(Vector2I fine, int samplesPerCell, Action<IncrementalHash, Vector2> visit)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int y = 0; y < fine.Y; y++)
        for (int x = 0; x < fine.X; x++)
            visit(hash, new Vector2((x + 0.5f) / samplesPerCell, (y + 0.5f) / samplesPerCell));
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Text(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData(new byte[] { 0 });
    }

    private static void Int(IncrementalHash hash, int value) => hash.AppendData(BitConverter.GetBytes(value));

    private static void Single(IncrementalHash hash, float value)
        => hash.AppendData(BitConverter.GetBytes(BitConverter.SingleToInt32Bits(value)));
}
