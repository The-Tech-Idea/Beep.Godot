using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridJobQueueComponent
{
    private readonly Dictionary<string, Node> _dispatchOwners = new();

    internal bool CanBindDispatcher(string jobId, string workerId, Node dispatcher)
    {
        if (!dispatcher.IsInsideTree() || !CanWorkerClaim(workerId)
            || GetJobState(jobId) != GridJobState.Claimed || GetJobClaimedBy(jobId) != workerId) return false;
        return !_dispatchOwners.TryGetValue(jobId, out var owner) || !GodotObject.IsInstanceValid(owner)
            || !owner.IsInsideTree() || owner == dispatcher;
    }

    internal bool BindDispatcher(string jobId, string workerId, Node dispatcher)
    {
        if (!CanBindDispatcher(jobId, workerId, dispatcher)) return false;
        _dispatchOwners[jobId] = dispatcher;
        return true;
    }

    internal void UnbindDispatcher(string jobId, Node dispatcher)
    {
        if (_dispatchOwners.TryGetValue(jobId, out var owner) && owner == dispatcher) _dispatchOwners.Remove(jobId);
    }
}
