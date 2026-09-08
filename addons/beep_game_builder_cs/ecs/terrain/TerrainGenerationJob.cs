using System;
using System.Threading;
using System.Threading.Tasks;

namespace Beep.ECS;

/// <summary>One isolated CPU build. Never reads nodes or publishes live world state.</summary>
internal sealed class TerrainGenerationJob : IDisposable
{
    internal sealed record Progress(string Stage, int CompletedStages);
    private readonly CancellationTokenSource _cancellation = new();
    private Progress _progress = new("Queued", 0);
    public Progress CurrentProgress => Volatile.Read(ref _progress);
    public Task<GeneratedTerrainField> Completion { get; }

    // Construct on the main thread. Do not pass authored resources to the worker.
    public TerrainGenerationJob(TerrainGenerationSettings settings)
    {
        TerrainResourceRules resources = TerrainResourceRules.Capture(settings);
        TerrainGenerationSettings detached = settings with { ResourceCatalog = null };
        CancellationToken token = _cancellation.Token;
        Completion = Task.Run(() => TerrainFieldBuilder.BuildPrepared(detached, resources, token,
            (stage, completed) => Volatile.Write(ref _progress, new Progress(stage, completed))), token);
    }

    public void Cancel() => _cancellation.Cancel();

    public void Dispose()
    {
        if (!Completion.IsCompleted)
            throw new InvalidOperationException("Cancel and await generation before disposing its job.");
        _cancellation.Dispose();
    }
}
