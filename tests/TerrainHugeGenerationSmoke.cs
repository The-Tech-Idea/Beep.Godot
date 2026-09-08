using Beep.ECS;
using Godot;
using System;
using System.Diagnostics;
using System.Collections.Generic;

public partial class TerrainHugeGenerationSmoke : Node
{
    private TerrainGenerationJob? _job;
    private readonly Stopwatch _timer = new();
    private readonly Dictionary<string, long> _stages = new();
    private string _stage = "Queued";
    private long _stageStart, _peakWorking, _peakManaged;
    private bool _timedOut;
    public bool CancelDuringErosion { get; set; }
    public bool CancelDuringStartPositions { get; set; }
    private long _cancelAt = -1;

    public void Start()
    {
        var generator = new TerrainGeneratorComponent
        {
            BoundsSize = new Vector2I(1024, 1024), Seed = 31415,
            Landform = TerrainGeneratorComponent.LandformMode.Island,
            LandmassScale = 0.65f, UseClimateBiomeMaps = true,
            UseScaleRules = true, StartPositionCount = 6
        };
        _timer.Start();
        try { _job = new TerrainGenerationJob(generator.CaptureGenerationSettings()); }
        finally { generator.Free(); }
    }

    public int Poll()
    {
        if (_job is null) return -1;
        using var process = Process.GetCurrentProcess();
        _peakWorking = Math.Max(_peakWorking, process.WorkingSet64);
        _peakManaged = Math.Max(_peakManaged, GC.GetTotalMemory(false));
        string stage = _job.CurrentProgress.Stage;
        if (_stage != stage)
        {
            long duration = _timer.ElapsedMilliseconds - _stageStart;
            _stages[_stage] = duration;
            GD.Print($"[terrain-huge] {_stage}: {duration} ms; next={stage}");
            _stage = stage;
            _stageStart = _timer.ElapsedMilliseconds;
        }
        if (_timer.Elapsed.TotalMinutes > 5 && !_timedOut)
        {
            _timedOut = true;
            _job.Cancel();
        }
        if ((CancelDuringErosion || CancelDuringStartPositions)
            && stage == (CancelDuringErosion ? "Erosion" : "Start positions") && _cancelAt < 0)
        {
            _cancelAt = _timer.ElapsedMilliseconds;
            _job.Cancel();
        }
        if (!_job.Completion.IsCompleted) return 0;
        try
        {
            if (CancelDuringErosion || CancelDuringStartPositions)
            {
                long latency = _timer.ElapsedMilliseconds - _cancelAt;
                if (_cancelAt < 0 || !_job.Completion.IsCanceled || latency > 2000)
                    throw new InvalidOperationException("Stage cancellation failed or exceeded the benchmark's two-second limit");
                GD.Print($"[terrain-huge] cancellation OK: {latency}ms; no field published");
                return 1;
            }
            var field = _job.Completion.GetAwaiter().GetResult();
            long generationMs = _timer.ElapsedMilliseconds;
            int dry = 0, water = 0, features = 0, resources = 0;
            for (int y = 0; y < 1024; y++)
            for (int x = 0; x < 1024; x++)
            {
                var cell = new Vector2I(x, y);
                string kind = field.TerrainAtCell(cell);
                bool wet = TerrainTileSets.IsWaterKind(kind);
                if (wet) water++; else dry++;
                float height = field.ElevationAtCell(cell);
                if (kind.Length == 0 || !float.IsFinite(height) || height < 0 || height > 1)
                    throw new InvalidOperationException($"Invalid terrain at {cell}");
                string feature = field.FeatureAtCell(cell);
                if (feature.Length > 0) features++;
                if (wet && (feature == "woods" || feature == "jungle"))
                    throw new InvalidOperationException($"Forest in water at {cell}");
                if (field.ResourceAtCell(cell).Length > 0) resources++;
                float shade = field.ShadeAtPosition(new Vector2(x + 0.5f, y + 0.5f));
                if (!float.IsFinite(shade)) throw new InvalidOperationException("Invalid fine shading");
            }
            if (dry < 200000 || water < 200000 || features == 0 || resources == 0)
                throw new InvalidOperationException("Huge world lost land, water, features or resources");
            var starts = new HashSet<Vector2I>(field.StartPositions);
            if (starts.Count != 6) throw new InvalidOperationException("Six distinct starts were not generated");
            foreach (var cell in starts)
                if (!new Rect2I(0, 0, 1024, 1024).HasPoint(cell) || TerrainTileSets.IsWaterKind(field.TerrainAtCell(cell)))
                    throw new InvalidOperationException("Invalid player start");
            var report = new Godot.Collections.Dictionary
            {
                ["generation_ms"] = generationMs, ["sampled_peak_working_bytes"] = _peakWorking,
                ["sampled_peak_managed_bytes"] = _peakManaged, ["process_peak_working_bytes"] = process.PeakWorkingSet64,
                ["dry_cells"] = dry, ["water_cells"] = water, ["feature_cells"] = features,
                ["surface_resource_cells"] = resources, ["starts"] = starts.Count,
                ["diagnostics"] = field.Diagnostics.ToDictionary(),
                ["scope"] = "CPU generation only; no scene publication, rendering, navigation or FPS qualification"
            };
            var timings = new Godot.Collections.Dictionary();
            foreach (var pair in _stages) timings[pair.Key] = pair.Value;
            report["observed_stage_ms"] = timings;
            DirAccess.MakeDirRecursiveAbsolute("res://tests/output/terrain_async");
            using var file = Godot.FileAccess.Open("res://tests/output/terrain_async/huge_generation.json", Godot.FileAccess.ModeFlags.Write);
            file.StoreString(Json.Stringify(report, "  "));
            GD.Print($"[terrain-huge] OK: {generationMs}ms; process peak={process.PeakWorkingSet64 / 1048576}MiB; dry={dry}, water={water}");
            return 1;
        }
        catch (Exception error)
        {
            GD.PushError($"[terrain-huge] {error}");
            return -1;
        }
        finally { _job.Dispose(); _job = null; }
    }
}
