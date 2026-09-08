using Godot;
using System;

namespace Beep.ECS;

/// <summary>Scene adapter for the shared production process: authored recipes,
/// wallet binding, clock deadlines, signals and save integration.</summary>
[Tool]
[GlobalClass]
public partial class GridProductionComponent : Node, ISaveable, IActorResidencyGuard
{
    public enum ProductionState { Idle, Producing, Paused }
    [Signal] public delegate void ProductionStartedEventHandler(string recipeId);
    [Signal] public delegate void ProductionCompletedEventHandler(string recipeId);
    [Signal] public delegate void ProductionRejectedEventHandler(string recipeId, string reason);
    [Signal] public delegate void ProductionStateChangedEventHandler(int state);

    private readonly GridProductionProcess _localProduction = new();
    private GridProductionProcess _production => WorldSimulation?.FindProcess(_boundProductionId) ?? _localProduction;
    private GridResourceWalletComponent? _wallet;
    private bool _transitioning => _production.IsTransitioning;
    private bool _advancingWork => _production.IsAdvancing;

    public GridProductionComponent()
    {
        _production.ResolveWallet = () =>
        {
            EntityComponent.Resolve(this, ResourceWalletPath, ref _wallet);
            return _wallet;
        };
        _production.Started += id => EmitSignal(SignalName.ProductionStarted, id);
        _production.Completed += id => EmitSignal(SignalName.ProductionCompleted, id);
        _production.Rejected += (id, reason) => EmitSignal(SignalName.ProductionRejected, id, reason);
        _production.StateChanged += state => EmitSignal(SignalName.ProductionStateChanged, (int)state);
    }

    [Export] public bool ParticipatesInSave { get; set; } = true;
    [Export] public string SaveKey { get; set; } = "grid_production.state";
    [Export] public NodePath ResourceWalletPath { get; set; } = new("");
    [Export] public NodePath WorkClockPath { get; set; } = new("");
    [Export] public Godot.Collections.Array Recipes { get => _production.Recipes; set => _production.Recipes = value; }
    [Export] public string ActiveRecipeId { get => _production.ActiveRecipeId; set => _production.ActiveRecipeId = value; }
    [Export] public bool AutoStart { get; set; }
    [Export] public bool Loop { get => _production.Loop; set => _production.Loop = value; }
    [Export] public bool ConsumeInputsOnStart { get => _production.ConsumeInputsOnStart; set => _production.ConsumeInputsOnStart = value; }

    public ProductionState State => _production.State;
    public string CurrentRecipeId => _production.CurrentRecipeId;
    public float RemainingTurns => (float)Math.Max(0, _production.RemainingTurns - ProductionElapsedTurns);
    public float EffectiveRemainingTurns => float.IsFinite(RemainingTurns) ? Math.Max(0, RemainingTurns) : 0;
    public double PendingWorkTurns => _production.PendingWorkTurns;
    public float Progress01 => _production.RecipeDuration(CurrentRecipeId) is > 0 and var duration
        ? Mathf.Clamp(1 - EffectiveRemainingTurns / duration, 0, 1) : 0;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) { SetProcess(false); return; }
        if (BindWorldSimulation())
        {
            SetProcess(false);
            StartAutomatically();
            UpdateConfigurationWarnings();
            return;
        }
        _productionClock = GridWorkClockComponent.FindFor(this, WorkClockPath);
        if (_productionClock is not null)
        {
            _productionClock.TreeExiting += RetainScheduledProgress;
            _productionClock.TreeEntered += ScheduleProduction;
        }
        SetProcess(_productionClock is null);
        ScheduleProduction();
        StartAutomatically();
        if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        UpdateConfigurationWarnings();
    }

    public override void _ExitTree()
    {
        UnbindWorldSimulation();
        RetainScheduledProgress();
        if (GodotObject.IsInstanceValid(_productionClock))
        {
            _productionClock!.TreeExiting -= RetainScheduledProgress;
            _productionClock.TreeEntered -= ScheduleProduction;
        }
        _productionClock = null;
        RequestReady();
        if (ParticipatesInSave) RemoveFromGroup(SaveableHelper.Group);
    }

    public override string[] _GetConfigurationWarnings() => !SimulationPath.IsEmpty
        ? Array.Empty<string>() : ResourceWalletPath.IsEmpty
            ? new[] { "ResourceWalletPath should point to a GridResourceWalletComponent." } : Array.Empty<string>();

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) Tick(delta);
    }

    public void Tick(double delta) => AdvanceWork(GridWorkClockBinding.TurnsForDelta(delta));

    public void AdvanceWork(float turns)
    {
        if (_usesWorldSimulation) { WorldSimulation?.AdvanceProduction(_boundProductionId, turns); return; }
        if (_transitioning || _advancingWork || State != ProductionState.Producing
            || !float.IsFinite(turns) || turns <= 0) return;
        double elapsed = ScheduledElapsedTurns;
        CancelProductionDeadline();
        AdvanceProduction(turns + elapsed);
    }

    private void AdvanceProduction(double turns)
    {
        try { _production.Advance(turns); }
        finally { ScheduleProduction(); }
    }

    public bool StartProduction(string recipeId = "")
    {
        if (_usesWorldSimulation) return EnsureWorldActorLink() && WorldSimulation?.StartProduction(_boundProductionId,
            string.IsNullOrWhiteSpace(recipeId) ? ActiveRecipeId : recipeId) == true;
        try { return _production.Start(recipeId); }
        finally { ScheduleProduction(); }
    }

    public void PauseProduction()
    {
        if (_usesWorldSimulation) { WorldSimulation?.PauseProduction(_boundProductionId); return; }
        if (_transitioning || State != ProductionState.Producing) return;
        RetainScheduledProgress();
        _production.Pause();
    }

    public void ResumeProduction()
    {
        if (_usesWorldSimulation) { WorldSimulation?.ResumeProduction(_boundProductionId); return; }
        _production.Resume();
        ScheduleProduction();
    }

    public void CancelProduction(bool refundInputs = false)
    {
        if (_usesWorldSimulation) { WorldSimulation?.CancelProduction(_boundProductionId, refundInputs); return; }
        if (_transitioning) return;
        CancelProductionDeadline();
        _production.Cancel(refundInputs);
    }

    public bool CompleteProduction()
    {
        if (_usesWorldSimulation) return WorldSimulation?.CompleteProduction(_boundProductionId) == true;
        if (_transitioning || State != ProductionState.Producing) return false;
        CancelProductionDeadline();
        try { return _production.Complete(); }
        finally { ScheduleProduction(); }
    }

    public Godot.Collections.Dictionary CaptureState() => _production.Capture(ProductionElapsedTurns);

    public void RestoreState(Godot.Collections.Dictionary state)
    {
        if (_usesWorldSimulation) { WorldSimulation?.RestoreProduction(_boundProductionId, state); return; }
        if (_transitioning) return;
        CancelProductionDeadline();
        _production.Restore(state);
        ScheduleProduction();
    }

    public GridProductionRecipe? FindRecipe(string recipeId) => _production.FindRecipe(recipeId);

    public void Save(GameBuilder.GameStateData state)
    {
        if (!SimulationPath.IsEmpty) return;
        if (!string.IsNullOrWhiteSpace(SaveKey)) state.GameData[SaveKey] = CaptureState();
    }

    public void Load(GameBuilder.GameStateData state)
    {
        if (!SimulationPath.IsEmpty) return;
        if (!string.IsNullOrWhiteSpace(SaveKey) && state.GameData.TryGetValue(SaveKey, out Variant value)
            && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved)) RestoreState(saved);
    }
}
