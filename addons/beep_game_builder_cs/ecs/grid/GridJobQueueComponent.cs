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
            GridJob? best = null;
            long bestDistance = long.MaxValue;

            foreach (GridJob job in _jobs.Values)
            {
                if (excludedJobs?.Contains(job.Id) == true) continue;
                if (job.State != GridJobState.Queued || _workCells.ContainsKey(job.ApproachCell) || !SharedClaimAvailable(workerId, job.ApproachCell)
                    || job.ApproachCell.X == int.MinValue || job.ApproachCell.Y == int.MinValue)
                    continue;

                if (allowedKinds is { Count: > 0 } && !KindAllowed(job.Kind, allowedKinds))
                    continue;

                long distance = Math.Abs((long)job.ApproachCell.X - workerCell.X) + Math.Abs((long)job.ApproachCell.Y - workerCell.Y);
                if (best == null
                    || job.Priority > best.Priority
                    || (job.Priority == best.Priority && distance < bestDistance)
                    || (job.Priority == best.Priority && distance == bestDistance && string.CompareOrdinal(job.Id, best.Id) < 0))
                {
                    best = job;
                    bestDistance = distance;
                }
            }

            if (best == null)
                return "";

            return ClaimJob(best.Id, workerId) ? best.Id : "";
        }

        public bool ClaimJob(string id, string workerId)
        {
            if (!CanClaimJob(id, workerId)) return false;
            GridJob job = _jobs[id];
            ReserveClaim(job, workerId);
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
            job.ApproachCell = approachCell;
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

        public int QueuedCount => Count(GridJobState.Queued);
        public int ClaimedCount => Count(GridJobState.Claimed);
        public int CompletedCount => Count(GridJobState.Completed);

        public void ClearJobs()
        {
            ReleaseSharedReservations();
            _executionOwners.Clear();
            _dispatchOwners.Clear();
            _jobs.Clear();
            _workerClaims.Clear();
            _workCells.Clear();
            EmitQueueChanged();
        }

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetJobs()
        {
            var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (GridJob job in _jobs.Values)
                result.Add(job.ToDictionary());
            return result;
        }

        public void LoadJobs(Godot.Collections.Array jobs, bool clearExisting = true)
        {
            if (clearExisting)
                _jobs.Clear();

            foreach (Variant value in jobs)
            {
                if (!GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary dict))
                    continue;

                string id = DictString(dict, "id", "");
                if (string.IsNullOrEmpty(id))
                    continue;

                string kind = GridIds.NormalizeOr(DictString(dict, "kind", "work"), "work");
                var cell = DictVector2I(dict, "cell", new Vector2I(int.MinValue, int.MinValue));
                if (cell.X == int.MinValue || cell.Y == int.MinValue)
                    continue;

                var job = new GridJob(
                    id,
                    kind,
                    cell,
                    DictInt(dict, "priority", 0),
                    ClampWorkTurns(DictFloat(dict, "work_turns", EffectiveDefaultWorkTurns)))
                {
                    State = ParseState(DictString(dict, "state", nameof(GridJobState.Queued))),
                    ClaimedBy = DictString(dict, "claimed_by", ""),
                    ApproachCell = DictVector2I(dict, "approach_cell", cell),
                    ReservedCell = DictVector2I(dict, "reserved_cell", DictVector2I(dict, "approach_cell", cell))
                };

                if (RequeueClaimedJobsOnLoad && job.State == GridJobState.Claimed)
                {
                    job.State = GridJobState.Queued;
                    job.ClaimedBy = "";
                }

                _jobs[id] = job;
                job.RemainingTurns = Mathf.Clamp(DictFloat(dict, "remaining_turns", job.WorkTurns), 0f, job.WorkTurns);
                TrackNextJobNumber(id);
            }

            RebuildReservations();
            EmitQueueChanged();
        }

        private GridJob? FindOpenJobAt(Vector2I cell, string kind)
        {
            foreach (GridJob job in _jobs.Values)
                if (job.Cell == cell && job.Kind == kind && job.State is GridJobState.Queued or GridJobState.Claimed)
                    return job;
            return null;
        }

        private int Count(GridJobState state)
        {
            int count = 0;
            foreach (GridJob job in _jobs.Values)
                if (job.State == state)
                    count++;
            return count;
        }

        private void EmitQueueChanged()
        {
            RefreshChunkPins();
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

        private static string DictString(Godot.Collections.Dictionary dict, string key, string fallback)
            => dict.ContainsKey(key) ? dict[key].AsString() : fallback;

        private static int DictInt(Godot.Collections.Dictionary dict, string key, int fallback)
            => GridVariantReader.Int(dict, key, fallback);

        private static float DictFloat(Godot.Collections.Dictionary dict, string key, float fallback)
            => GridVariantReader.Float(dict, key, fallback);

        private static Vector2I DictVector2I(Godot.Collections.Dictionary dict, string key, Vector2I fallback)
            => GridVariantReader.Vector2I(dict, key, fallback);

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
