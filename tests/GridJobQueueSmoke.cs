using Beep.ECS;
using Godot;
using System;

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

        // --- Spatial claim index: differential test vs brute force across random geometries (ENH-12) ---
        // The spatial path must choose the IDENTICAL job the linear scan would (priority > nearest >
        // id). Compare the actual claim to a brute-force computed BEFORE claiming, over many random
        // multi-chunk layouts, so a ring-search or termination bug is caught regardless of geometry.
        var rng = new Random(0xC0FFEE);
        for (int scenario = 0; scenario < 300; scenario++)
        {
            var q = new GridJobQueueComponent { Name = $"Diff{scenario}" };
            AddChild(q);
            int jobCount = 1 + rng.Next(12);
            for (int j = 0; j < jobCount; j++)
                q.AddJob(new Vector2I(rng.Next(-80, 80), rng.Next(-80, 80)), "work", -1f, rng.Next(0, 3));
            var worker = new Vector2I(rng.Next(-80, 80), rng.Next(-80, 80));
            string expected = BruteForceBest(q, worker); // computed while all jobs are still queued
            string chosen = q.ClaimNextJob("w", worker);
            if (chosen != expected)
                return Fail($"Scenario {scenario}: spatial claimed '{chosen}', brute force expected '{expected}' (worker {worker.X},{worker.Y})");
            q.Free();
        }

        // Maintenance: the index tracks claim, the next-best after it, and release (re-queue).
        var maint = new GridJobQueueComponent { Name = "Maint" };
        AddChild(maint);
        string mFar = maint.AddJob(new Vector2I(50, 0), "work");
        string mNear = maint.AddJob(new Vector2I(1, 0), "work");
        if (maint.ClaimNextJob("w1", new Vector2I(0, 0)) != mNear)
            return Fail("Maint: the nearer job should be claimed first");
        if (maint.ClaimNextJob("w2", new Vector2I(0, 0)) != mFar)
            return Fail("Maint: the far job should be claimed once the near one is taken");
        maint.ReleaseJob(mNear, "w1");
        if (maint.ClaimNextJob("w3", new Vector2I(0, 0)) != mNear)
            return Fail("Maint: a released job must be claimable again (re-indexed)");
        maint.Free();

        // An approach-cell move re-indexes a queued job to its new position.
        var move = new GridJobQueueComponent { Name = "Move" };
        AddChild(move);
        move.AddJob(new Vector2I(2, 0), "work");
        string moved = move.AddJob(new Vector2I(60, 0), "work");
        move.SetJobApproachCell(moved, new Vector2I(-1, 0)); // now the nearest to (0,0)
        if (move.ClaimNextJob("w", new Vector2I(0, 0)) != moved)
            return Fail("Move: an approach-cell move must re-index the job's position");
        move.Free();

        // Cancel removes a job from the index so it is never claimed.
        var cancel = new GridJobQueueComponent { Name = "Cancel" };
        AddChild(cancel);
        string cNear = cancel.AddJob(new Vector2I(1, 0), "work");
        string cFar = cancel.AddJob(new Vector2I(40, 0), "work");
        cancel.CancelJob(cNear);
        if (cancel.ClaimNextJob("w", new Vector2I(0, 0)) != cFar)
            return Fail("Cancel: a cancelled near job must not be claimed");
        cancel.Free();

        // Load rebuilds the index from the loaded jobs.
        var load = new GridJobQueueComponent { Name = "Load" };
        AddChild(load);
        load.LoadJobs(new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary { ["id"] = "work_1", ["kind"] = "work", ["cell"] = new Vector2I(5, 0), ["state"] = "Queued", ["priority"] = 0 },
            new Godot.Collections.Dictionary { ["id"] = "work_2", ["kind"] = "work", ["cell"] = new Vector2I(1, 0), ["state"] = "Queued", ["priority"] = 0 },
        });
        if (load.ClaimNextJob("w", new Vector2I(0, 0)) != "work_2")
            return Fail("Load: the index must be rebuilt from loaded jobs (nearest claimed)");
        load.Free();

        GD.Print("[grid-jobqueue] one refresh per mutation; cached counts stay correct; spatial claim matches brute force over 300 geometries; fairness is priority > nearest > id");
        return true;
    }

    // The reference fairness rule: among queued jobs, highest priority, then nearest to the worker
    // (Manhattan to the stand cell), then lowest id. The spatial claim must match this exactly.
    private static string BruteForceBest(GridJobQueueComponent q, Vector2I worker)
    {
        string best = "";
        int bestPriority = int.MinValue;
        long bestDistance = long.MaxValue;
        foreach (Godot.Collections.Dictionary job in q.GetJobs())
        {
            if ((string)job["state"] != "Queued") continue;
            var cell = (Vector2I)job["approach_cell"];
            int priority = (int)job["priority"];
            string id = (string)job["id"];
            long distance = Math.Abs((long)cell.X - worker.X) + Math.Abs((long)cell.Y - worker.Y);
            if (best.Length == 0
                || priority > bestPriority
                || (priority == bestPriority && distance < bestDistance)
                || (priority == bestPriority && distance == bestDistance && string.CompareOrdinal(id, best) < 0))
            {
                best = id;
                bestPriority = priority;
                bestDistance = distance;
            }
        }
        return best;
    }

    private void OnQueueChanged(int queued, int claimed, int completed) => _queueChanged++;
    private static bool Fail(string message) { GD.PushError(message); return false; }
}
