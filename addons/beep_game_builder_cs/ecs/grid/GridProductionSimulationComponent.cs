using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ProductionState = Beep.ECS.GridProductionComponent.ProductionState;

namespace Beep.ECS;

/// <summary>World-owned production records. No building or actor scenes are instantiated.</summary>
[GlobalClass]
public partial class GridProductionSimulationComponent : Node, ISaveable
{
    [Export] public NodePath WorkClockPath { get; set; } = new("");
    [Export] public NodePath ResourceWalletPath { get; set; } = new("");
    [Export] public Godot.Collections.Array Recipes { get; set; } = new();
    [Export] public bool Loop { get; set; } = true;
    [Export] public bool ConsumeInputsOnStart { get; set; } = true;
    [Export] public bool ParticipatesInSave { get; set; } = true;
    [Export] public string SaveKey { get; set; } = "grid_production_simulation.state";
    [Signal] public delegate void ProductionCompletedEventHandler(string processId, string recipeId);
    [Signal] public delegate void ProductionStartedEventHandler(string processId, string recipeId);
    [Signal] public delegate void ProductionRejectedEventHandler(string processId, string recipeId, string reason);
    [Signal] public delegate void ProductionStateChangedEventHandler(string processId, int state);
    public int ProductionCount => _records.Count;
    private readonly Dictionary<string, Entry> _records = new(StringComparer.Ordinal);
    private readonly Dictionary<long, string> _deadlines = new();
    private GridWorkClockComponent? _clock;
    private GridResourceWalletComponent? _wallet;
    private sealed class Entry(GridProductionProcess process)
    {
        public readonly GridProductionProcess Process = process;
        public long Deadline;
        public double Started;
        public string ActorId = "";
        public bool RemovePending, RefundOnRemoval;
    }

    public override void _Ready()
    {
        ConnectActorRegistry();
        _clock = GridWorkClockComponent.FindFor(this, WorkClockPath);
        if (_clock is not null)
        {
            _clock.TreeExiting += RetainAll;
            _clock.TreeEntered += ScheduleAll;
        }
        ScheduleAll();
        if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        SetProcess(false);
    }

    public override void _ExitTree()
    {
        DisconnectActorRegistry();
        RetainAll();
        if (GodotObject.IsInstanceValid(_clock))
        {
            _clock!.TreeExiting -= RetainAll;
            _clock.TreeEntered -= ScheduleAll;
        }
        _clock = null;
        if (ParticipatesInSave) RemoveFromGroup(SaveableHelper.Group);
        RequestReady();
    }

    private GridProductionProcess CreateProcess(string id, bool connectSignals = true)
    {
        var process = new GridProductionProcess
        {
            Recipes = Recipes, Loop = Loop, ConsumeInputsOnStart = ConsumeInputsOnStart,
            CanContinue = () => GodotObject.IsInstanceValid(this) && IsInsideTree()
                && _records.TryGetValue(id, out var current) && !current.RemovePending,
            ResolveWallet = () =>
            {
                EntityComponent.Resolve(this, ResourceWalletPath, ref _wallet);
                return _wallet;
            }
        };
        if (connectSignals) ConnectProcessSignals(id, process);
        return process;
    }

    private void ConnectProcessSignals(string id, GridProductionProcess process)
    {
        process.Completed += recipe =>
        {
            if (GodotObject.IsInstanceValid(this) && IsInsideTree()) EmitSignal(SignalName.ProductionCompleted, id, recipe);
        };
        process.Started += recipe =>
        {
            if (GodotObject.IsInstanceValid(this) && IsInsideTree()) EmitSignal(SignalName.ProductionStarted, id, recipe);
        };
        process.Rejected += (recipe, reason) =>
        {
            if (GodotObject.IsInstanceValid(this) && IsInsideTree()) EmitSignal(SignalName.ProductionRejected, id, recipe, reason);
        };
        process.StateChanged += state =>
        {
            if (GodotObject.IsInstanceValid(this) && IsInsideTree()) EmitSignal(SignalName.ProductionStateChanged, id, (int)state);
        };
    }

