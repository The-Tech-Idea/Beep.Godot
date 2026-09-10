using Beep.ECS;
using Godot;

// ENH-12 (one-notification path): every job-queue mutation ran the O(jobs) chunk-pin pass twice -
// once in the mutator and again in EmitQueueChanged. EmitQueueChanged no longer refreshes; the
// mutators own it. This probe asserts AddJob / ClaimJob / CompleteJob each refresh the pins exactly
// once and emit QueueChanged once, and that claim + complete still behave. The mutation the guard
// catches: restoring RefreshChunkPins in EmitQueueChanged makes every mutation refresh twice.
[GlobalClass]
public partial class GridJobQueueSmoke : Node
{
    private int _queueChanged;

    public bool Run()
    {
        var queue = new GridJobQueueComponent { Name = "Jobs" };
        AddChild(queue);
        queue.QueueChanged += OnQueueChanged;

        long pinsBefore = queue.ChunkPinRefreshCount;
        int qcBefore = _queueChanged;
        string id = queue.AddJob(new Vector2I(3, 4), "clear");
        if (queue.ChunkPinRefreshCount - pinsBefore != 1)
            return Fail($"AddJob should refresh chunk pins once, refreshed {queue.ChunkPinRefreshCount - pinsBefore}");
        if (_queueChanged - qcBefore != 1)
            return Fail($"AddJob should emit QueueChanged once, emitted {_queueChanged - qcBefore}");

        pinsBefore = queue.ChunkPinRefreshCount;
        string claimed = queue.ClaimNextJob("worker1", new Vector2I(0, 0));
        if (claimed != id)
            return Fail($"ClaimNextJob should claim the only queued job, got '{claimed}'");
        if (queue.ChunkPinRefreshCount - pinsBefore != 1)
            return Fail($"ClaimJob should refresh chunk pins once, refreshed {queue.ChunkPinRefreshCount - pinsBefore}");
        if (queue.GetJobState(id) != GridJobQueueComponent.GridJobState.Claimed)
            return Fail("The claimed job should be in the Claimed state");

        pinsBefore = queue.ChunkPinRefreshCount;
        if (!queue.CompleteJob(id, "worker1"))
            return Fail("CompleteJob should succeed for the claimed job");
        if (queue.ChunkPinRefreshCount - pinsBefore != 1)
            return Fail($"CompleteJob should refresh chunk pins once, refreshed {queue.ChunkPinRefreshCount - pinsBefore}");

        queue.QueueChanged -= OnQueueChanged;
        queue.Free();
        GD.Print("[grid-jobqueue] AddJob/Claim/Complete each refresh chunk pins exactly once; claim + complete behave unchanged");
        return true;
    }

    private void OnQueueChanged(int queued, int claimed, int completed) => _queueChanged++;
    private static bool Fail(string message) { GD.PushError(message); return false; }
}
