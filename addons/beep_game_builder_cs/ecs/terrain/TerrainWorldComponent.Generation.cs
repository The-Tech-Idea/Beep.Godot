using Godot;
using System;
using System.Threading.Tasks;

namespace Beep.ECS;

public partial class TerrainWorldComponent
{
    [Signal] public delegate void GenerationProgressEventHandler(string stage, float fraction);
    [Signal] public delegate void GenerationFinishedEventHandler(bool success, string message);
    public bool IsGenerating => _generation is not null;
    public bool CanCancelGeneration => IsGenerating && !_scenePublished;
    [Export(PropertyHint.Range, "1,4096,1")]
    public int PublicationCellsPerFrame { get; set; } = 512;

    private TerrainGenerationJob? _generation;
    private TerrainGeneratorComponent? _generationTarget;
    private TerrainGenerationSettings _generationSettings;
    private Godot.Collections.Dictionary? _generationConfiguration;
    private Godot.Collections.Dictionary? _builtRecipe;
    private string _generationRecipe = "";
    private string _generationBaseConfiguration = "";
    private NodePath _generationCellPath = new("");
    private string _lastGenerationStage = "";
    private bool _generationCancelled;
    private bool _publicationPending;
    private GridCellDataComponent.GeneratedPublication? _cellPublication;
    private bool _scenePublished;
    private TerrainCollisionComponent? _publicationCollision;
    private ulong? _collisionSettledFrame;
    private TerrainPaintedRendererComponent? _publicationPainter;
    private TerrainIsometricAutotileRendererComponent? _publicationAutotile;
    private ulong _autotileRevision;

    /// <summary>Starts a replacement build without modifying the current field or live cells.</summary>
    public bool BeginNewWorld()
    {
        if (Engine.IsEditorHint() || !IsInsideTree() || IsGenerating) return false;
        Resolve();
        if (_generator is null) return false;

        // Configure the request synchronously, then restore the live generator's
        // exported inputs before yielding. The worker receives detached settings.
        var previous = _generator.CaptureConfiguration();
        _generationBaseConfiguration = Json.Stringify(previous);
        _generationCellPath = CellDataPath;
        try
        {
            if (!ConfigureGenerator(out _)) return false;
            _generationTarget = _generator;
            _generationSettings = _generator.CaptureGenerationSettings();
            _generationConfiguration = _generator.CaptureConfiguration();
            _generationRecipe = Json.Stringify(CaptureRecipe());
            _generation = new TerrainGenerationJob(_generationSettings);
        }
        catch (Exception error)
        {
            EmitSignal(SignalName.GenerationFinished, false, error.Message);
            return false;
        }
        finally { _generator.ApplyConfiguration(previous); }

        _generationCancelled = false;
        _publicationPending = false;
        _scenePublished = false;
        _publicationCollision = null;
        _collisionSettledFrame = null;
        _lastGenerationStage = "Queued";
        SetProcess(true);
        EmitSignal(SignalName.GenerationProgress, "Queued", 0f);
        return true;
    }

    public void CancelGeneration()
    {
        if (!CanCancelGeneration) return;
        _generationCancelled = true;
        _generation!.Cancel();
        EmitSignal(SignalName.GenerationProgress, "Cancelling", 0f);
    }