    public bool StartProduction(string processId, string recipeId)
    {
        if (string.IsNullOrWhiteSpace(processId)
            || !IsInsideTree() || !GodotObject.IsInstanceValid(_clock) || !_clock!.IsInsideTree()) return false;
        if (_records.TryGetValue(processId, out var existing))
        {
            bool started = existing.Process.Start(recipeId);
            FinishRemoval(processId, existing);
            Schedule(processId, existing);
            return started;
        }
        var entry = new Entry(CreateProcess(processId)) { ActorId = _actorLinks.GetValueOrDefault(processId, "") };
        _records.Add(processId, entry);
        if (!entry.Process.Start(recipeId)) { _records.Remove(processId); return false; }
        FinishRemoval(processId, entry);
        Schedule(processId, entry);
        return true;
    }

    public bool PauseProduction(string processId)
    {
        if (!_records.TryGetValue(processId, out var entry) || entry.Process.IsTransitioning) return false;
        Retain(entry);
        entry.Process.Pause();
        return true;
    }

    public bool ResumeProduction(string processId)
    {
        if (!_records.TryGetValue(processId, out var entry) || entry.Process.IsTransitioning) return false;
        entry.Process.Resume();
        Schedule(processId, entry);
        return true;
    }

    public bool RemoveProduction(string processId, bool refundInputs)
    {
        if (!_records.TryGetValue(processId, out var entry)) return false;
        Cancel(entry);
        if (entry.Process.IsTransitioning || entry.Process.IsAdvancing)
        {
            entry.RemovePending = true;
            entry.RefundOnRemoval |= refundInputs;
            return true;
        }
        entry.Process.Cancel(refundInputs);
        _records.Remove(processId);
        ForgetActorLink(processId);
        return true;
    }

    public Godot.Collections.Dictionary GetProduction(string processId)
        => _records.TryGetValue(processId, out var entry) ? entry.Process.Capture(Elapsed(entry)) : new();

    internal GridProductionProcess? FindProcess(string id) => _records.TryGetValue(id, out var entry) ? entry.Process : null;
    internal double ElapsedFor(string id) => _records.TryGetValue(id, out var entry) ? Elapsed(entry) : 0;

    public bool AdvanceProduction(string id, double turns)
    {
        if (!double.IsFinite(turns) || turns <= 0 || !_records.TryGetValue(id, out var entry)
            || entry.Process.IsTransitioning || entry.Process.IsAdvancing || entry.Process.State != ProductionState.Producing) return false;
        double elapsed = Elapsed(entry);
        Cancel(entry);
        entry.Process.Advance(turns + elapsed);
        FinishRemoval(id, entry);
        Schedule(id, entry);
        return true;
    }

    public bool CancelProduction(string id, bool refundInputs)
    {
        if (!_records.TryGetValue(id, out var entry) || entry.Process.IsTransitioning || entry.Process.IsAdvancing) return false;
        Cancel(entry);
        entry.Process.Cancel(refundInputs);
        FinishRemoval(id, entry);
        return true;
    }

    public bool CompleteProduction(string id)
    {
        if (!_records.TryGetValue(id, out var entry) || entry.Process.IsTransitioning || entry.Process.IsAdvancing) return false;
        Cancel(entry);
        bool result = entry.Process.Complete();
        FinishRemoval(id, entry);
        Schedule(id, entry);
        return result;
    }

    public bool RestoreProduction(string id, Godot.Collections.Dictionary state)
    {
        if (!_records.TryGetValue(id, out var entry) || entry.Process.IsTransitioning || entry.Process.IsAdvancing) return false;
        Cancel(entry);
        entry.Process.Restore(state);
        Schedule(id, entry);
        return true;
    }

    private double Elapsed(Entry entry) => entry.Deadline != 0 && GodotObject.IsInstanceValid(_clock)
        ? Math.Max(0, _clock!.ElapsedTurns - entry.Started) : 0;

    private void Cancel(Entry entry)
    {
        if (entry.Deadline == 0) return;
        if (GodotObject.IsInstanceValid(_clock)) _clock!.CancelScheduledWork(entry.Deadline);
        _deadlines.Remove(entry.Deadline);
        entry.Deadline = 0;
    }

    private void Retain(Entry entry)
    {
        double elapsed = Elapsed(entry);
        Cancel(entry);
        entry.Process.RetainElapsed(elapsed);
    }

