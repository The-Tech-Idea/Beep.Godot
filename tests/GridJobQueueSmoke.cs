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

        // --- Count-cache correctness (ENH-12) ---
        // QueuedCount/ClaimedCount/CompletedCount are cached and recomputed on mutation; a missed
        // invalidation would return a stale count. Walk the transitions and check the values.
        var counts = new GridJobQueueComponent { Name = "Counts", RemoveCompletedJobs = false };
        AddChild(counts);
        if (counts.QueuedCount != 0 || counts.ClaimedCount != 0 || counts.CompletedCount != 0)
            return Fail("A fresh queue should report zero counts");
        string cj = counts.AddJob(new Vector2I(2, 2), "clear");
        if (counts.QueuedCount != 1 || counts.ClaimedCount != 0 || counts.CompletedCount != 0)
            return Fail($"After AddJob: expected 1/0/0, got {counts.QueuedCount}/{counts.ClaimedCount}/{counts.CompletedCount}");
        counts.ClaimNextJob("w", new Vector2I(0, 0));
        if (counts.QueuedCount != 0 || counts.ClaimedCount != 1)
            return Fail($"After claim: expected queued 0/claimed 1, got {counts.QueuedCount}/{counts.ClaimedCount}");
        counts.CompleteJob(cj, "w");
        if (counts.ClaimedCount != 0 || counts.CompletedCount != 1)
            return Fail($"After complete: expected claimed 0/completed 1, got {counts.ClaimedCount}/{counts.CompletedCount}");
        counts.Free();

        // --- Dispatch-fairness baseline (ENH-12, record first) ---
        // The claim rule is: highest priority, then NEAREST to the claiming worker, then lowest id.
        // It is NOT age-based. This pins it so a future claim index cannot silently change it - in
        // particular a priority-queue keyed by (-priority, seq), which the plan sketched, would
        // return the older, farther job here and quietly regress "workers take the nearest job".
        var nearFar = new GridJobQueueComponent { Name = "NearFar" };
        AddChild(nearFar);
        string older = nearFar.AddJob(new Vector2I(20, 20), "work"); // added first: older, lower seq
        string nearer = nearFar.AddJob(new Vector2I(0, 0), "work");  // added second: newer, but nearest
        string picked = nearFar.ClaimNextJob("A", new Vector2I(0, 0));
        if (picked != nearer)
            return Fail($"Claim must pick the nearest job ({nearer}), not the oldest ({older}); got '{picked}'");
        nearFar.Free();

        // Priority still wins over distance.
        var priorityFar = new GridJobQueueComponent { Name = "PriorityFar" };
        AddChild(priorityFar);
        priorityFar.AddJob(new Vector2I(0, 0), "work", -1f, 0);              // near, low priority
        string highFar = priorityFar.AddJob(new Vector2I(30, 0), "work", -1f, 5); // far, high priority
        string pick2 = priorityFar.ClaimNextJob("A", new Vector2I(0, 0));
        if (pick2 != highFar)
            return Fail($"Claim must prefer higher priority even when farther ({highFar}); got '{pick2}'");
        priorityFar.Free();

        GD.Print("[grid-jobqueue] one refresh per mutation; cached counts stay correct; claim fairness is priority > nearest > id (not age)");
        return true;
    }

    private void OnQueueChanged(int queued, int claimed, int completed) => _queueChanged++;
    private static bool Fail(string message) { GD.PushError(message); return false; }
}