    public override void _Process(double delta)
    {
        var job = _generation;
        if (job is null) return;
        if (_scenePublished)
        {
            if (_publicationPainter is not null) ContinuePaintedPublication(job);
            else if (_publicationAutotile is not null) ContinueAutotilePublication(job);
            else FinishCollisionPublication(job);
            return;
        }
        if (!job.Completion.IsCompleted)
        {
            var progress = job.CurrentProgress;
            if (!_generationCancelled && progress.Stage != _lastGenerationStage)
            {
                _lastGenerationStage = progress.Stage;
                EmitSignal(SignalName.GenerationProgress, progress.Stage, progress.CompletedStages / 20f * 0.9f);
            }
            return;
        }

        bool success = false;
        bool staging = false;
        string message = "Cancelled";
        if (!_generationCancelled && !job.Completion.IsCanceled && !job.Completion.IsFaulted && !_publicationPending)
        {
            // Give the authored loading screen a frame before scene publication.
            _publicationPending = true;
            EmitSignal(SignalName.GenerationProgress, "Publishing world", 0.9f);
            return;
        }
        try
        {
            if (job.Completion.IsFaulted)
                message = job.Completion.Exception!.GetBaseException().Message;
            else if (!_generationCancelled && !job.Completion.IsCanceled)
            {
                Resolve();
                if (!GodotObject.IsInstanceValid(_generationTarget) || _generator != _generationTarget
                    || _generationRecipe != Json.Stringify(CaptureRecipe())
                    || CellDataPath != _generationCellPath
                    || _generationBaseConfiguration != Json.Stringify(_generator!.CaptureConfiguration()))
                    message = "Settings changed; generated result discarded";
                else if (!BindCellSource())
                    message = "Live cell source is unavailable";
                else
                {
                    if (_generationConfiguration!["ClearExistingCells"].AsBool())
                    {
                        _cellPublication ??= _generator!.PreparePublication(_generationSettings, job.Completion.GetAwaiter().GetResult());
                        if (_cellPublication is not null)
                        {
                            if (!_generator!.PublicationMatches(_cellPublication))
                                throw new InvalidOperationException("Live cell source changed during publication.");
                            if (!_cellPublication.Step(PublicationCellsPerFrame))
                            {
                                staging = true;
                                EmitSignal(SignalName.GenerationProgress, "Preparing gameplay cells",
                                    0.9f + 0.06f * _cellPublication.Loaded / ((float)_generationSettings.Size.X * _generationSettings.Size.Y));
                                return;
                            }
                        }
                    }
                    // Live-cell commit is the cancellation boundary. From here the
                    // loading state remains active until native collision is usable.
                    _scenePublished = true;
                    _generator!.ApplyConfiguration(_generationConfiguration!);
                    _generator.PublishField(_generationSettings, job.Completion.GetAwaiter().GetResult(), _cellPublication);
                    BuiltSize = _generationSettings.Size;
                    _builtRecipe = CaptureRecipe();
                    if (Projection == TerrainProjection.Painted && _painted is not null)
                    {
                        _painted.BoundsOrigin = _generator.BoundsOrigin;
                        _painted.BoundsSize = BuiltSize;
                        if (_painted.BeginSnapshotPreparation())
                        {
                            _publicationPainter = _painted;
                            _cellPublication?.Dispose();
                            _cellPublication = null;
                            staging = true;
                            EmitSignal(SignalName.GenerationProgress, "Preparing painted terrain", 0.96f);
                            return;
                        }
                    }
                    if (Projection == TerrainProjection.IsometricAutotile && _isometricAutotile is not null)
                    {
                        _publicationAutotile = _isometricAutotile;
                        _publicationAutotile.BoundsOrigin = _generator.BoundsOrigin;
                        _publicationAutotile.BoundsSize = BuiltSize;
                        _autotileRevision = _publicationAutotile.PublicationRevision;
                        _publicationAutotile.RequestRebuild();
                        _cellPublication?.Dispose();
                        _cellPublication = null;
                        staging = true;
                        EmitSignal(SignalName.GenerationProgress, "Preparing isometric terrain", 0.96f);
                        return;
                    }
                    Draw(_generationSettings.Size, queueCollision: true);
                    _publicationCollision = CollisionPath.IsEmpty ? null : GetNodeOrNull<TerrainCollisionComponent>(CollisionPath);
                    if (!CollisionPath.IsEmpty && _publicationCollision is null)
                        throw new InvalidOperationException("Configured terrain collision is unavailable.");
                    if (_publicationCollision is not null)
                    {
                        _cellPublication?.Dispose();
                        _cellPublication = null;
                        staging = true;
                        EmitSignal(SignalName.GenerationProgress, "Preparing terrain collision", 0.98f);
                        return;
                    }
                    success = true;
                    message = "Complete";
                }
            }
        }
        catch (Exception error) { message = error.Message; }
        finally
        {
            if (!staging)
                CompletePublication(job, success ? null : message);
        }
    }

    private void ContinuePaintedPublication(TerrainGenerationJob job)
    {
        try
        {
            var painter = _publicationPainter!;
            if (!GodotObject.IsInstanceValid(painter) || !painter.IsInsideTree()
                || PaintedRendererPath.IsEmpty || GetNodeOrNull<TerrainPaintedRendererComponent>(PaintedRendererPath) != painter
                || Projection != TerrainProjection.Painted
                || !GodotObject.IsInstanceValid(_generationTarget)
                || GeneratorPath.IsEmpty || GetNodeOrNull<TerrainGeneratorComponent>(GeneratorPath) != _generationTarget
                || CellDataPath != _generationCellPath)
                throw new InvalidOperationException("Painted terrain sources changed during publication.");
            if (!painter.StepSnapshotPreparation(PublicationCellsPerFrame))
            {
                EmitSignal(SignalName.GenerationProgress, painter.PreparationStage,
                    0.96f + 0.02f * painter.PreparedSnapshotCells / ((float)BuiltSize.X * BuiltSize.Y));
                return;
            }
            _publicationPainter = null;
            Draw(BuiltSize, queueCollision: true);
            _publicationCollision = CollisionPath.IsEmpty ? null : GetNodeOrNull<TerrainCollisionComponent>(CollisionPath);
            if (!CollisionPath.IsEmpty && _publicationCollision is null)
                throw new InvalidOperationException("Configured terrain collision is unavailable.");
            if (_publicationCollision is not null)
            {
                EmitSignal(SignalName.GenerationProgress, "Preparing terrain collision", 0.98f);
                return;
            }
            CompletePublication(job, null);
        }
        catch (Exception error) { CompletePublication(job, error.Message); }
    }