    private void RetainAll() { foreach (var entry in _records.Values) Retain(entry); }
    private void ScheduleAll() { foreach (var (id, entry) in _records) Schedule(id, entry); }
    private void Schedule(string id, Entry entry)
    {
        if (entry.Deadline != 0 || entry.RemovePending || !_records.TryGetValue(id, out var current) || current != entry
            || entry.Process.State != ProductionState.Producing || !IsInsideTree()
            || !GodotObject.IsInstanceValid(_clock) || !_clock!.IsInsideTree()) return;
        entry.Started = _clock.ElapsedTurns;
        entry.Deadline = _clock.ScheduleWork(this,
            Math.Max(0.000001, entry.Process.RemainingTurns - entry.Process.PendingWorkTurns), nameof(ProcessDueProduction));
        if (entry.Deadline != 0) _deadlines.Add(entry.Deadline, id);
    }

    public void ProcessDueProduction(long requestId, double elapsedTurns)
    {
        if (!_deadlines.Remove(requestId, out var id) || !_records.TryGetValue(id, out var entry)
            || entry.Deadline != requestId) return;
        double elapsed = Elapsed(entry);
        entry.Deadline = 0;
        entry.Process.Advance(elapsed);
        FinishRemoval(id, entry);
        if (_records.TryGetValue(id, out var current) && current == entry) Schedule(id, entry);
    }

    public Godot.Collections.Dictionary CaptureState()
    {
        var result = new Godot.Collections.Dictionary();
        foreach (var (id, entry) in _records.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            using var state = entry.Process.Capture(Elapsed(entry));
            state["loop"] = entry.Process.Loop;
            state["consume_on_start"] = entry.Process.ConsumeInputsOnStart;
            state["actor_id"] = entry.ActorId;
            state["actor_registry_path"] = entry.ActorId.Length > 0 ? ActorRegistryPath.ToString() : "";
            result[id] = state;
        }
        return result;
    }

    public bool RestoreState(Godot.Collections.Dictionary state)
    {
        if (_records.Values.Any(entry => entry.Process.IsTransitioning || entry.Process.IsAdvancing)) return false;
        var restored = new Dictionary<string, Entry>(StringComparer.Ordinal);
        string restoredRegistryPath = "";
        foreach (var pair in state)
        {
            if (pair.Key.VariantType != Variant.Type.String || string.IsNullOrWhiteSpace(pair.Key.AsString())
                || !GridVariantReader.TryDictionary(pair.Value, out var saved)) return false;
            string id = pair.Key.AsString();
            var process = CreateProcess(id, connectSignals: false);
            process.Loop = GridVariantReader.Bool(saved, "loop", Loop);
            process.ConsumeInputsOnStart = GridVariantReader.Bool(saved, "consume_on_start", ConsumeInputsOnStart);
            process.Restore(saved);
            if (process.State != ProductionState.Idle && !process.HasRecipe(process.CurrentRecipeId)) return false;
            string registryPath = GridVariantReader.String(saved, "actor_registry_path", "");
            if (registryPath.Length > 0)
            {
                if (restoredRegistryPath.Length > 0 && restoredRegistryPath != registryPath) return false;
                restoredRegistryPath = registryPath;
            }
            restored.Add(id, new(process) { ActorId = GridVariantReader.String(saved, "actor_id", "") });
        }
        RetainAll();
        _records.Clear();
        _actorLinks.Clear();
        _actorProductionIds.Clear();
        if (ActorRegistryPath.IsEmpty && restoredRegistryPath.Length > 0) ActorRegistryPath = new(restoredRegistryPath);
        foreach (var pair in restored)
        {
            _records.Add(pair.Key, pair.Value);
            if (pair.Value.ActorId.Length > 0) RememberActorLink(pair.Key, pair.Value.ActorId);
            ConnectProcessSignals(pair.Key, pair.Value.Process);
        }
        ConnectActorRegistry();
        ScheduleAll();
        return true;
    }

    public void Save(GameBuilder.GameStateData state)
    {
        if (!string.IsNullOrWhiteSpace(SaveKey)) state.GameData[SaveKey] = CaptureState();
    }
    public void Load(GameBuilder.GameStateData state)
    {
        if (!string.IsNullOrWhiteSpace(SaveKey) && state.GameData.TryGetValue(SaveKey, out var value)
            && GridVariantReader.TryDictionary(value, out var saved)) RestoreState(saved);
    }
}
