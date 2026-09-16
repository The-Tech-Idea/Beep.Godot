using Beep.ECS;
using Godot;
using System;
using System.Collections.Generic;

public partial class TerrainFinalTopologySmoke : Node
{
    public bool Run()
    {
        bool passed = true;
        passed &= VerifyBiomeGroundCover();
        passed &= VerifyReliefCleanup();
        passed &= VerifyThemedRelief();
        passed &= VerifyLavaStarts();
        passed &= VerifyWaterFringes();
        var settings = default(TerrainGenerationSettings) with { UseScaleRules = true };
        foreach (WaterBody water in new[] { WaterBody.River, WaterBody.Lake })
        {
            var world = new TerrainGenerationBuffer(36, 20, 4);
            Array.Fill(world.Land, true);
            Array.Fill(world.Terrain, "grass");
            // A five-cell channel divides the grid until scale cleanup drains it.
            for (int y = 0; y < world.Height; y++)
            for (int x = 16; x < 20; x++)
            {
                int sample = world.Index(x, y);
                world.Land[sample] = false;
                world.Water[sample] = water;
                world.Terrain[sample] = "shallow_water";
            }
            TerrainTileReductionStage.Apply(world);
            TerrainScaleConstraintStage.ApplyTerrain(world, settings);
            TerrainContinentStage.Apply(world);
            int stale = 0;
            for (int i = 0; i < world.Count; i++)
                if (!world.Land[i] || world.Water[i] != WaterBody.None || world.Terrain[i] != "grass") stale++;
            passed &= Check(stale == 0, $"{water} cleanup left {stale} stale painter samples");
            foreach (int id in world.CellContinent)
                passed &= Check(id == 1, $"{water} cleanup did not reconnect dry land");
        }

        // Check the actual pipeline, not just the stage ordering in the fixture.
        var generator = new TerrainGeneratorComponent
        {
            BoundsSize = new Vector2I(32, 32), TopologySamplesPerCell = 4,
            LandmassScale = 0.42f, ResourceDensity = 0, FeatureDensity = 0,
            StartPositionCount = 0
        };
        foreach (bool rules in new[] { false, true })
        foreach (int seed in new[] { 31415, 12345, 98765, 8675309 })
        {
            generator.UseScaleRules = rules;
            generator.Seed = seed;
            passed &= VerifyRegions(generator, $"seed={seed} scale={rules}");
        }
        generator.Free();
        return passed;
    }

    private static bool VerifyBiomeGroundCover()
    {
        bool passed = true;
        foreach (bool climate in new[] { false, true })
        foreach (TerrainRelief relief in Enum.GetValues<TerrainRelief>())
        {
            var world = new TerrainGenerationBuffer(16, 16, 4);
            Array.Fill(world.Land, true);
            Array.Fill(world.Relief, relief);
            Array.Fill(world.Elevation, 0.9f);
            Array.Fill(world.Temperature, 0.6f);
            Array.Fill(world.Moisture, 0.55f);
            var settings = default(TerrainGenerationSettings) with
            {
                Preset = TerrainPreset.Grassland, UseClimateBiomeMaps = climate
            };
            TerrainBiomeStage.Apply(world, settings);
            TerrainTileReductionStage.Apply(world);
            for (int i = 0; i < world.Count; i++)
                passed &= Check(world.Terrain[i] == "grass", $"{relief} replaced green ground with {world.Terrain[i]}");
            foreach (TerrainRelief actual in world.CellRelief)
                passed &= Check(actual == relief, "Ground cover change flattened relief");
        }
        // A raised meadow beside stone is still a meadow, not a rock expansion candidate.
        var patch = new TerrainGenerationBuffer(8, 8, 1);
        Array.Fill(patch.Land, true);
        Array.Fill(patch.Terrain, "rock");
        Array.Fill(patch.Relief, TerrainRelief.Mountains);
        patch.Terrain[patch.Index(3, 3)] = "grass";
        TerrainCoherenceStage.Apply(patch, default(TerrainGenerationSettings) with { MinBiomeRegionFraction = 0.2f });
        passed &= Check(patch.Terrain[patch.Index(3, 3)] == "grass", "Coherence absorbed a raised meadow into rock");
        return passed;
    }

