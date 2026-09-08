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
    public partial class GridWorkerComponent : GameplayComponent, IWorker, ISaveable, IActorResidencyGuard
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

        /// <summary>
        /// The clock that decides what a turn of work is. Empty finds one
        /// scene-wide; with none anywhere the worker burns work off its own
        /// frame delta, so a template scene or a headless probe still runs.
        /// </summary>
        [Export] public NodePath WorkClockPath { get; set; } = new("");
        [Export] public string WorkerId { get; set; } = "";
        [Export] public bool AutoClaimJobs { get; set; } = true;

        /// <summary>
        /// Job kinds this worker will claim; empty claims everything. This is
        /// what keeps a mixed workforce sane: a boat lists "fish", a survey
        /// crew "survey", and the truck lists the land kinds - otherwise a
        /// land worker keeps claiming water jobs it can never path to and
        /// churns claim/release forever.
        /// </summary>
        [Export] public Godot.Collections.Array<string> AllowedJobKinds { get; set; } = new();
        [Export(PropertyHint.Range, "0.05,5,0.05")] public float ClaimIntervalSeconds { get; set; } = 0.25f;
        [Export(PropertyHint.Range, "0.01,20,0.01")] public float WorkSpeedMultiplier { get; set; } = 1f;

        public WorkerState State { get; private set; } = WorkerState.Idle;
        public string CurrentJobId { get; private set; } = "";

        /// <summary>Turns of work left on the current job - the grid's one unit.</summary>
        public float WorkRemainingTurns
        {
            get => HasWorldExecution && GodotObject.IsInstanceValid(_queue)
                ? _queue!.GetJobRemainingTurns(CurrentJobId) : _workRemainingTurns;
            private set => _workRemainingTurns = value;
        }
        private float _workRemainingTurns;

        /// <summary>IWorker.IsWorking - executing the job's work timer right now,
        /// not idle and not still travelling to it.</summary>
        public bool IsWorking => State == WorkerState.Working;

        private GridJobQueueComponent? _queue;
        private GridProjectionComponent? _grid;
        private GridPathFollowerComponent? _follower;
        private Node2D? _body;
        private GridWorkClockComponent? _workClock;
        private bool _workClockConnected;
        private float _claimTimer;
        private bool _wasMoving;
        private Vector2I _workCell;
        public float EffectiveClaimInterval => Mathf.Max(0.01f, float.IsFinite(ClaimIntervalSeconds) ? ClaimIntervalSeconds : 0.25f);
        public float EffectiveWorkSpeed => Mathf.Max(0.01f, float.IsFinite(WorkSpeedMultiplier) ? WorkSpeedMultiplier : 1f);

        public override void _Ready()
        {
            base._Ready();
            var actor = ActorComponent.ForBody(GetParent());
            WorkerId = actor is { ActorId.Length: > 0 } ? actor.ActorId
                : string.IsNullOrWhiteSpace(WorkerId) ? $"{GetParent()?.Name ?? Name}_{GetInstanceId()}" : WorkerId.Trim();
            ResolveReferences();
            BindExecution();
            if (!Engine.IsEditorHint())
                BindWorkClock();
            SetProcess(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (!HasWorldExecution && _queue != null && GodotObject.IsInstanceValid(_queue) && !string.IsNullOrEmpty(CurrentJobId))
                _queue.ReleaseJob(CurrentJobId, WorkerId);
            if (_workClockConnected && _workClock != null && GodotObject.IsInstanceValid(_workClock))
                _workClock.WorkTick -= OnWorkTick;
            _workClockConnected = false;
            UnbindExecution();
            base._ExitTree();
        }

        private void BindWorkClock()
        {
            if (!ExecutionPath.IsEmpty) return;
            _workClock = GridWorkClockComponent.FindFor(this, WorkClockPath);

            if (_workClock == null || _workClockConnected)
                return;

            _workClock.WorkTick += OnWorkTick;
            _workClockConnected = true;
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
            if (State == WorkerState.Idle && !AutoClaimJobs) return;
            if (ActorComponent.ForBody(GetParent()) is { } actor && (!actor.IsActive || actor.IsDead)) return;
            ResolveReferences();
            if (_queue == null || _grid == null || _follower == null || _body == null)
                return;

            if (_worldOwned && !HasWorldExecution)
            {
                if (_queue.GetJobState(CurrentJobId) == GridJobQueueComponent.GridJobState.Completed) NotifyJobCompleted();
                else CancelCurrentJob("world_execution_ended");
                return;
            }

            if (State != WorkerState.Idle && !HasCurrentClaim())
            {
                CancelCurrentJob("job_no_longer_claimed");
                return;
            }

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
                // Work is measured in TURNS and advances on the work clock, not
                // on frames - that is what lets one authored duration mean the
                // same thing in a turn-based game and an RTS. Walking to the
                // site and polling for a job stay real-time on both axes: a
                // worker animates between turns, it does not teleport.
                if (ExecutionPath.IsEmpty && _workClock == null)
                    AdvanceWork(effectiveDelta);
                return;
            }

            if (!AutoClaimJobs || ActorComponent.ForBody(_body) is { HasOrders: true })
                return;

            _claimTimer -= effectiveDelta;
            if (_claimTimer <= 0f)
            {
                _claimTimer = EffectiveClaimInterval;
                ClaimNextJob();
            }
        }

        /// <summary>
        /// Burns turns of work off the claimed job. Bound to
        /// GridWorkClockComponent.WorkTick when the scene has one; called
        /// directly from Tick when it does not, so a template scene or a
        /// headless probe still runs with nothing driving it.
        /// </summary>
        public void AdvanceWork(float turns)
        {
            if (!ExecutionPath.IsEmpty) return;
            if (!IsActive || State != WorkerState.Working) return;
            if (ActorComponent.ForBody(GetParent()) is { } actor && (!actor.IsActive || actor.IsDead)) return;
            ResolveReferences();
            if (_queue == null)
                return;
            if (!HasCurrentClaim())
            {
                CancelCurrentJob("job_no_longer_claimed");
                return;
            }

            if (!float.IsFinite(turns) || turns <= 0f)
                return;

            var result = _queue.AdvanceClaimedWork(CurrentJobId, WorkerId, _workCell, turns, EffectiveWorkSpeed);
            WorkRemainingTurns = _queue.GetJobRemainingTurns(CurrentJobId);
            if (result == GridJobQueueComponent.WorkAdvanceResult.Completed)
                NotifyJobCompleted();
            else if (result == GridJobQueueComponent.WorkAdvanceResult.Rejected)
                CancelCurrentJob("work_advance_rejected");
        }

        private void OnWorkTick(float turns) => AdvanceWork(turns);

        public void Save(GameBuilder.GameStateData state)
        {
            state.GameData["worker"] = new Godot.Collections.Dictionary
            {
                ["id"] = WorkerId, ["job"] = CurrentJobId, ["state"] = (int)State,
                ["remaining"] = WorkRemainingTurns, ["work_x"] = _workCell.X, ["work_y"] = _workCell.Y,
                ["world_owned"] = _worldOwned
            };
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (!state.GameData.TryGetValue("worker", out var saved)) return;
            ResolveReferences();
            var record = saved.AsGodotDictionary();
            if (GridVariantReader.Bool(record, "world_owned", false)
                || (GodotObject.IsInstanceValid(_execution) && _execution!.GetWorkerJob(WorkerId).Length > 0))
            {
                SynchronizeExecution();
                return;
            }
            WorkerId = record["id"].AsString();
            CurrentJobId = record["job"].AsString();
            WorkRemainingTurns = record["remaining"].AsSingle();
            _workCell = new(record["work_x"].AsInt32(), record["work_y"].AsInt32());
            var restoredState = (WorkerState)record["state"].AsInt32();
            if (CurrentJobId.Length > 0 && _queue is not null)
            {
                if (_queue.GetJobState(CurrentJobId) == GridJobQueueComponent.GridJobState.Queued)
                    _queue.ClaimJob(CurrentJobId, WorkerId);
                if (_queue.GetJobClaimedBy(CurrentJobId) != WorkerId
                    || !_queue.TryReserveWorkCell(CurrentJobId, WorkerId, _workCell))
                {
                    _queue.ReleaseJob(CurrentJobId, WorkerId);
                    CurrentJobId = "";
                    restoredState = WorkerState.Idle;
                }
            }
            if (_queue is null || CurrentJobId.Length == 0)
            {
                CurrentJobId = "";
                WorkRemainingTurns = 0;
                restoredState = WorkerState.Idle;
            }
            _wasMoving = _follower?.IsMoving == true;
            SetState(restoredState);
        }

        public bool ClaimNextJob()
        {
            if (ActorComponent.ForBody(GetParent()) is { } actor && (!actor.IsActive || actor.IsDead)) return false;
            ResolveReferences();
            if (_queue == null || _grid == null || _follower == null || _body == null || State != WorkerState.Idle)
                return false;

            Vector2I workerCell = _grid.WorldToCell(_body.GlobalPosition);
            string jobId = _queue.ClaimNextJob(WorkerId, workerCell, AllowedJobKinds);
            if (string.IsNullOrEmpty(jobId))
                return false;

            return BeginClaimedJob(jobId);
        }

        public bool AssignJob(string jobId)
        {
            ResolveReferences();
            if (_queue == null || State != WorkerState.Idle || !CanAcceptJob(jobId))
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

        public bool CanAcceptJob(string jobId)
        {
            if (ActorComponent.ForBody(GetParent()) is { } actor && (!actor.IsActive || actor.IsDead)) return false;
            ResolveReferences();
            if (_queue is null || string.IsNullOrWhiteSpace(jobId) || !_queue.HasJob(jobId)
                || !_queue.CanWorkerClaim(WorkerId)) return false;
            if (AllowedJobKinds.Count > 0 && !AllowedJobKinds.Contains(_queue.GetJobKind(jobId))) return false;
            var state = _queue.GetJobState(jobId);
            return state == GridJobQueueComponent.GridJobState.Queued
                || (state == GridJobQueueComponent.GridJobState.Claimed && _queue.GetJobClaimedBy(jobId) == WorkerId);
        }

        public void CancelCurrentJob(string reason = "worker_cancelled")
        {
            string failedJob = CurrentJobId;
            if (HasWorldExecution) _execution!.StopWork(WorkerId, true);
            _worldOwned = false;
            if (!string.IsNullOrEmpty(CurrentJobId) && _queue != null)
                _queue.ReleaseJob(CurrentJobId, WorkerId);

            _follower?.CancelMove();
            CurrentJobId = "";
            WorkRemainingTurns = 0f;
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

            if (_queue.GetJobState(jobId) != GridJobQueueComponent.GridJobState.Claimed
                || _queue.GetJobClaimedBy(jobId) != WorkerId)
            {
                CancelCurrentJob("job_no_longer_claimed");
                return false;
            }

            // Stand where the job says to stand - a build site names a cell
            // just outside its blocked footprint - and only if that cell is
            // unreachable fall back to the job cell itself, so a job that
            // set no approach cell (or one behind a wall) behaves exactly as
            // before.
            Vector2I approach = _queue.GetReservedWorkCell(jobId);
            bool moving = approach.X != int.MinValue && approach != cell && _follower.MoveToCell(approach);
            if (!moving)
                moving = _queue.TryReserveWorkCell(jobId, WorkerId, cell) && _follower.MoveToCell(cell);
            if (!moving)
            {
                _queue.ReleaseJob(jobId, WorkerId);
                string failedJob = CurrentJobId;
                CurrentJobId = "";
                SetState(WorkerState.Idle);
                EmitSignal(SignalName.WorkerFailedJob, WorkerId, failedJob, "no_path");
                return false;
            }

            _wasMoving = true;
            _workCell = _follower.DestinationCell;
            SetState(WorkerState.MovingToJob);
            return true;
        }

        private void StartWorkOrFail()
        {
            if (_follower is null || !_follower.HasReachedDestination || _follower.DestinationCell != _workCell)
            {
                if (_follower is { IsMoving: false } && _follower.LastMoveFailure.Length > 0
                    && _queue is not null && _queue.HasJob(CurrentJobId))
                {
                    Vector2I fallback = _queue.GetJobCell(CurrentJobId);
                    if (_workCell != fallback && _queue.TryReserveWorkCell(CurrentJobId, WorkerId, fallback)
                        && _follower.MoveToCell(fallback))
                    {
                        _workCell = fallback;
                        _wasMoving = true;
                        return;
                    }
                }
                CancelCurrentJob("job_destination_not_reached");
                return;
            }
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

            // The job may have been cancelled or reassigned while this worker
            // walked to it (visible when the queue keeps cancelled jobs).
            // Working through it anyway wasted the whole duration and then
            // failed at CompleteJob with a misleading reason.
            if (_queue.GetJobState(CurrentJobId) != GridJobQueueComponent.GridJobState.Claimed
                || _queue.GetJobClaimedBy(CurrentJobId) != WorkerId)
            {
                string failedJob = CurrentJobId;
                CurrentJobId = "";
                SetState(WorkerState.Idle);
                EmitSignal(SignalName.WorkerFailedJob, WorkerId, failedJob, "job_no_longer_claimed");
                return;
            }

            WorkRemainingTurns = _queue.GetJobRemainingTurns(CurrentJobId);
            SetState(WorkerState.Working);
            if (!ExecutionPath.IsEmpty)
            {
                if (!GodotObject.IsInstanceValid(_execution) || !_execution!.BeginWork(WorkerId, CurrentJobId, EffectiveWorkSpeed))
                {
                    CancelCurrentJob("world_execution_rejected");
                    return;
                }
                _worldOwned = true;
            }
            EmitSignal(SignalName.WorkerStartedJob, WorkerId, CurrentJobId);
        }

        private void NotifyJobCompleted()
        {
            _worldOwned = false;
            if (_queue == null || string.IsNullOrEmpty(CurrentJobId))
            {
                SetState(WorkerState.Idle);
                return;
            }

            string jobId = CurrentJobId;
            CurrentJobId = "";
            WorkRemainingTurns = 0f;
            SetState(WorkerState.Idle);
            EmitSignal(SignalName.WorkerCompletedJob, WorkerId, jobId);
        }

        private bool HasCurrentClaim()
            => _queue is not null && _queue.GetJobState(CurrentJobId) == GridJobQueueComponent.GridJobState.Claimed
                && _queue.GetJobClaimedBy(CurrentJobId) == WorkerId
                && _queue.GetReservedWorkCell(CurrentJobId) == _workCell;

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

            Resolve(JobQueuePath, ref _queue);
            Resolve(GridPath, ref _grid);

            // Not the shared rule: the follower is a sibling on the same body,
            // never found scene-wide.
            if (_follower == null || !GodotObject.IsInstanceValid(_follower))
                _follower = !PathFollowerPath.IsEmpty
                    ? GetNodeOrNull<GridPathFollowerComponent>(PathFollowerPath)
                    : EntityComponent.FindComponent<GridPathFollowerComponent>(GetParent(), recursive: false);
        }
    }
}
