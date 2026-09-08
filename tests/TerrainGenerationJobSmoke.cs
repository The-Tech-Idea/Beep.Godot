using Beep.ECS;
using Godot;
using System;

public partial class TerrainGenerationJobSmoke : Node
{
    private TerrainGenerationJob? _job;
    private TerrainGenerationJob? _cancelled;
    private GeneratedTerrainField? _reference;
    private ResourceDefinition? _definition;
    private ResourceCatalog? _catalog;
    private int _previousProgress;
    private bool _progressValid = true;
    private readonly Vector2I _size = new(64, 40);

    public void Start()
    {
        _definition = new ResourceDefinition
        {
            Id = "test_ore", Stratum = ResourceStratum.Underground, DepositScale = 1,
            TerrainKinds = new() { "grass", "dry_grass", "desert", "rock", "shallow_water", "deep_water", "sand" }
        };
        _catalog = new ResourceCatalog { Resources = new() { _definition } };
        var settings = default(TerrainGenerationSettings) with
        {
            Size = _size, Seed = 31415, Mode = TerrainMode.ProceduralNoise,
            Landform = TerrainGeneratorComponent.LandformMode.Island,
            LandmassScale = 0.65f, ArchipelagoIslandCount = 4, TopologySamplesPerCell = 6,
            Frequency = 0.015f, Octaves = 4, Lacunarity = 2, Gain = 0.5f,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            BeachWidth = 1, LakeCoverage = 0.05f, LakeFrequencyMultiplier = 1,
            ErosionStrength = 1, HillsFraction = 0.1f, MountainsFraction = 0.05f,
            ResourceCatalog = _catalog, ResourceDensity = 1, FeatureDensity = 1,
            FeatureFrequencyMultiplier = 1, TemperatureFrequencyMultiplier = 1, MoistureFrequencyMultiplier = 1,
            ClimateLatitudeSpan = 0.6f, ClimateLatitudeCentre = 0.5f, BiomeCoherenceKeep = 5,
            UseClimateBiomeMaps = true, StartPositionCount = 0
        };
        _reference = TerrainFieldBuilder.Build(settings);
        _job = new TerrainGenerationJob(settings);
        _cancelled = new TerrainGenerationJob(settings with { Seed = 27182 });
        _cancelled.Cancel();
        // Neither edits nor destruction of authored resources may reach a worker.
        _definition.Id = "changed_while_running";
        _definition.TerrainKinds.Clear();
        _catalog.Resources.Clear();
        _definition.Dispose();
        _catalog.Dispose();
    }

    public int Poll()
    {
        if (_job is null || _cancelled is null || _reference is null) return -1;
        int completed = _job.CurrentProgress.CompletedStages;
        _progressValid &= completed >= _previousProgress && completed <= 20;
        _previousProgress = completed;
        if (!_job.Completion.IsCompleted || !_cancelled.Completion.IsCompleted) return 0;
        try
        {
            if (!_cancelled.Completion.IsCanceled) return Fail("Cancelled worker published a result");
            if (!_progressValid || completed != 20 || _job.CurrentProgress.Stage != "Complete")
                return Fail("Invalid stage progress");
            GeneratedTerrainField actual = _job.Completion.GetAwaiter().GetResult();
            int deposits = 0;
            for (int y = 0; y < _size.Y; y++)
            for (int x = 0; x < _size.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (actual.TerrainAtCell(cell) != _reference.TerrainAtCell(cell)
                    || actual.WaterSourceAtCell(cell) != _reference.WaterSourceAtCell(cell)
                    || actual.ReliefAtCell(cell) != _reference.ReliefAtCell(cell)
                    || actual.ElevationAtCell(cell) != _reference.ElevationAtCell(cell)
                    || actual.FeatureAtCell(cell) != _reference.FeatureAtCell(cell)
                    || actual.ResourceAtCell(cell) != _reference.ResourceAtCell(cell)
                    || actual.LiquidResourceAtCell(cell) != _reference.LiquidResourceAtCell(cell)
                    || actual.UndergroundResourceAtCell(cell) != _reference.UndergroundResourceAtCell(cell)
                    || actual.UndergroundRichnessAtCell(cell) != _reference.UndergroundRichnessAtCell(cell)
                    || actual.UndergroundDepthAtCell(cell) != _reference.UndergroundDepthAtCell(cell))
                    return Fail($"Worker changed field at {cell}");
                if (actual.UndergroundResourceAtCell(cell) == "test_ore") deposits++;
                for (int sy = 0; sy < 6; sy++)
                for (int sx = 0; sx < 6; sx++)
                {
                    var point = new Vector2(x + (sx + 0.5f) / 6, y + (sy + 0.5f) / 6);
                    if (actual.TerrainAtPosition(point) != _reference.TerrainAtPosition(point)
                        || actual.WaterFractionAtPosition(point) != _reference.WaterFractionAtPosition(point)
                        || actual.ShadeAtPosition(point) != _reference.ShadeAtPosition(point))
                        return Fail($"Worker changed sub-cell field at {point}");
                }
            }
            if (deposits == 0) return Fail("Snapshot isolation fixture had no deposits");
            GD.Print($"[terrain-generation-job] synchronous/worker equality; {deposits} detached deposits; cancellation and progress OK");
            return 1;
        }
        catch (Exception error) { return Fail(error.ToString()); }
        finally
        {
            _job.Dispose();
            _cancelled.Dispose();
        }
    }

    private static int Fail(string message)
    {
        GD.PushError("[terrain-generation-job] " + message);
        return -1;
    }
}
