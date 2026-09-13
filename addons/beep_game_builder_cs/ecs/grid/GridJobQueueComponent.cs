using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Lightweight cell-job queue for builder, RTS, farming, tactics, and settlement games.
    ///
    /// Use this for jobs such as clear land, build road, harvest tile,
    /// deliver resource, or inspect a cell. Workers claim jobs by id and complete,
    /// release, or cancel them without the queue depending on TileMap.
    ///
    /// WORK IS MEASURED IN TURNS, like every other grid duration (BuildTurns,
    /// DurationTurns, CycleTurns): a turn is the grid's one unit, and
    /// GridWorkClockComponent decides what a turn is in the game's own time -
    /// one end-turn on the turn axis, one day's worth of beats on the
    /// real-time axis. The queue never ticks and never divides by a frame
    /// delta; it only records what an executor reports, so the unit is a
    /// float for sub-turn resolution (a worker with WorkSpeedMultiplier 2
    /// burns half a turn of work per turn... twice). The field used to be
    /// called seconds while the work clock fed it turns.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridJobQueueComponent : Node
    {
        public enum GridJobState
        {
            Queued,
            Claimed,
            Completed,
            Cancelled
        }

        [Signal] public delegate void JobAddedEventHandler(string id, string kind, int x, int y);
        [Signal] public delegate void JobClaimedEventHandler(string id, string workerId);
        [Signal] public delegate void JobReleasedEventHandler(string id, string workerId);
        [Signal] public delegate void JobCompletedEventHandler(string id, string workerId);
        [Signal] public delegate void JobCancelledEventHandler(string id, string reason);
        [Signal] public delegate void QueueChangedEventHandler(int queued, int claimed, int completed);

        [Export] public bool UniqueCellKind { get; set; } = true;
        [Export] public string OwnerId { get; set; } = "";
        [Export] public NodePath ActorRegistryPath { get; set; } = new("");
        [Export] public bool RemoveCompletedJobs { get; set; } = true;
        [Export] public bool RemoveCancelledJobs { get; set; } = true;
        /// <summary>
        /// Whether saved claims load as queued. Actor-backed workers have stable
        /// IDs and reclaim their saved job when restored after the queue. Keeping
        /// this enabled also releases claims for workers absent from a save.
        /// </summary>
        [Export] public bool RequeueClaimedJobsOnLoad { get; set; } = true;
        /// <summary>Work a job carries when its author gave none, in turns.</summary>
        [Export(PropertyHint.Range, "0.01,600,0.01")] public float DefaultWorkTurns { get; set; } = 1.5f;

        private readonly Dictionary<string, GridJob> _jobs = new();
        private int _nextJobNumber = 1;
        private int _queuedCache, _claimedCache, _completedCache;
        private bool _countsDirty = true;

        // Queued jobs indexed for the claim: priority tier (key = -priority, so highest priority
        // iterates first) -> ApproachCell chunk -> job ids. A claim visits tiers high-to-low and,
        // within a tier, chunks outward from the worker, so it inspects the nearby jobs instead of
        // every job. Chunking via GridCellDataComponent.ChunkOf (the one chunk rule, DUP-09).
        private readonly SortedDictionary<int, Dictionary<Vector2I, List<string>>> _queuedIndex = new();

        // Cells per chunk along one axis, derived from the one chunk rule (GridCellDataComponent.ChunkOf)
        // rather than restating the shift, so the ring-search distance bound tracks it automatically.
        private static readonly int ChunkSpan = DeriveChunkSpan();

        private static int DeriveChunkSpan()
        {
            Vector2I origin = GridCellDataComponent.ChunkOf(Vector2I.Zero);
            for (int x = 1; x < (1 << 20); x++)
                if (GridCellDataComponent.ChunkOf(new Vector2I(x, 0)) != origin)
                    return x;
            return 1; // degenerate; a span of 1 only makes the ring search visit more, never miss
        }

        public float EffectiveDefaultWorkTurns => Mathf.Max(0.01f, float.IsFinite(DefaultWorkTurns) ? DefaultWorkTurns : 1.5f);

        public string AddJob(Vector2I cell, string kind = "work", float workTurns = -1f, int priority = 0)
        {
            kind = GridIds.NormalizeOr(kind, "work");
            if (UniqueCellKind && FindOpenJobAt(cell, kind) is { } existing)
                return existing.Id;

            string id = $"{kind}_{_nextJobNumber++}";
            while (_jobs.ContainsKey(id))
                id = $"{kind}_{_nextJobNumber++}";

            float effectiveWorkTurns = workTurns > 0f && float.IsFinite(workTurns)
                ? workTurns
                : EffectiveDefaultWorkTurns;
            _jobs[id] = new GridJob(id, kind, cell, priority, effectiveWorkTurns);
            IndexAddQueued(_jobs[id]);
            RefreshChunkPins();
            EmitSignal(SignalName.JobAdded, id, kind, cell.X, cell.Y);
            EmitQueueChanged();
            return id;
        }

        public bool CancelJob(string id, string reason = "cancelled")
        {
            if (!_jobs.TryGetValue(id, out GridJob? job) || job.State is GridJobState.Completed or GridJobState.Cancelled)
                return false;

            ReleaseReservation(job);
            if (job.State == GridJobState.Queued) IndexRemoveQueued(job);
            job.State = GridJobState.Cancelled;
            job.ClaimedBy = "";
            RefreshChunkPins();
            EmitSignal(SignalName.JobCancelled, id, reason);
            if (RemoveCancelledJobs && _jobs.GetValueOrDefault(id) == job)
                _jobs.Remove(id);
            EmitQueueChanged();
            return true;
        }

        /// <summary>
        /// Claims the best queued job - highest priority, then nearest. A
        /// non-empty <paramref name="allowedKinds"/> restricts the claim to
        /// those kinds: a boat claims "fish" jobs, a truck everything else,
        /// and neither churns on work it can never reach.
        /// </summary>
        public string ClaimNextJob(string workerId, Vector2I workerCell, Godot.Collections.Array<string>? allowedKinds = null)
            => ClaimNextJobExcluding(workerId, workerCell, allowedKinds, null);

        internal string ClaimNextJobExcluding(string workerId, Vector2I workerCell,
            Godot.Collections.Array<string>? allowedKinds, IReadOnlySet<string>? excludedJobs)
        {
            if (!EnsureReservationScope() || !CanWorkerClaim(workerId) || _workerClaims.ContainsKey(workerId)) return "";

            // The queued-job spatial index finds the best claimable job by visiting chunks outward
            // from the worker within each priority tier (highest first), instead of every job. It
            // falls back to the full scan only when the index yields nothing while queued jobs exist,
            // so an index gap can never leave a queued job undispatched. Both paths share
            // IsJobClaimable and BetterClaim, so they pick the identical job - the index only changes
            // the visiting ORDER, never the winner (ENH-12).
            GridJob? best = FindBestClaimableSpatial(workerCell, workerId, allowedKinds, excludedJobs);
            if (best == null && QueuedCount > 0)
                best = FindBestClaimableLinear(workerCell, workerId, allowedKinds, excludedJobs);

            if (best == null)
                return "";

            return ClaimJob(best.Id, workerId) ? best.Id : "";
        }

        /// <summary>Whether this worker may claim the job right now: queued, not excluded, its stand
        /// cell free and not reserved elsewhere, a real cell, and an allowed kind. Shared by the
        /// spatial and linear claim paths so both apply the identical filter.</summary>
        private bool IsJobClaimable(GridJob job, string workerId,
            Godot.Collections.Array<string>? allowedKinds, IReadOnlySet<string>? excludedJobs)
        {
            if (excludedJobs?.Contains(job.Id) == true) return false;
            if (job.State != GridJobState.Queued || _workCells.ContainsKey(job.ApproachCell)
                || !SharedClaimAvailable(workerId, job.ApproachCell)
                || job.ApproachCell.X == int.MinValue || job.ApproachCell.Y == int.MinValue)
                return false;
            return allowedKinds is not { Count: > 0 } || KindAllowed(job.Kind, allowedKinds);
        }

        /// <summary>The claim fairness rule, shared by both paths: higher priority wins; then nearer
        /// to the worker (Manhattan to the stand cell); then the lower id.</summary>
        private static bool BetterClaim(GridJob job, long distance, GridJob best, long bestDistance)
            => job.Priority > best.Priority
                || (job.Priority == best.Priority && distance < bestDistance)
                || (job.Priority == best.Priority && distance == bestDistance && string.CompareOrdinal(job.Id, best.Id) < 0);

        private static long ManhattanTo(Vector2I cell, Vector2I worker)
            => Math.Abs((long)cell.X - worker.X) + Math.Abs((long)cell.Y - worker.Y);

        private GridJob? FindBestClaimableLinear(Vector2I workerCell, string workerId,
            Godot.Collections.Array<string>? allowedKinds, IReadOnlySet<string>? excludedJobs)
        {
            GridJob? best = null;
            long bestDistance = long.MaxValue;
            foreach (GridJob job in _jobs.Values)
            {
                if (!IsJobClaimable(job, workerId, allowedKinds, excludedJobs)) continue;
                long distance = ManhattanTo(job.ApproachCell, workerCell);
                if (best == null || BetterClaim(job, distance, best, bestDistance))
                {
                    best = job;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private GridJob? FindBestClaimableSpatial(Vector2I workerCell, string workerId,
            Godot.Collections.Array<string>? allowedKinds, IReadOnlySet<string>? excludedJobs)
        {
            // Tiers iterate highest priority first. A lower-priority job is only considered when the
            // higher tiers yield nothing CLAIMABLE - exactly what the linear scan does.
            foreach (Dictionary<Vector2I, List<string>> tier in _queuedIndex.Values)
            {
                GridJob? best = SearchTierNearest(tier, workerCell, workerId, allowedKinds, excludedJobs);
                if (best != null)
                    return best;
            }
            return null;
        }

        private GridJob? SearchTierNearest(Dictionary<Vector2I, List<string>> byChunk, Vector2I workerCell, string workerId,
            Godot.Collections.Array<string>? allowedKinds, IReadOnlySet<string>? excludedJobs)
        {
            if (byChunk.Count == 0) return null;
            Vector2I center = GridCellDataComponent.ChunkOf(workerCell);
            GridJob? best = null;
            long bestDistance = long.MaxValue;
            int chunksTotal = byChunk.Count;
            int chunksSeen = 0;
            for (int r = 0; chunksSeen < chunksTotal; r++)
            {
                chunksSeen += ScanChunkRing(byChunk, center, r, workerCell, workerId, allowedKinds, excludedJobs, ref best, ref bestDistance);
                // Any cell in a chunk at ring r+1 is at least r*ChunkSize away; once the best found is
                // strictly nearer than that, no farther ring can match or beat it (the '<' keeps one
                // extra ring in flight so an equal-distance, lower-id job is never skipped).
                if (best != null && bestDistance < (long)r * ChunkSpan)
                    break;
            }
            return best;
        }

        /// <summary>Scans the chunks on the Chebyshev ring at radius <paramref name="r"/> around
        /// <paramref name="center"/>, returning how many of them existed in the tier (so the caller
        /// can stop once every chunk has been visited).</summary>
        private int ScanChunkRing(Dictionary<Vector2I, List<string>> byChunk, Vector2I center, int r, Vector2I workerCell,
            string workerId, Godot.Collections.Array<string>? allowedKinds, IReadOnlySet<string>? excludedJobs,
            ref GridJob? best, ref long bestDistance)
        {
            if (r == 0)
                return ScanChunk(byChunk, center, workerCell, workerId, allowedKinds, excludedJobs, ref best, ref bestDistance);

            int hits = 0;
            for (int dx = -r; dx <= r; dx++)
            {
                hits += ScanChunk(byChunk, new Vector2I(center.X + dx, center.Y - r), workerCell, workerId, allowedKinds, excludedJobs, ref best, ref bestDistance);
                hits += ScanChunk(byChunk, new Vector2I(center.X + dx, center.Y + r), workerCell, workerId, allowedKinds, excludedJobs, ref best, ref bestDistance);
            }
            for (int dy = -r + 1; dy <= r - 1; dy++)
            {
                hits += ScanChunk(byChunk, new Vector2I(center.X - r, center.Y + dy), workerCell, workerId, allowedKinds, excludedJobs, ref best, ref bestDistance);
                hits += ScanChunk(byChunk, new Vector2I(center.X + r, center.Y + dy), workerCell, workerId, allowedKinds, excludedJobs, ref best, ref bestDistance);
            }
            return hits;
        }

        private int ScanChunk(Dictionary<Vector2I, List<string>> byChunk, Vector2I chunk, Vector2I workerCell,
            string workerId, Godot.Collections.Array<string>? allowedKinds, IReadOnlySet<string>? excludedJobs,
            ref GridJob? best, ref long bestDistance)
        {
            if (!byChunk.TryGetValue(chunk, out List<string>? ids)) return 0;
            foreach (string id in ids)
            {
                if (!_jobs.TryGetValue(id, out GridJob? job)) continue; // a stale id: live Queued exits own index removal, so this is defence in depth
                if (!IsJobClaimable(job, workerId, allowedKinds, excludedJobs)) continue;
                long distance = ManhattanTo(job.ApproachCell, workerCell);
                if (best == null || BetterClaim(job, distance, best, bestDistance))
                {
                    best = job;
                    bestDistance = distance;
                }
            }
            return 1;
        }

        // --- queued-job spatial index maintenance ---

        private void IndexAddQueued(GridJob job)
        {
            if (job.ApproachCell.X == int.MinValue || job.ApproachCell.Y == int.MinValue) return;
            int key = -job.Priority;
            if (!_queuedIndex.TryGetValue(key, out Dictionary<Vector2I, List<string>>? byChunk))
                _queuedIndex[key] = byChunk = new Dictionary<Vector2I, List<string>>();
            Vector2I chunk = GridCellDataComponent.ChunkOf(job.ApproachCell);
            if (!byChunk.TryGetValue(chunk, out List<string>? ids))
                byChunk[chunk] = ids = new List<string>();
            if (!ids.Contains(job.Id))
                ids.Add(job.Id);
        }

        private void IndexRemoveQueued(GridJob job)
        {
            if (job.ApproachCell.X == int.MinValue || job.ApproachCell.Y == int.MinValue) return;
            int key = -job.Priority;
            if (!_queuedIndex.TryGetValue(key, out Dictionary<Vector2I, List<string>>? byChunk)) return;
            Vector2I chunk = GridCellDataComponent.ChunkOf(job.ApproachCell);
            if (byChunk.TryGetValue(chunk, out List<string>? ids))
            {
                ids.Remove(job.Id);
                if (ids.Count == 0) byChunk.Remove(chunk);
            }
            if (byChunk.Count == 0) _queuedIndex.Remove(key);
        }

        private void RebuildQueuedIndex()
        {
            _queuedIndex.Clear();
            foreach (GridJob job in _jobs.Values)
                if (job.State == GridJobState.Queued)
                    IndexAddQueued(job);
        }

        /// <summary>
        /// Total ids across every priority tier and chunk bucket in the queued spatial index.
        /// A leak shows up here as a count that does not fall when a queued job leaves the queue.
        /// </summary>
        internal int QueuedIndexEntryCount
        {
            get
            {
                int total = 0;
                foreach (Dictionary<Vector2I, List<string>> byChunk in _queuedIndex.Values)
                    foreach (List<string> ids in byChunk.Values)
                        total += ids.Count;
                return total;
            }
        }

        public bool ClaimJob(string id, string workerId)
        {
            if (!CanClaimJob(id, workerId)) return false;
            GridJob job = _jobs[id];
            ReserveClaim(job, workerId);
            RefreshChunkPins(); // the claim reserves a work cell and flips state, so pins change
            EmitSignal(SignalName.JobClaimed, job.Id, workerId);
            EmitQueueChanged();
            return _jobs.GetValueOrDefault(id) == job && job.State == GridJobState.Claimed && job.ClaimedBy == workerId;
        }

        public bool ReleaseJob(string id, string workerId = "")
        {
            if (!_jobs.TryGetValue(id, out GridJob? job) || job.State != GridJobState.Claimed)
                return false;

            if (!string.IsNullOrEmpty(workerId) && job.ClaimedBy != workerId)
                return false;

            string releasedBy = job.ClaimedBy;
            ReleaseReservation(job);
            job.State = GridJobState.Queued;
            job.ClaimedBy = "";
            IndexAddQueued(job);
            RefreshChunkPins();
            EmitSignal(SignalName.JobReleased, id, releasedBy);
            EmitQueueChanged();
            return true;
        }

        public bool CompleteJob(string id, string workerId = "")
        {
            if (!_jobs.TryGetValue(id, out GridJob? job) || job.State is GridJobState.Completed or GridJobState.Cancelled)
                return false;

            if (!string.IsNullOrEmpty(workerId) && !string.IsNullOrEmpty(job.ClaimedBy) && job.ClaimedBy != workerId)
                return false;

            string completedBy = string.IsNullOrEmpty(workerId) ? job.ClaimedBy : workerId;
            // A job that never left Queued never reached ReserveClaim, so its queued spatial-index
            // entry is still there. Every other Queued exit removes it (CancelJob, ReserveClaim);
            // this one must too, or the id is orphaned in a bucket whose GridJob is gone.
            if (job.State == GridJobState.Queued) IndexRemoveQueued(job);
            ReleaseReservation(job);
            job.State = GridJobState.Completed;
            job.ClaimedBy = completedBy;
            RefreshChunkPins();
            EmitSignal(SignalName.JobCompleted, id, completedBy);
            if (RemoveCompletedJobs && _jobs.GetValueOrDefault(id) == job)
                _jobs.Remove(id);
            EmitQueueChanged();
            return true;
        }

        public bool HasJob(string id) => _jobs.ContainsKey(id);

        public Vector2I GetJobCell(string id)
            => _jobs.TryGetValue(id, out GridJob? job) ? job.Cell : new Vector2I(int.MinValue, int.MinValue);

        public string GetJobKind(string id)
            => _jobs.TryGetValue(id, out GridJob? job) ? job.Kind : "";

        public string GetJobClaimedBy(string id)
            => _jobs.TryGetValue(id, out GridJob? job) ? job.ClaimedBy : "";

        public float GetJobWorkTurns(string id)
            => _jobs.TryGetValue(id, out GridJob? job) ? job.WorkTurns : 0f;

        public float GetJobRemainingTurns(string id)
            => _jobs.TryGetValue(id, out GridJob? job) ? job.RemainingTurns : 0f;

        public enum WorkAdvanceResult { Rejected, Progressed, Completed }

        /// <summary>Advances an executor that has already reached its reserved work cell.
        /// Progress stays in the queue, independent of the executor's scene lifetime.
        /// Arrival and worker activity remain the executor's responsibility.</summary>
        public WorkAdvanceResult AdvanceClaimedWork(string jobId, string workerId, Vector2I workCell,
            float elapsedTurns, float workSpeed)
            => AdvanceClaimedWorkCore(jobId, workerId, workCell, elapsedTurns, workSpeed, null);

        internal WorkAdvanceResult AdvanceClaimedWorkCore(string jobId, string workerId, Vector2I workCell,
            float elapsedTurns, float workSpeed, Node? executor)
        {
            if (!float.IsFinite(elapsedTurns) || elapsedTurns <= 0f
                || !float.IsFinite(workSpeed) || workSpeed <= 0f || !EnsureReservationScope() || !CanWorkerClaim(workerId)
                || !_jobs.TryGetValue(jobId, out var job) || job.State != GridJobState.Claimed
                || job.ClaimedBy != workerId || GetReservedWorkCell(jobId) != workCell || !ExecutorMatches(jobId, executor))
                return WorkAdvanceResult.Rejected;
            job.RemainingTurns = (float)Math.Max(0.0, job.RemainingTurns - (double)elapsedTurns * workSpeed);
            if (job.RemainingTurns > 0f) return WorkAdvanceResult.Progressed;
            return CompleteJob(jobId, workerId) ? WorkAdvanceResult.Completed : WorkAdvanceResult.Rejected;
        }

        /// <summary>Sets raw remaining work for custom executors and authoring tools.
        /// Normal worker execution uses AdvanceClaimedWork to validate claims and complete once.</summary>
        public void ReportProgress(string jobId, float remainingTurns)
        {
            if (_jobs.TryGetValue(jobId, out GridJob? job))
                job.RemainingTurns = Mathf.Clamp(float.IsFinite(remainingTurns) ? remainingTurns : job.WorkTurns, 0f, job.WorkTurns);
        }

        /// <summary>
        /// How far along a job is, 0 (just started, or unknown/never
        /// reported) to 1 (about to complete). Derived from WorkTurns and
        /// whatever ReportProgress last recorded, so it reflects reality
        /// for ANY executor that reports - not specifically GridWorkerComponent.
        /// </summary>
        public float GetJobProgress01(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out GridJob? job) || job.WorkTurns <= 0f)
                return 0f;
            return Mathf.Clamp(1f - job.RemainingTurns / job.WorkTurns, 0f, 1f);
        }

        public GridJobState GetJobState(string id)
            => _jobs.TryGetValue(id, out GridJob? job) ? job.State : GridJobState.Cancelled;

        /// <summary>
        /// The cell a worker should STAND ON to do this job - by default the
        /// job cell itself. A build site sets it to a cell just outside its
        /// footprint (the way Age of Empires villagers and Settlers builders
        /// stand beside the site, never inside it): the footprint's own
        /// cells are blocked by the placed GridObjectComponent, so a worker
        /// sent to the job cell with AllowBlockedGoal off fails with no_path
        /// and drops the job.
        /// </summary>
        public bool SetJobApproachCell(string id, Vector2I approachCell)
        {
            if (approachCell.X == int.MinValue || approachCell.Y == int.MinValue) return false;
            if (!_jobs.TryGetValue(id, out GridJob? job))
                return false;
            if (job.State == GridJobState.Claimed && job.ApproachCell != approachCell) return false;
            bool wasQueued = job.State == GridJobState.Queued;
            if (wasQueued) IndexRemoveQueued(job); // remove from the old chunk first
            job.ApproachCell = approachCell;
            if (wasQueued) IndexAddQueued(job);    // re-add at the new chunk
            RefreshChunkPins();
            return true;
        }

        /// <summary>The cell to stand on for a job - the job cell unless a
        /// site set one - or (int.MinValue, int.MinValue) for an unknown job.</summary>
        public Vector2I GetJobApproachCell(string id)
            => _jobs.TryGetValue(id, out GridJob? job) ? job.ApproachCell : new Vector2I(int.MinValue, int.MinValue);

        /// <summary>
        /// The id of the job a worker currently holds Claimed, or empty. A
        /// lookup in the queue's claim index, with no per-job marshalling.
        /// Worker IDs are ordinal, matching actor identity. For a roster snapshot,
        /// use <see cref="GetClaimedJobIdsByWorker"/>.
        /// </summary>
        public string FindClaimedJobId(string workerId)
        {
            if (string.IsNullOrWhiteSpace(workerId))
                return "";

            return _workerClaims.GetValueOrDefault(workerId, "");
        }

        /// <summary>
        /// Every currently-Claimed job's id, keyed by the claiming worker id -
        /// a detached copy of the queue's claim index, with no per-job marshalling.
        /// Meant for a caller that needs to resolve many workers' claims at
        /// once (a HUD refresh over the whole roster) instead of calling
        /// <see cref="FindClaimedJobId"/> - or re-marshalling <see cref="GetJobs"/> -
        /// once per worker.
        /// </summary>
        public Dictionary<string, string> GetClaimedJobIdsByWorker()
        {
            return new Dictionary<string, string>(_workerClaims, StringComparer.Ordinal);
        }

        public int QueuedCount { get { RecomputeCountsIfDirty(); return _queuedCache; } }
        public int ClaimedCount { get { RecomputeCountsIfDirty(); return _claimedCache; } }
        public int CompletedCount { get { RecomputeCountsIfDirty(); return _completedCache; } }

        public void ClearJobs()
        {
            ReleaseSharedReservations();
            _executionOwners.Clear();
            _dispatchOwners.Clear();
            _jobs.Clear();
            _workerClaims.Clear();
            _workCells.Clear();
            _queuedIndex.Clear();
            RefreshChunkPins(); // no jobs left to want any cell
            EmitQueueChanged();
        }

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetJobs()
        {
            var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (GridJob job in _jobs.Values)
                result.Add(job.ToDictionary());
            return result;
        }

        /// <summary>A typed snapshot of one job for same-assembly HUDs: the fields a job board needs
        /// to sort and render, without a Godot Dictionary per job. GetJobs stays for GDScript/saves.</summary>
        internal readonly record struct JobSnapshot(string Id, string Kind, Vector2I Cell, GridJobState State, int Priority, string ClaimedBy);

        /// <summary>The stored jobs as typed snapshots - the <see cref="GetJobs"/> view without the
        /// per-cell Dictionary marshal the job board used to pay on every QueueChanged (ENH-12).</summary>
        internal IEnumerable<JobSnapshot> EnumerateJobs()
        {
            foreach (GridJob job in _jobs.Values)
                yield return new JobSnapshot(job.Id, job.Kind, job.Cell, job.State, job.Priority, job.ClaimedBy);
        }

        public void LoadJobs(Godot.Collections.Array jobs, bool clearExisting = true)
        {
            if (clearExisting)
                _jobs.Clear();

            foreach (Variant value in jobs)
            {
                if (!GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary dict))
                    continue;

                string id = GridVariantReader.String(dict, "id", "");
                if (string.IsNullOrEmpty(id))
                    continue;

                string kind = GridIds.NormalizeOr(GridVariantReader.String(dict, "kind", "work"), "work");
                var cell = GridVariantReader.Vector2I(dict, "cell", new Vector2I(int.MinValue, int.MinValue));
                if (cell.X == int.MinValue || cell.Y == int.MinValue)
                    continue;

                var job = new GridJob(
                    id,
                    kind,
                    cell,
                    GridVariantReader.Int(dict, "priority", 0),
                    ClampWorkTurns(GridVariantReader.Float(dict, "work_turns", EffectiveDefaultWorkTurns)))
                {
                    State = ParseState(GridVariantReader.String(dict, "state", nameof(GridJobState.Queued))),
                    ClaimedBy = GridVariantReader.String(dict, "claimed_by", ""),
                    ApproachCell = GridVariantReader.Vector2I(dict, "approach_cell", cell),
                    ReservedCell = GridVariantReader.Vector2I(dict, "reserved_cell", GridVariantReader.Vector2I(dict, "approach_cell", cell))
                };

                if (RequeueClaimedJobsOnLoad && job.State == GridJobState.Claimed)
                {
                    job.State = GridJobState.Queued;
                    job.ClaimedBy = "";
                }

                _jobs[id] = job;
                job.RemainingTurns = Mathf.Clamp(GridVariantReader.Float(dict, "remaining_turns", job.WorkTurns), 0f, job.WorkTurns);
                TrackNextJobNumber(id);
            }

            RebuildReservations();
            RefreshChunkPins(); // the loaded jobs want their cells
            EmitQueueChanged();
        }

        private GridJob? FindOpenJobAt(Vector2I cell, string kind)
        {
            foreach (GridJob job in _jobs.Values)
                if (job.Cell == cell && job.Kind == kind && job.State is GridJobState.Queued or GridJobState.Claimed)
                    return job;
            return null;
        }

        // The three QueueChanged counts are cached and recomputed once after a mutation, not scanned
        // per read: EmitQueueChanged plus every QueuedCount/ClaimedCount/CompletedCount reader (the
        // job board summary, the minimap) shared three O(jobs) scans per mutation. A mutation marks
        // the cache dirty (EmitQueueChanged, and RebuildReservations for its conflict requeue); the
        // next read does one pass. Over-invalidation is safe - it forces a recompute, never a wrong
        // count - so only a missed invalidation could go stale, and the probe guards that (ENH-12).
        private void RecomputeCountsIfDirty()
        {
            if (!_countsDirty) return;
            int queued = 0, claimed = 0, completed = 0;
            foreach (GridJob job in _jobs.Values)
            {
                switch (job.State)
                {
                    case GridJobState.Queued: queued++; break;
                    case GridJobState.Claimed: claimed++; break;
                    case GridJobState.Completed: completed++; break;
                }
            }
            _queuedCache = queued;
            _claimedCache = claimed;
            _completedCache = completed;
            _countsDirty = false;
        }

        // QueueChanged no longer refreshes the chunk pins: every mutator that reaches here has
        // already refreshed them (AddJob/Cancel/Release/Complete directly; Claim/Clear/Load below),
        // so this used to run the O(jobs) pin pass twice per mutation (ENH-12, DUP-09).
        private void EmitQueueChanged()
        {
            _countsDirty = true; // a mutation just happened; the counts recompute once on next read
            EmitSignal(SignalName.QueueChanged, QueuedCount, ClaimedCount, CompletedCount);
        }


        private static bool KindAllowed(string kind, Godot.Collections.Array<string> allowedKinds)
        {
            foreach (string allowed in allowedKinds)
            {
                if (GridIds.NormalizeOr(allowed, "work") == kind)
                    return true;
            }
            return false;
        }

        public bool CanWorkerClaim(string workerId)
            => !string.IsNullOrWhiteSpace(workerId) && (OwnerId.Length == 0 || (!ActorRegistryPath.IsEmpty
                && GetNodeOrNull<ActorRegistryComponent>(ActorRegistryPath)?.GetActorOwner(workerId) == OwnerId));

        private void TrackNextJobNumber(string id)
        {
            int underscore = id.LastIndexOf('_');
            if (underscore < 0 || underscore >= id.Length - 1)
                return;

            if (int.TryParse(id[(underscore + 1)..], out int number))
                _nextJobNumber = Mathf.Max(_nextJobNumber, number + 1);
        }

        private static GridJobState ParseState(string value)
            => Enum.TryParse(value, ignoreCase: true, out GridJobState state) ? state : GridJobState.Queued;





        private static float ClampWorkTurns(float value)
            => Mathf.Max(0.01f, float.IsFinite(value) ? value : 1.5f);

        private sealed class GridJob
        {
            public GridJob(string id, string kind, Vector2I cell, int priority, float workTurns)
            {
                Id = id;
                Kind = kind;
                Cell = cell;
                Priority = priority;
                WorkTurns = workTurns;
                RemainingTurns = workTurns;
                ApproachCell = cell;
                ReservedCell = cell;
            }

            public string Id { get; }
            public string Kind { get; }
            public Vector2I Cell { get; }
            public int Priority { get; }
            public float WorkTurns { get; }
            public GridJobState State { get; set; } = GridJobState.Queued;
            public string ClaimedBy { get; set; } = "";

            /// <summary>Where a worker stands to do the job; the job cell
            /// unless a site set a cell outside its footprint.</summary>
            public Vector2I ApproachCell { get; set; }
            public Vector2I ReservedCell { get; set; }

            /// <summary>Turns of work left, as last reported by whoever is
            /// executing the job. Starts equal to WorkTurns (0% progress).</summary>
            public float RemainingTurns { get; set; }

            public Godot.Collections.Dictionary ToDictionary()
            {
                return new Godot.Collections.Dictionary
                {
                    ["id"] = Id,
                    ["kind"] = Kind,
                    ["cell"] = Cell,
                    ["approach_cell"] = ApproachCell,
                    ["reserved_cell"] = ReservedCell,
                    ["priority"] = Priority,
                    ["work_turns"] = WorkTurns,
                    ["remaining_turns"] = RemainingTurns,
                    ["state"] = State.ToString(),
                    ["claimed_by"] = ClaimedBy
                };
            }
        }
    }
}
