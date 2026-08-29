using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Worker/vehicle agent that claims jobs from GridJobQueueComponent, moves to
    /// the target cell with GridPathFollowerComponent, waits for the job duration,
    /// and marks the job complete.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridWorkerComponent : GameplayComponent
    {
        public enum WorkerState
        {
            Idle,
            MovingToJob,
            Working
        }

        [Signal] public delegate void WorkerClaimedJobEventHandler(string workerId, string jobId, string kind, int x, int y);
        [Signal] public delegate void WorkerStartedJobEventHandler(string workerId, string jobId);
        [Signal] public delegate void WorkerCompletedJobEventHandler(string workerId, string jobId);
        [Signal] public delegate void WorkerFailedJobEventHandler(string workerId, string jobId, string reason);
        [Signal] public delegate void WorkerStateChangedEventHandler(string workerId, int state);

        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath PathFollowerPath { get; set; } = new("");
        [Export] public string WorkerId { get; set; } = "";
        [Export] public bool AutoClaimJobs { get; set; } = true;
        [Export(PropertyHint.Range, "0.05,5,0.05")] public float ClaimIntervalSeconds { get; set; } = 0.25f;
        [Export(PropertyHint.Range, "0.01,20,0.01")] public float WorkSpeedMultiplier { get; set; } = 1f;

        public WorkerState State { get; private set; } = WorkerState.Idle;
        public string CurrentJobId { get; private set; } = "";
        public float WorkRemainingSeconds { get; private set; }

        private GridJobQueueComponent? _queue;
        private GridProjectionComponent? _grid;
        private GridPathFollowerComponent? _follower;
        private Node2D? _body;
        private float _claimTimer;
        private bool _wasMoving;
        public float EffectiveClaimInterval => Mathf.Max(0.01f, float.IsFinite(ClaimIntervalSeconds) ? ClaimIntervalSeconds : 0.25f);
        public float EffectiveWorkSpeed => Mathf.Max(0.01f, float.IsFinite(WorkSpeedMultiplier) ? WorkSpeedMultiplier : 1f);

        public override void _Ready()
        {
            base._Ready();
            WorkerId = string.IsNullOrWhiteSpace(WorkerId) ? $"{GetParent()?.Name ?? Name}_{GetInstanceId()}" : WorkerId.Trim();
            ResolveReferences();
            SetProcess(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (WorkSpeedMultiplier <= 0f)
                return new[] { "WorkSpeedMultiplier must be greater than zero." };
            if (ClaimIntervalSeconds <= 0f)
                return new[] { "ClaimIntervalSeconds must be greater than zero." };
            return Array.Empty<string>();
        }

        public override void _Process(double delta)
        {
            if (!IsActive || Engine.IsEditorHint())
                return;

            Tick(delta);
        }

        public void Tick(double delta)
        {
            ResolveReferences();
            if (_queue == null || _grid == null || _follower == null || _body == null)
                return;

            float effectiveDelta = delta > 0.0 && double.IsFinite(delta) ? (float)delta : 0f;

            if (State == WorkerState.MovingToJob)
            {
                if (_wasMoving && !_follower.IsMoving)
                    StartWorkOrFail();
                _wasMoving = _follower.IsMoving;
                return;
            }

            if (State == WorkerState.Working)
            {
                WorkRemainingSeconds -= effectiveDelta * EffectiveWorkSpeed;
                if (WorkRemainingSeconds <= 0f)
                    CompleteCurrentJob();
                return;
            }

            if (!AutoClaimJobs)
                return;

            _claimTimer -= effectiveDelta;
            if (_claimTimer <= 0f)
            {
                _claimTimer = EffectiveClaimInterval;
                ClaimNextJob();
            }
        }

        public bool ClaimNextJob()
        {
            ResolveReferences();
            if (_queue == null || _grid == null || _follower == null || _body == null || State != WorkerState.Idle)
                return false;

            Vector2I workerCell = _grid.WorldToCell(_body.GlobalPosition);
            string jobId = _queue.ClaimNextJob(WorkerId, workerCell);
            if (string.IsNullOrEmpty(jobId))
                return false;

            return BeginClaimedJob(jobId);
        }

        public bool AssignJob(string jobId)
        {
            ResolveReferences();
            if (_queue == null || State != WorkerState.Idle || string.IsNullOrEmpty(jobId))
                return false;

            if (_queue.GetJobState(jobId) == GridJobQueueComponent.GridJobState.Queued)
            {
                if (!_queue.ClaimJob(jobId, WorkerId))
                    return false;
            }
            else if (_queue.GetJobState(jobId) != GridJobQueueComponent.GridJobState.Claimed
                || _queue.GetJobClaimedBy(jobId) != WorkerId)
            {
                EmitSignal(SignalName.WorkerFailedJob, WorkerId, jobId, "claimed_by_another_worker");
                return false;
            }

            return BeginClaimedJob(jobId);
        }

        public void CancelCurrentJob(string reason = "worker_cancelled")
        {
            string failedJob = CurrentJobId;
            if (!string.IsNullOrEmpty(CurrentJobId) && _queue != null)
                _queue.ReleaseJob(CurrentJobId, WorkerId);

            _follower?.CancelMove();
            CurrentJobId = "";
            WorkRemainingSeconds = 0f;
            SetState(WorkerState.Idle);
            EmitSignal(SignalName.WorkerFailedJob, WorkerId, failedJob, reason);
        }

        private bool BeginClaimedJob(string jobId)
        {
            if (_queue == null || _follower == null)
                return false;

            Vector2I cell = _queue.GetJobCell(jobId);
            if (cell.X == int.MinValue)
                return false;

            CurrentJobId = jobId;
            string kind = _queue.GetJobKind(jobId);
            EmitSignal(SignalName.WorkerClaimedJob, WorkerId, jobId, kind, cell.X, cell.Y);

            if (!_follower.MoveToCell(cell))
            {
                _queue.ReleaseJob(jobId, WorkerId);
                string failedJob = CurrentJobId;
                CurrentJobId = "";
                SetState(WorkerState.Idle);
                EmitSignal(SignalName.WorkerFailedJob, WorkerId, failedJob, "no_path");
                return false;
            }

            _wasMoving = true;
            SetState(WorkerState.MovingToJob);
            return true;
        }

        private void StartWorkOrFail()
        {
            if (_queue == null || string.IsNullOrEmpty(CurrentJobId))
            {
                SetState(WorkerState.Idle);
                return;
            }

            if (!_queue.HasJob(CurrentJobId))
            {
                string failedJob = CurrentJobId;
                CurrentJobId = "";
                SetState(WorkerState.Idle);
                EmitSignal(SignalName.WorkerFailedJob, WorkerId, failedJob, "job_missing");
                return;
            }

            WorkRemainingSeconds = Mathf.Max(0.01f, _queue.GetJobWorkSeconds(CurrentJobId));
            SetState(WorkerState.Working);
            EmitSignal(SignalName.WorkerStartedJob, WorkerId, CurrentJobId);
        }

        private void CompleteCurrentJob()
        {
            if (_queue == null || string.IsNullOrEmpty(CurrentJobId))
            {
                SetState(WorkerState.Idle);
                return;
            }

            string jobId = CurrentJobId;
            if (!_queue.CompleteJob(jobId, WorkerId))
            {
                CurrentJobId = "";
                WorkRemainingSeconds = 0f;
                SetState(WorkerState.Idle);
                EmitSignal(SignalName.WorkerFailedJob, WorkerId, jobId, "complete_rejected");
                return;
            }
            CurrentJobId = "";
            WorkRemainingSeconds = 0f;
            SetState(WorkerState.Idle);
            EmitSignal(SignalName.WorkerCompletedJob, WorkerId, jobId);
        }

        private void SetState(WorkerState state)
        {
            if (State == state)
                return;

            State = state;
            EmitSignal(SignalName.WorkerStateChanged, WorkerId, (int)State);
        }

        private void ResolveReferences()
        {
            if (_body == null || !GodotObject.IsInstanceValid(_body))
            {
                _body = GetParent() as Node2D;
            }

            if (_queue == null || !GodotObject.IsInstanceValid(_queue))
                _queue = !JobQueuePath.IsEmpty
                    ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;

            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_follower == null || !GodotObject.IsInstanceValid(_follower))
                _follower = !PathFollowerPath.IsEmpty
                    ? GetNodeOrNull<GridPathFollowerComponent>(PathFollowerPath)
                    : EntityComponent.FindComponent<GridPathFollowerComponent>(GetParent(), recursive: false);
        }
    }
}
