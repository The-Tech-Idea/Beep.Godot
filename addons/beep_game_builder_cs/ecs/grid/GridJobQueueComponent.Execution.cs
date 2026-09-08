using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridJobQueueComponent
{
    private readonly Dictionary<string, Node> _executionOwners = new();

    private bool ExecutorMatches(string jobId, Node? executor)
    {
        if (!_executionOwners.TryGetValue(jobId, out var owner)) return executor is null;
        if (!GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree())
        {
            _executionOwners.Remove(jobId);
            return executor is null;
        }
        return owner == executor;
    }

    internal bool BindExecutor(string jobId, string workerId, Node executor)
    {
        if (!CanBindExecutor(jobId, workerId, executor)) return false;
        _executionOwners[jobId] = executor;
        return true;
    }

    internal bool CanBindExecutor(string jobId, string workerId, Node executor)
    {
        if (!EnsureReservationScope() || !executor.IsInsideTree() || !CanWorkerClaim(workerId)
            || !_jobs.TryGetValue(jobId, out var job) || job.State != GridJobState.Claimed || job.ClaimedBy != workerId)
            return false;
        if (!ExecutorMatches(jobId, null) && !ExecutorMatches(jobId, executor)) return false;
        return true;
    }

    internal void UnbindExecutor(string jobId, Node executor)
    {
        if (_executionOwners.TryGetValue(jobId, out var owner) && owner == executor) _executionOwners.Remove(jobId);
    }
}