    private void ContinueAutotilePublication(TerrainGenerationJob job)
    {
        try
        {
            var renderer = _publicationAutotile!;
            if (!GodotObject.IsInstanceValid(renderer) || !renderer.IsInsideTree()
                || IsometricAutotileRendererPath.IsEmpty || GetNodeOrNull<TerrainIsometricAutotileRendererComponent>(IsometricAutotileRendererPath) != renderer
                || Projection != TerrainProjection.IsometricAutotile
                || !GodotObject.IsInstanceValid(_generationTarget)
                || GeneratorPath.IsEmpty || GetNodeOrNull<TerrainGeneratorComponent>(GeneratorPath) != _generationTarget
                || CellDataPath != _generationCellPath
                || renderer.CellDataPath.IsEmpty
                || renderer.GetNodeOrNull<GridCellDataComponent>(renderer.CellDataPath) != _generationTarget!.GetNodeOrNull<GridCellDataComponent>(_generationTarget.CellDataPath)
                || renderer.BoundsSize != BuiltSize || renderer.BoundsOrigin != _generationTarget!.BoundsOrigin)
                throw new InvalidOperationException("Isometric terrain sources changed during publication.");
            if (renderer.IsRebuilding) return;
            if (renderer.PublicationRevision == _autotileRevision || !renderer.GetPaintDiagnostics()["valid"].AsBool())
                throw new InvalidOperationException("Isometric terrain publication was cancelled or has incomplete tile coverage.");
            _publicationAutotile = null;
            Draw(BuiltSize, queueCollision: true, preparedAutotile: true);
            _publicationCollision = CollisionPath.IsEmpty ? null : GetNodeOrNull<TerrainCollisionComponent>(CollisionPath);
            if (!CollisionPath.IsEmpty && _publicationCollision is null)
                throw new InvalidOperationException("Configured terrain collision is unavailable.");
            if (_publicationCollision is not null)
                EmitSignal(SignalName.GenerationProgress, "Preparing terrain collision", 0.98f);
            else CompletePublication(job, null);
        }
        catch (Exception error) { CompletePublication(job, error.Message); }
    }

    private void FinishCollisionPublication(TerrainGenerationJob job)
    {
        string? error = null;
        var collision = _publicationCollision;
        if (!GodotObject.IsInstanceValid(collision) || !collision!.IsInsideTree()
            || CollisionPath.IsEmpty || GetNodeOrNull<TerrainCollisionComponent>(CollisionPath) != collision)
            error = "Terrain collision was removed during publication.";
        else if (collision.IsUpdating)
        {
            _collisionSettledFrame = null;
            return;
        }
        else if (!GodotObject.IsInstanceValid(_generationTarget)
            || GeneratorPath.IsEmpty || GetNodeOrNull<TerrainGeneratorComponent>(GeneratorPath) != _generationTarget
            || CellDataPath != _generationCellPath
            || collision.BoundsOrigin != _generationTarget!.BoundsOrigin || collision.BoundsSize != BuiltSize
            || GetNodeOrNull<GridProjectionComponent>(GridPath) is not { } grid
            || _generationTarget.GetNodeOrNull<GridCellDataComponent>(_generationTarget.CellDataPath) is not { } cells
            || !collision.UsesSources(grid, cells))
            error = "Terrain collision sources changed during publication.";
        else
        {
            // Native physics must consume the last published shapes before gameplay starts.
            _collisionSettledFrame ??= Engine.GetPhysicsFrames();
            if (Engine.GetPhysicsFrames() <= _collisionSettledFrame.Value) return;
            if (!collision.IsReady) error = "Terrain collision could not build every required chunk.";
        }

        CompletePublication(job, error);
    }

    private void CompletePublication(TerrainGenerationJob job, string? error)
    {
        if (_generation != job) return;
        if (GodotObject.IsInstanceValid(_publicationAutotile)) _publicationAutotile!.CancelRebuild();
        _publicationAutotile = null;
        if (GodotObject.IsInstanceValid(_publicationPainter)) _publicationPainter!.CancelSnapshotPreparation();
        _publicationPainter = null;
        _cellPublication?.Dispose();
        _cellPublication = null;
        _generation = null;
        _generationTarget = null;
        _generationConfiguration = null;
        _scenePublished = false;
        _publicationCollision = null;
        _collisionSettledFrame = null;
        job.Dispose();
        SetProcess(false);
        if (error is null) EmitSignal(SignalName.WorldBuilt, BuiltSize);
        EmitSignal(SignalName.GenerationFinished, error is null, error ?? "Complete");
    }

    private void RetireGeneration()
    {
        if (GodotObject.IsInstanceValid(_publicationAutotile)) _publicationAutotile!.CancelRebuild();
        _publicationAutotile = null;
        if (GodotObject.IsInstanceValid(_publicationPainter)) _publicationPainter!.CancelSnapshotPreparation();
        _publicationPainter = null;
        _cellPublication?.Dispose();
        _cellPublication = null;
        var job = _generation;
        _generation = null;
        _generationTarget = null;
        _generationConfiguration = null;
        _scenePublished = false;
        _publicationCollision = null;
        _collisionSettledFrame = null;
        if (job is null) return;
        job.Cancel();
        // Cleanup owns no nodes and must finish even after this scene is freed.
        _ = job.Completion.ContinueWith(task =>
        {
            _ = task.Exception;
            job.Dispose();
        }, TaskScheduler.Default);
    }
}