    private static bool VerifyReliefCleanup()
    {
        bool passed = true;
        foreach (TerrainRelief relief in new[] { TerrainRelief.Flat, TerrainRelief.Hills, TerrainRelief.Mountains })
        foreach (string peak in new[] { "rock", "snow", "gravel" })
        foreach (bool rules in new[] { false, true })
        {
            var world = new TerrainGenerationBuffer(36, 36, 4);
            Array.Fill(world.Land, true);
            Array.Fill(world.Terrain, "grass");
            Array.Fill(world.Elevation, 0.35f);
            Array.Fill(world.Shade, 0.92f);
            for (int y = 16; y < 20; y++)
            for (int x = 16; x < 20; x++)
            {
                int i = world.Index(x, y);
                world.Terrain[i] = peak;
                world.Relief[i] = relief;
                world.Elevation[i] = 0.65f;
            }
            int wet = world.Index(16, 16);
            world.Land[wet] = false;
            world.Water[wet] = WaterBody.Ocean;
            world.Terrain[wet] = "shallow_water";
            world.Relief[wet] = TerrainRelief.Flat;
            int detail = world.Index(17, 16);
            world.Terrain[detail] = "sand";
            TerrainTileReductionStage.Apply(world);
            int cell = world.CellIndex(4, 4);
            float elevation = world.CellElevation[cell];
            var beforeLand = (bool[])world.Land.Clone();
            var beforeWater = (WaterBody[])world.Water.Clone();
            var beforeHeight = (float[])world.Elevation.Clone();
            var beforeShade = (float[])world.Shade.Clone();
            TerrainScaleConstraintStage.ApplyTerrain(world,
                default(TerrainGenerationSettings) with { UseScaleRules = rules });
            passed &= Check(world.CellTerrain[cell] == (rules ? "grass" : peak), "peak cell material cleanup");
            passed &= Check(world.CellRelief[cell] == (rules ? TerrainRelief.Flat : relief), "peak cell relief cleanup");
            int errors = 0;
            for (int i = 0; i < world.Count; i++)
            {
                if (world.Land[i] != beforeLand[i] || world.Water[i] != beforeWater[i]
                    || world.Elevation[i] != beforeHeight[i] || world.Shade[i] != beforeShade[i]) errors++;
                int x = i % world.Width;
                int y = i / world.Width;
                bool inside = x >= 16 && x < 20 && y >= 16 && y < 20;
                string expected = !inside ? "grass" : i == wet ? "shallow_water" : i == detail ? "sand" : rules ? "grass" : peak;
                if (world.Terrain[i] != expected) errors++;
                if (inside && i != wet && world.Relief[i] != (rules ? TerrainRelief.Flat : relief)) errors++;
            }
            passed &= Check(errors == 0 && world.CellElevation[cell] == elevation,
                $"relief={relief} peak={peak} scale={rules}: {errors} sample mismatches; cleanup must preserve water, detail, elevation and shade");
            var field = new GeneratedTerrainField(world, default);
            passed &= Check(field.TerrainAtCell(new Vector2I(4, 4)) == field.TerrainAtPosition(new Vector2(4.5f, 4.5f)),
                $"relief={relief} scale={rules}: renderer material queries disagree after cleanup");
        }
        // A real range at the minimum size must survive, including fine detail.
        var retained = new TerrainGenerationBuffer(36, 36, 4);
        Array.Fill(retained.Land, true);
        Array.Fill(retained.Terrain, "grass");
        for (int y = 12; y < 20; y++)
        for (int x = 12; x < 24; x++)
        {
            int sample = retained.Index(x, y);
            retained.Terrain[sample] = "rock";
            retained.Relief[sample] = TerrainRelief.Mountains;
        }
        TerrainTileReductionStage.Apply(retained);
        var materials = (string[])retained.Terrain.Clone();
        var reliefs = (TerrainRelief[])retained.Relief.Clone();
        TerrainScaleConstraintStage.ApplyTerrain(retained,
            default(TerrainGenerationSettings) with { UseScaleRules = true });
        int changed = 0;
        for (int i = 0; i < retained.Count; i++)
            if (materials[i] != retained.Terrain[i] || reliefs[i] != retained.Relief[i]) changed++;
        passed &= Check(changed == 0, "minimum-size range was removed or repainted");
        return passed;
    }

    private static bool VerifyLavaStarts()
    {
        var world = new TerrainGenerationBuffer(36, 36, 4);
        Array.Fill(world.Land, true);
        Array.Fill(world.Terrain, "lava");
        TerrainTileReductionStage.Apply(world);
        TerrainContinentStage.Apply(world);
        var settings = default(TerrainGenerationSettings) with { StartPositionCount = 1 };
        var kit = TerrainStartKitRules.Capture(settings);
        TerrainStartPositionStage.Apply(world, settings, kit);
        bool passed = Check(world.StartPositions.Count == 0, "Starting position accepted blocked lava");
        int dry = world.CellIndex(4, 4);
        world.CellTerrain[dry] = "grass";
        TerrainStartPositionStage.Apply(world, settings, kit);
        return passed & Check(world.StartPositions.Count == 1 && world.StartPositions[0] == new Vector2I(4, 4),
            "Start selection did not choose the only habitable cell beside lava");
    }

