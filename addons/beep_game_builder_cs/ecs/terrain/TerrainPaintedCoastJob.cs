using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Beep.ECS;

/// <summary>Detached coast pixels; never reads nodes or creates GPU resources.</summary>
internal sealed class TerrainPaintedCoastJob : IDisposable
{
    internal sealed record Result(TerrainCoastField.Pixels Coast, TerrainCoastField.Pixels Lake,
        TerrainCoastField.Pixels SmoothCoast, TerrainCoastField.Pixels SmoothLake);
    private readonly CancellationTokenSource _cancel = new();
    internal Task<Result> Completion { get; }
    internal int Detail { get; }
    internal float Range { get; }

    internal TerrainPaintedCoastJob(TerrainVisualSnapshot snapshot, Vector2I size, int detail, float range)
    {
        Detail = detail;
        Range = range;
        var token = _cancel.Token;
        Completion = Task.Run(() => Compute(snapshot, size, detail, range, token), token);
    }

    private static Result Compute(TerrainVisualSnapshot snapshot, Vector2I size, int detail, float range, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        int count = checked(size.X * size.Y);
        var wet = new bool[count];
        var patches = new GridTerrainWaterPatch?[count];
        var lakes = new GridTerrainWaterPatch?[count];
        bool hasLakeBanks = false;
        for (int i = 0; i < count; i++)
        {
            if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
            var sample = snapshot[i];
            wet[i] = TerrainTileSets.IsWaterKind(sample.Kind);
            patches[i] = sample.Water;
            lakes[i] = sample.Lake;
            hasLakeBanks |= sample.Shore.LakeWidth > 0;
        }
        var coast = TerrainCoastField.BuildLivePixels(wet, patches, size, Mathf.Clamp(detail, 1, 16), Mathf.Max(5f, range), token);
        var lake = hasLakeBanks
            ? TerrainCoastField.BuildLakePixels(lakes, size, detail, Mathf.Max(5f, range), token)
            : new TerrainCoastField.Pixels(Vector2I.One, new byte[] { 0, 0, 0, 255 }, Image.Format.Rgba8);
        token.ThrowIfCancellationRequested();
        var smoothCoast = TerrainCoastField.RenderCache.PrepareContours(coast, size, token);
        var smoothLake = TerrainCoastField.RenderCache.PrepareContours(lake, size, token);
        return new(coast, lake, smoothCoast, smoothLake);
    }

    public void Dispose()
    {
        _cancel.Cancel();
        // Scene exit must observe worker failure and retire cancellation without touching nodes.
        _ = Completion.ContinueWith(task =>
        {
            _ = task.Exception;
            _cancel.Dispose();
        }, TaskScheduler.Default);
    }
}
