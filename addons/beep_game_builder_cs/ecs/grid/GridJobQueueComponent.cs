using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Lightweight cell-job queue for builder, RTS, farming, tactics, and settlement games.
    ///
    /// Use this for jobs such as clear land, build road, harvest tile, repair object,
    /// deliver resource, or inspect a cell. Workers claim jobs by id and complete,
    /// release, or cancel them without the queue depending on TileMap.
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
        [Export] public bool RemoveCompletedJobs { get; set; } = true;
        [Export] public bool RemoveCancelledJobs { get; set; } = true;
        /// <summary>
        /// Whether jobs saved as Claimed load back as Queued. On by default:
        /// worker ids include the instance id, which no reload reproduces, and
        /// workers do not persist their current job - so a loaded claim always
        /// belongs to a ghost, and the job would sit claimed forever.
        /// </summary>
        [Export] public bool RequeueClaimedJobsOnLoad { get; set; } = true;
        [Export(PropertyHint.Range, "0.01,600,0.01")] public float DefaultWorkSeconds { get; set; } = 1.5f;

        private readonly Dictionary<string, GridJob> _jobs = new();
        private int _nextJobNumber = 1;

        public float EffectiveDefaultWorkSeconds => Mathf.Max(0.01f, float.IsFinite(DefaultWorkSeconds) ? DefaultWorkSeconds : 1.5f);

        public string AddJob(Vector2I cell, string kind = "work", float workSeconds = -1f, int priority = 0)
        {
            kind = NormalizeKind(kind);
            if (UniqueCellKind && FindOpenJobAt(cell, kind) is { } existing)
                return existing.Id;

            string id = $"{kind}_{_nextJobNumber++}";
            while (_jobs.ContainsKey(id))
                id = $"{kind}_{_nextJobNumber++}";

            float effectiveWorkSeconds = workSeconds > 0f && float.IsFinite(workSeconds)
                ? workSeconds
                : EffectiveDefaultWorkSeconds;
            _jobs[id] = new GridJob(id, kind, cell, priority, effectiveWorkSeconds);
            EmitSignal(SignalName.JobAdded, id, kind, cell.X, cell.Y);
            EmitQueueChanged();
            return id;
        }

        public bool CancelJob(string id, string reason = "cancelled")
        {
            if (!_jobs.TryGetValue(id, out GridJob? job) || job.State is GridJobState.Completed or GridJobState.Cancelled)
                return false;

            job.State = GridJobState.Cancelled;
            job.ClaimedBy = "";
            EmitSignal(SignalName.JobCancelled, id, reason);
            if (RemoveCancelledJobs)
                _jobs.Remove(id);
            EmitQueueChanged();
            return true;
        }

        public string ClaimNextJob(string workerId, Vector2I workerCell)
        {
            workerId = NormalizeWorker(workerId);
            GridJob? best = null;
            int bestDistance = int.MaxValue;

            foreach (GridJob job in _jobs.Values)
            {
                if (job.State != GridJobState.Queued)
                    continue;

                int distance = Mathf.Abs(job.Cell.X - workerCell.X) + Mathf.Abs(job.Cell.Y - workerCell.Y);
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

            best.State = GridJobState.Claimed;
            best.ClaimedBy = workerId;
            EmitSignal(SignalName.JobClaimed, best.Id, workerId);
            EmitQueueChanged();
            return best.Id;
        }

        public bool ClaimJob(string id, string workerId)
        {
            workerId = NormalizeWorker(workerId);
            if (!_jobs.TryGetValue(id, out GridJob? job) || job.State != GridJobState.Queued)
                return false;

            job.State = GridJobState.Claimed;
            job.ClaimedBy = workerId;
            EmitSignal(SignalName.JobClaimed, job.Id, workerId);
            EmitQueueChanged();
            return true;
        }

        public bool ReleaseJob(string id, string workerId = "")
        {
            if (!_jobs.TryGetValue(id, out GridJob? job) || job.State != GridJobState.Claimed)
                return false;

            if (!string.IsNullOrEmpty(workerId) && job.ClaimedBy != workerId)
                return false;

            string releasedBy = job.ClaimedBy;
            job.State = GridJobState.Queued;
            job.ClaimedBy = "";
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
            job.State = GridJobState.Completed;
            job.ClaimedBy = completedBy;
            EmitSignal(SignalName.JobCompleted, id, completedBy);
            if (RemoveCompletedJobs)
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

        public float GetJobWorkSeconds(string id)
            => _jobs.TryGetValue(id, out GridJob? job) ? job.WorkSeconds : 0f;

        public GridJobState GetJobState(string id)
            => _jobs.TryGetValue(id, out GridJob? job) ? job.State : GridJobState.Cancelled;

        public int QueuedCount => Count(GridJobState.Queued);
        public int ClaimedCount => Count(GridJobState.Claimed);
        public int CompletedCount => Count(GridJobState.Completed);

        public void ClearJobs()
        {
            _jobs.Clear();
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

                string kind = NormalizeKind(DictString(dict, "kind", "work"));
                var cell = DictVector2I(dict, "cell", new Vector2I(int.MinValue, int.MinValue));
                if (cell.X == int.MinValue || cell.Y == int.MinValue)
                    continue;

                var job = new GridJob(
                    id,
                    kind,
                    cell,
                    DictInt(dict, "priority", 0),
                    ClampWorkSeconds(DictFloat(dict, "work_seconds", EffectiveDefaultWorkSeconds)))
                {
                    State = ParseState(DictString(dict, "state", nameof(GridJobState.Queued))),
                    ClaimedBy = DictString(dict, "claimed_by", "")
                };

                if (RequeueClaimedJobsOnLoad && job.State == GridJobState.Claimed)
                {
                    job.State = GridJobState.Queued;
                    job.ClaimedBy = "";
                }

                _jobs[id] = job;
                TrackNextJobNumber(id);
            }

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
            => EmitSignal(SignalName.QueueChanged, QueuedCount, ClaimedCount, CompletedCount);

        private static string NormalizeKind(string kind)
            => string.IsNullOrWhiteSpace(kind) ? "work" : kind.Trim().ToLowerInvariant().Replace(' ', '_');

        private static string NormalizeWorker(string workerId)
            => string.IsNullOrWhiteSpace(workerId) ? Guid.NewGuid().ToString("N") : workerId.Trim();

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

        private static float ClampWorkSeconds(float value)
            => Mathf.Max(0.01f, float.IsFinite(value) ? value : 1.5f);

        private sealed class GridJob
        {
            public GridJob(string id, string kind, Vector2I cell, int priority, float workSeconds)
            {
                Id = id;
                Kind = kind;
                Cell = cell;
                Priority = priority;
                WorkSeconds = workSeconds;
            }

            public string Id { get; }
            public string Kind { get; }
            public Vector2I Cell { get; }
            public int Priority { get; }
            public float WorkSeconds { get; }
            public GridJobState State { get; set; } = GridJobState.Queued;
            public string ClaimedBy { get; set; } = "";

            public Godot.Collections.Dictionary ToDictionary()
            {
                return new Godot.Collections.Dictionary
                {
                    ["id"] = Id,
                    ["kind"] = Kind,
                    ["cell"] = Cell,
                    ["priority"] = Priority,
                    ["work_seconds"] = WorkSeconds,
                    ["state"] = State.ToString(),
                    ["claimed_by"] = ClaimedBy
                };
            }
        }
    }
}