    private static bool VerifyThemedRelief()
    {
        bool passed = true;
        foreach (TerrainPreset preset in new[] { TerrainPreset.Lava, TerrainPreset.Rock,
            TerrainPreset.Snow, TerrainPreset.Ice, TerrainPreset.Desert, TerrainPreset.Sand, TerrainPreset.Swamp })
        foreach (TerrainRelief relief in new[] { TerrainRelief.Flat, TerrainRelief.Hills, TerrainRelief.Mountains })
        {
            var world = new TerrainGenerationBuffer(36, 36, 4);
            Array.Fill(world.Land, true);
            Array.Fill(world.Terrain, "rock");
            Array.Fill(world.Elevation, 0.4f);
            Array.Fill(world.Shade, 0.9f);
            for (int y = 16; y < 20; y++)
            for (int x = 16; x < 20; x++)
                world.Relief[world.Index(x, y)] = relief;
            world.Terrain[world.Index(17, 16)] = "lava";
            int wet = world.Index(16, 16);
            world.Terrain[wet] = "shallow_water";
            world.Land[wet] = false;
            world.Water[wet] = WaterBody.Ocean;
            TerrainTileReductionStage.Apply(world);
            var terrain = (string[])world.Terrain.Clone();
            var cellTerrain = (string[])world.CellTerrain.Clone();
            var water = (WaterBody[])world.Water.Clone();
            var land = (bool[])world.Land.Clone();
            var height = (float[])world.Elevation.Clone();
            var shade = (float[])world.Shade.Clone();
            TerrainScaleConstraintStage.ApplyTerrain(world,
                default(TerrainGenerationSettings) with { UseScaleRules = true, Preset = preset });
            passed &= Check(terrain.AsSpan().SequenceEqual(world.Terrain)
                && cellTerrain.AsSpan().SequenceEqual(world.CellTerrain),
                $"{preset}/{relief}: relief cleanup replaced themed ground or fine material detail");
            passed &= Check(water.AsSpan().SequenceEqual(world.Water) && land.AsSpan().SequenceEqual(world.Land)
                && height.AsSpan().SequenceEqual(world.Elevation) && shade.AsSpan().SequenceEqual(world.Shade),
                $"{preset}/{relief}: material cleanup changed water, height or shade");
            passed &= Check(world.CellRelief[world.CellIndex(4, 4)] == TerrainRelief.Flat,
                $"{preset}/{relief}: themed ground disabled relief size cleanup");
        }
        return passed;
    }

    private static bool VerifyWaterFringes()
    {
        bool passed = true;
        foreach (WaterBody kind in new[] { WaterBody.Lake, WaterBody.River })
        foreach (bool rules in new[] { false, true })
        foreach (int length in new[] { 5, kind == WaterBody.Lake ? 8 : 6 })
        {
            var world = new TerrainGenerationBuffer(96, 96, 8);
            Array.Fill(world.Land, true);
            Array.Fill(world.Terrain, "grass");
            Array.Fill(world.Footprint, true);
            for (int y = 8; y < 8 + length * 8; y++)
            for (int x = 16; x < 24; x++)
            {
                int sample = world.Index(x, y);
                world.Land[sample] = false;
                world.Water[sample] = kind;
                world.Terrain[sample] = "shallow_water";
            }
            // One-sample fringes cross into cells whose majority stays dry.
            for (int y = 12; y < 8 + length * 8; y += 8)
            {
                int sample = world.Index(15, y);
                world.Land[sample] = false;
                world.Water[sample] = kind;
                world.Terrain[sample] = "shallow_water";
            }
            var originalWater = (WaterBody[])world.Water.Clone();
            var originalMaterial = (string[])world.Terrain.Clone();
            TerrainTileReductionStage.Apply(world);
            TerrainScaleConstraintStage.ApplyTerrain(world,
                default(TerrainGenerationSettings) with { UseScaleRules = rules });
            int wet = 0;
            foreach (WaterBody value in world.Water) if (value != WaterBody.None) wet++;
            int expected = rules && length == 5 ? 0 : length * 65;
            passed &= Check(wet == expected,
                $"{kind} length={length} scale={rules}: expected {expected} fine water samples including fringes, found {wet}");
            if (expected > 0)
                passed &= Check(originalWater.AsSpan().SequenceEqual(world.Water)
                    && originalMaterial.AsSpan().SequenceEqual(world.Terrain), "Retained water body lost its fine shape/material");
            else
                for (int i = 0; i < world.Count; i++)
                    passed &= Check(world.Land[i] && world.Terrain[i] == "grass", "Drained sample retained a water material or land flag");
            foreach (bool footprint in world.Footprint)
                passed &= Check(footprint, "Inland cleanup changed the landmass footprint");
        }
        var mouth = new TerrainGenerationBuffer(32, 32, 8);
        Array.Fill(mouth.Land, true);
        Array.Fill(mouth.Terrain, "grass");
        for (int y = 16; y < 24; y++)
        for (int x = 16; x < 24; x++)
        {
            int at = mouth.Index(x, y);
            mouth.Water[at] = WaterBody.Ocean;
            mouth.Terrain[at] = "deep_water";
            mouth.Land[at] = false;
        }
        int riverMouth = mouth.Index(16, 18);
        mouth.Water[riverMouth] = WaterBody.River;
        TerrainTileReductionStage.Apply(mouth);
        TerrainScaleConstraintStage.ApplyTerrain(mouth,
            default(TerrainGenerationSettings) with { UseScaleRules = true });
        passed &= Check(mouth.Water[riverMouth] == WaterBody.Ocean && !mouth.Land[riverMouth],
            "Orphan river mouth became a dry hole inside an ocean cell");
        for (int y = 16; y < 24; y++)
        for (int x = 16; x < 24; x++)
            passed &= Check(mouth.Water[mouth.Index(x, y)] == WaterBody.Ocean, "Ocean sample was drained");
        return passed;
    }

