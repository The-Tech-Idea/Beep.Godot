using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridWorkerDispatchComponent
{
    public Godot.Collections.Dictionary CaptureState()
    {
        var workers = new Godot.Collections.Dictionary();
        foreach (var (id, worker) in _workers)
        {
            var retries = new Godot.Collections.Dictionary();
            foreach (var (job, deadline) in worker.RetryDeadlines)
            {
                double delay = deadline - (GodotObject.IsInstanceValid(_clock) ? _clock!.ElapsedTurns : 0);
                if (delay > 0) retries[job] = delay;
            }
            workers[id] = new Godot.Collections.Dictionary
            {
                ["move_speed"] = worker.MoveSpeed, ["work_speed"] = worker.WorkSpeed,
                ["job"] = worker.Job, ["working"] = worker.Working, ["retries"] = retries
            };
        }
        return new Godot.Collections.Dictionary
        {
            ["workers"] = workers, ["allowed_kinds"] = AllowedJobKinds.Duplicate(),
            ["retry_turns"] = FailedJobRetryTurns
        };
    }

    /// <summary>Restore registry, queue (retaining claims), travel and execution first, while the clock is paused.</summary>
    public bool RestoreState(Godot.Collections.Dictionary state)
    {
        if (!Ready || _dispatching || !state.TryGetValue("workers", out var records)
            || !GridVariantReader.TryDictionary(records, out var savedWorkers)
            || !state.TryGetValue("allowed_kinds", out var kinds) || kinds.VariantType != Variant.Type.Array) return false;
        float retryTurns = GridVariantReader.Float(state, "retry_turns", float.NaN);
        if (!float.IsFinite(retryTurns) || retryTurns <= 0) return false;
        var restoredKinds = new Godot.Collections.Array<string>();
        foreach (var value in kinds.AsGodotArray())
        {
            if (value.VariantType != Variant.Type.String) return false;
            restoredKinds.Add(value.AsString());
        }
        var restored = new Dictionary<string, Worker>(StringComparer.Ordinal);
        foreach (var pair in savedWorkers)
        {
            if (pair.Key.VariantType != Variant.Type.String || !GridVariantReader.TryDictionary(pair.Value, out var saved)) return false;
            string id = pair.Key.AsString();
            float move = GridVariantReader.Float(saved, "move_speed", float.NaN);
            float work = GridVariantReader.Float(saved, "work_speed", float.NaN);
            if (!float.IsFinite(move) || move <= 0 || !float.IsFinite(work) || work <= 0
                || !_registry!.GetActorPosition(id).IsFinite() || !_queue!.CanWorkerClaim(id)
                || !saved.TryGetValue("job", out var job) || job.VariantType != Variant.Type.String
                || !saved.TryGetValue("working", out var working) || working.VariantType != Variant.Type.Bool
                || !saved.TryGetValue("retries", out var retries) || !GridVariantReader.TryDictionary(retries, out var delays)) return false;
            var worker = new Worker(move, work) { Job = job.AsString(), Working = working.AsBool() };
            if (worker.Job.Length == 0 && worker.Working) return false;
            foreach (var delay in delays)
            {
                if (delay.Key.VariantType != Variant.Type.String || delay.Value.VariantType is not (Variant.Type.Float or Variant.Type.Int)) return false;
                double turns = delay.Value.AsDouble();
                if (!double.IsFinite(turns) || turns <= 0) return false;
                worker.RetryDeadlines.Add(delay.Key.AsString(), _clock!.ElapsedTurns + turns);
            }
            if (worker.Job.Length > 0 && (!AssignmentMatches(id, worker)
                || !_queue.CanBindDispatcher(worker.Job, id, this))) return false;
            restored.Add(id, worker);
        }
        // Replacing metadata must not orphan an assignment still running in the restored services.
        foreach (var (id, old) in _workers)
            if (old.Job.Length > 0 && AssignmentMatches(id, old)
                && (!restored.TryGetValue(id, out var next) || next.Job != old.Job || next.Working != old.Working)) return false;

        foreach (var worker in _workers.Values) _queue!.UnbindDispatcher(worker.Job, this);
        _workers.Clear();
        foreach (var (id, worker) in restored)
        {
            if (worker.Job.Length > 0) _queue!.BindDispatcher(worker.Job, id, this);
            _workers.Add(id, worker);
        }
        AllowedJobKinds = restoredKinds;
        FailedJobRetryTurns = retryTurns;
        return true;
    }

    private bool AssignmentMatches(string id, Worker worker) =>
        _queue!.GetJobState(worker.Job) == GridJobQueueComponent.GridJobState.Claimed
        && _queue.GetJobClaimedBy(worker.Job) == id
        && (worker.Working ? _execution!.MatchesWork(id, worker.Job, worker.WorkSpeed)
            : _registry!.IsDormant(id) && _travel!.MatchesRoute(id, _queue.GetReservedWorkCell(worker.Job), worker.MoveSpeed));

    public void Save(GameBuilder.GameStateData state) { if (SaveKey.Length > 0) state.GameData[SaveKey] = CaptureState(); }
    public void Load(GameBuilder.GameStateData state)
    {
        if (state.GameData.TryGetValue(SaveKey, out var saved) && GridVariantReader.TryDictionary(saved, out var data)) RestoreState(data);
    }
}
