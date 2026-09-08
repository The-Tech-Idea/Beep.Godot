using Godot;

namespace Beep.ECS;

public partial class GridWorkerComponent
{
    /// <summary>Optional world-owned executor. Travel stays on this worker; arrived work is delegated.</summary>
    [Export] public NodePath ExecutionPath { get; set; } = new("");
    private GridJobExecutionComponent? _execution;
    private bool _worldOwned;
    public bool HasWorldExecution => _worldOwned && GodotObject.IsInstanceValid(_execution)
        && CurrentJobId.Length > 0 && _execution!.GetWorkerJob(WorkerId) == CurrentJobId;

    public bool CanSuspendActor() => CurrentJobId.Length == 0 || (State == WorkerState.Working && HasWorldExecution);

    internal bool UsesExecutor(GridJobExecutionComponent executor, string job)
        => AcceptsExecutor(executor)
            && CurrentJobId == job && State == WorkerState.Working;

    internal bool AcceptsExecutor(GridJobExecutionComponent executor)
        => IsActive && _execution == executor && executor.MatchesSources(_queue, _grid);

    private void BindExecution()
    {
        if (Engine.IsEditorHint() || ExecutionPath.IsEmpty) return;
        _execution = GetNodeOrNull<GridJobExecutionComponent>(ExecutionPath);
        if (_execution is null) return;
        _execution.ExecutionFinished += OnExecutionFinished;
        _execution.ExecutionStateRestored += OnExecutionStateRestored;
    }

    private void UnbindExecution()
    {
        if (GodotObject.IsInstanceValid(_execution))
        {
            _execution!.ExecutionFinished -= OnExecutionFinished;
            _execution.ExecutionStateRestored -= OnExecutionStateRestored;
        }
        _execution = null;
    }

    private void SynchronizeExecution()
    {
        string job = GodotObject.IsInstanceValid(_execution) ? _execution!.GetWorkerJob(WorkerId) : "";
        _worldOwned = job.Length > 0;
        CurrentJobId = job;
        WorkRemainingTurns = _queue?.GetJobRemainingTurns(job) ?? 0;
        _workCell = _queue?.GetReservedWorkCell(job) ?? new Vector2I(int.MinValue, int.MinValue);
        SetState(_worldOwned ? WorkerState.Working : WorkerState.Idle);
    }

    private void OnExecutionStateRestored()
    {
        if (_worldOwned || (GodotObject.IsInstanceValid(_execution) && _execution!.GetWorkerJob(WorkerId).Length > 0))
            SynchronizeExecution();
    }

    private void OnExecutionFinished(string workerId, string jobId, bool completed)
    {
        if (!IsInsideTree() || IsQueuedForDeletion() || workerId != WorkerId || jobId != CurrentJobId) return;
        if (completed) NotifyJobCompleted();
        else CancelCurrentJob("world_execution_failed");
    }
}