    private static bool VerifyRegions(TerrainGeneratorComponent generator, string context)
    {
        Vector2I size = generator.BoundsSize;
        var expected = new int[size.X * size.Y];
        var water = new bool[expected.Length];
        for (int i = 0; i < water.Length; i++)
            water[i] = generator.WaterSourceAt(new Vector2I(i % size.X, i / size.X)).Length > 0;
        var queue = new Queue<int>();
        int count = 0;
        for (int start = 0; start < expected.Length; start++)
        {
            if (water[start] || expected[start] != 0) continue;
            expected[start] = ++count;
            queue.Enqueue(start);
            while (queue.TryDequeue(out int current))
            {
                var cell = new Vector2I(current % size.X, current / size.X);
                foreach (Vector2I delta in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
                {
                    Vector2I next = cell + delta;
                    if (next.X < 0 || next.Y < 0 || next.X >= size.X || next.Y >= size.Y) continue;
                    int at = next.Y * size.X + next.X;
                    if (water[at] || expected[at] != 0) continue;
                    expected[at] = count;
                    queue.Enqueue(at);
                }
            }
        }
        int mismatches = 0;
        for (int i = 0; i < expected.Length; i++)
            if (generator.ContinentAt(new Vector2I(i % size.X, i / size.X)) != expected[i]) mismatches++;
        int reported = generator.GetGenerationDiagnostics()["continent_count"].AsInt32();
        GD.Print($"[terrain-final-topology] {context}: dry regions={count}, reported={reported}, mismatched cells={mismatches}");
        return Check(mismatches == 0 && reported == count, $"{context}: continent metadata differs from final water grid")
            & (!generator.UseScaleRules || VerifyFineWaterRegions(generator, context));
    }

    private static bool VerifyFineWaterRegions(TerrainGeneratorComponent generator, string context)
    {
        var field = generator.ResolveField();
        int detail = field.Diagnostics.SamplesPerCell;
        int width = generator.BoundsSize.X * detail, height = generator.BoundsSize.Y * detail;
        var wet = new bool[width * height];
        var seen = new bool[wet.Length];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            wet[y * width + x] = field.IsWaterAtPosition(new Vector2((x + 0.5f) / detail, (y + 0.5f) / detail));
        int orphanSamples = 0;
        var queue = new Queue<int>();
        for (int start = 0; start < wet.Length; start++)
        {
            if (!wet[start] || seen[start]) continue;
            queue.Enqueue(start);
            seen[start] = true;
            int count = 0;
            bool anchored = false;
            while (queue.TryDequeue(out int at))
            {
                count++;
                int x = at % width, y = at / width;
                anchored |= field.WaterSourceAtCell(new Vector2I(x / detail, y / detail)) != "";
                foreach (Vector2I delta in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
                {
                    int nx = x + delta.X, ny = y + delta.Y;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int next = ny * width + nx;
                    if (!wet[next] || seen[next]) continue;
                    seen[next] = true;
                    queue.Enqueue(next);
                }
            }
            if (!anchored) orphanSamples += count;
        }
        GD.Print($"[terrain-final-topology] {context}: orphan fine water samples={orphanSamples}");
        return Check(orphanSamples == 0, $"{context}: fine water body has no surviving gameplay water cell");
    }

    private static bool Check(bool condition, string message)
    {
        if (!condition) GD.PrintErr($"[terrain-final-topology] FAIL: {message}");
        return condition;
    }
}
