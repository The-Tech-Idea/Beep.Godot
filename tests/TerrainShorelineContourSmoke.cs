using Beep.ECS;
using Godot;
using System;

public partial class TerrainShorelineContourSmoke : Node
{
    private readonly TerrainShorelineField _field = new();

    public ImageTexture BuildFixture(string name)
    {
        return _field.Build(new Vector2I(64, 40), 12, at => name switch
        {
            "circle" => at.DistanceTo(new Vector2(32, 20)) >= 14,
            "cove" => at.DistanceTo(new Vector2(32, 20)) >= 17
                || at.DistanceTo(new Vector2(43, 14)) < 12,
            "narrow" => Math.Abs(at.Y - 20) >= 1.25f,
            _ => throw new ArgumentException("Unknown fixture")
        });
    }

    public bool Run()
    {
        var random = new Random(4251);
        for (int trial = 0; trial < 24; trial++)
        {
            var size = new Vector2I(1 + random.Next(19), 1 + random.Next(17));
            var mask = new bool[size.X * size.Y];
            for (int i = 0; i < mask.Length; i++) mask[i] = trial == 0 || (trial > 1 && random.Next(3) == 0);
            foreach (bool seed in new[] { false, true })
            {
                double[] actual = TerrainEuclideanDistance.Squared(mask, size, seed);
                for (int y = 0; y < size.Y; y++)
                for (int x = 0; x < size.X; x++)
                {
                    double expected = double.PositiveInfinity;
                    for (int sy = 0; sy < size.Y; sy++)
                    for (int sx = 0; sx < size.X; sx++)
                        if (mask[sy * size.X + sx] == seed)
                            expected = Math.Min(expected, (x - sx) * (x - sx) + (y - sy) * (y - sy));
                    Check(actual[y * size.X + x] == expected, "EDT differs from brute-force Euclidean oracle");
                }
            }
            float[] signed = TerrainEuclideanDistance.Signed(mask, size, 4);
            for (int i = 0; i < signed.Length; i++)
                Check(float.IsFinite(signed[i]) && (signed[i] > 0) == mask[i], "Uniform mask sign/finite contract failed");
        }

        BuildFixture("circle");
        float largestError = 0;
        foreach (float width in new[] { 0f, 0.25f, 1f, 3f, 4f })
        for (int angle = 0; angle < 360; angle += 3)
        {
            Vector2 direction = Vector2.FromAngle(Mathf.DegToRad(angle));
            float low = 0, high = 18;
            for (int iteration = 0; iteration < 22; iteration++)
            {
                float mid = (low + high) / 2;
                if (_field.SampleDistance(new Vector2(32, 20) + direction * mid) < -width) low = mid;
                else high = mid;
            }
            largestError = Math.Max(largestError, Math.Abs((low + high) / 2 - (14 - width)));
        }
        Check(largestError < 0.10f, $"Circular inset deviates by {largestError} cells");
        GD.Print($"[shoreline-contours] 600 circular contour probes; maximum error={largestError:F5} cells");

        BuildFixture("cove");
        // Independent analytic distances on the inward-facing circular bay.
        for (int angle = 130; angle <= 250; angle += 5)
        {
            Vector2 direction = Vector2.FromAngle(Mathf.DegToRad(angle));
            foreach (float width in new[] { 0.25f, 1f, 3f })
            {
                Vector2 at = new Vector2(43, 14) + direction * (12 + width);
                if (at.DistanceTo(new Vector2(32, 20)) > 17 - width - 0.25f) continue;
                Check(Math.Abs(_field.SampleDistance(at) + width) < 0.10f, "Concave bay does not follow the inward offset");
            }
        }
        BuildFixture("narrow");
        Check(Math.Abs(_field.SampleDistance(new Vector2(32, 20)) + 1.25f) < 0.1f, "Narrow strip distance is wrong");
        for (float y = 18.8f; y < 21.2f; y += 0.05f)
            Check(_field.SampleDistance(new Vector2(32, y)) >= -1.5f, "Wide beach left a false grass strip");
        GD.Print("[shoreline-contours] brute-force EDT, empty/full masks, convex/concave offsets and narrow land OK");
        return true;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public void PopulateLakeFixture(GridCellDataComponent cells, float lakeWidth)
    {
        const int detail = 12;
        var size = new Vector2I(24, 18);
        var world = new TerrainGenerationBuffer(size.X * detail, size.Y * detail, detail);
        WaterBody Body(Vector2 at) => at.X < 2 ? WaterBody.Ocean
            : at.DistanceTo(new Vector2(11, 9)) < 4 ? WaterBody.Lake
            : at.X > 20 && at.X < 21 && at.Y > 3 && at.Y < 15 ? WaterBody.River : WaterBody.None;
        for (int y = 0; y < world.Height; y++)
        for (int x = 0; x < world.Width; x++)
        {
            int i = world.Index(x, y);
            world.Water[i] = Body(new Vector2(x + 0.5f, y + 0.5f) / detail);
            world.Land[i] = world.Water[i] == WaterBody.None;
            world.Terrain[i] = world.Land[i] ? "grass" : "shallow_water";
        }
        for (int y = 0; y < size.Y; y++)
        for (int x = 0; x < size.X; x++)
        {
            int i = world.CellIndex(x, y);
            world.CellWater[i] = Body(new Vector2(x + 0.5f, y + 0.5f));
            world.CellTerrain[i] = world.CellWater[i] == WaterBody.None ? "grass" : "shallow_water";
        }
        TerrainShorelineStage.Apply(world, default(TerrainGenerationSettings) with
        { Preset = TerrainPreset.Grassland, BeachWidth = 0.5f, LakeShoreWidth = lakeWidth });
        int checkedSamples = 0;
        for (int y = 0; y < world.Height; y++)
        for (int x = 0; x < world.Width; x++)
        {
            int i = world.Index(x, y);
            if (!world.Land[i]) continue;
            Vector2 at = new Vector2(x + 0.5f, y + 0.5f) / detail;
            float distance = at.DistanceTo(new Vector2(11, 9)) - 4;
            if (Math.Abs(distance - lakeWidth) < 0.15f || Math.Abs(at.X - 2.5f) < 0.15f) continue;
            bool sand = at.X < 2.5f || (lakeWidth > 0f && distance < lakeWidth);
            Check((world.Terrain[i] == "sand") == sand, "Lake bank differs from analytic circular offset or leaked onto river");
            checkedSamples++;
        }
        var data = new System.Collections.Generic.List<(Vector2I, string, string, int, float, float,
            string, GridTerrainWaterPatch, string, float, GridTerrainWaterPatch?, float)>();
        for (int y = 0; y < size.Y; y++)
        for (int x = 0; x < size.X; x++)
        {
            int i = world.CellIndex(x, y);
            var cell = new Vector2I(x, y);
            var water = GridTerrainWaterPatch.Create(detail, (sx, sy) => world.Water[world.Index(x * detail + sx, y * detail + sy)] != WaterBody.None);
            var lake = GridTerrainWaterPatch.Create(detail, (sx, sy) => world.Water[world.Index(x * detail + sx, y * detail + sy)] == WaterBody.Lake);
            data.Add((cell, world.CellTerrain[i], "", 0, 1f, 0f, world.CellWater[i].ToString().ToLowerInvariant(),
                water, world.CellInlandTerrain[i], world.BeachWidth, lake, world.LakeShoreWidth));
        }
        cells.LoadGeneratedCells(data);
        var restored = new GridCellDataComponent();
        restored.LoadCells(cells.GetCells());
        for (int y = 0; y < size.Y; y++)
        for (int x = 0; x < size.X; x++)
        {
            var cell = new Vector2I(x, y);
            Check(cells.LakePatchAtCell(cell)!.Encode() == restored.LakePatchAtCell(cell)!.Encode(), "Save/load lost lake membership");
            Check(cells.ShoreAtCell(cell) == restored.ShoreAtCell(cell), "Save/load lost lake width");
        }
        restored.SetTerrainKind(new Vector2I(11, 9), "grass");
        Check(restored.LakePatchAtCell(new Vector2I(11, 9)) is null, "Painting retained a stale lake");
        restored.FillTerrain(new Rect2I(10, 8, 3, 3), "grass");
        Check(restored.LakePatchAtCell(new Vector2I(10, 8)) is null, "Fill retained a stale lake");
        restored.Free();
        GD.Print($"[lake-banks] width={lakeWidth}: {checkedSamples} analytic samples; separate ocean/river, save/load and edits OK");
    }

    public bool RunGenerated()
    {
        var generator = new TerrainGeneratorComponent
        {
            BoundsSize = new Vector2I(24, 24), TopologySamplesPerCell = 4,
            LakeCoverage = 0, RiverDensity = 0, StartPositionCount = 0,
            ResourceDensity = 0, FeatureDensity = 0
        };
        AddChild(generator);
        var cells = new GridCellDataComponent { Name = "Cells" };
        generator.AddChild(cells);
        generator.CellDataPath = new NodePath("Cells");
        foreach (int seed in new[] { 31415, 8675309, 42 })
        foreach (var landform in Enum.GetValues<TerrainGeneratorComponent.LandformMode>())
        {
            generator.Seed = seed;
            generator.Landform = landform;
            bool[]? originalMask = null;
            foreach (float width in new[] { 0f, 0.25f, 1f, 3f })
            {
                generator.BeachWidth = width;
                GeneratedTerrainField field = generator.ResolveField();
                const int detail = 4, side = 24 * detail;
                var water = new bool[side * side];
                for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    water[y * side + x] = field.IsWaterAtPosition(new Vector2(x + 0.5f, y + 0.5f) / detail);
                if (originalMask is not null) Check(water.AsSpan().SequenceEqual(originalMask), "Beach changed generated water footprint");
                originalMask = water;
                int radius = (int)Math.Ceiling(width * detail + 0.5f);
                for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    if (water[y * side + x]) continue;
                    double nearest = double.PositiveInfinity;
                    for (int sy = Math.Max(0, y - radius); sy <= Math.Min(side - 1, y + radius); sy++)
                    for (int sx = Math.Max(0, x - radius); sx <= Math.Min(side - 1, x + radius); sx++)
                        if (water[sy * side + sx]) nearest = Math.Min(nearest, (sx - x) * (sx - x) + (sy - y) * (sy - y));
                    bool sand = width > 0 && (Math.Sqrt(nearest) - 0.5) / detail <= width;
                    string kind = field.TerrainAtPosition(new Vector2(x + 0.5f, y + 0.5f) / detail);
                    Check((kind == "sand") == sand, $"Fine shore mismatch seed={seed}, form={landform}, width={width} at {x},{y}: {kind}");
                }
            }
        }
        generator.GenerateTerrain();
        var restored = new GridCellDataComponent();
        AddChild(restored);
        restored.LoadCells(cells.GetCells());
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            var cell = new Vector2I(x, y);
            Check(cells.ShoreAtCell(cell) == restored.ShoreAtCell(cell), "Save/load lost shore authority");
        }
        restored.SetTerrainKind(new Vector2I(12, 12), "gravel");
        Check(restored.ShoreAtCell(new Vector2I(12, 12)) == ("gravel", 0f, 0f), "Explicit paint retained generated beach");
        restored.FillTerrain(new Rect2I(1, 1, 3, 3), "grass");
        Check(restored.ShoreAtCell(new Vector2I(2, 2)) == ("grass", 0f, 0f), "Fill retained generated beach");
        restored.Free();
        generator.Free();
        GD.Print("[terrain-coastal-grass] 36 generated masks: fine offsets, fixed water, live handoff, edits and save/load OK");
        return true;
    }
}
