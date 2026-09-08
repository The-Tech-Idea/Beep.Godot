using Godot;

namespace Beep.ECS;

public partial class GridProductionComponent
{
    [ExportGroup("World Simulation")]
    [Export] public NodePath SimulationPath { get; set; } = new("");
    [Export] public string ProductionId { get; set; } = "";
    private GridProductionSimulationComponent? _simulation;
    private bool _usesWorldSimulation;
    private string _boundProductionId = "";
    private GridProductionSimulationComponent? WorldSimulation => _usesWorldSimulation
        && GodotObject.IsInstanceValid(_simulation) && _simulation!.IsInsideTree() ? _simulation : null;
    private double ProductionElapsedTurns => _usesWorldSimulation
        ? WorldSimulation?.ElapsedFor(_boundProductionId) ?? 0 : ScheduledElapsedTurns;

    private bool BindWorldSimulation()
    {
        _usesWorldSimulation = !SimulationPath.IsEmpty;
        if (!_usesWorldSimulation) return false;
        _boundProductionId = string.IsNullOrWhiteSpace(ProductionId)
            ? ActorComponent.ForBody(GetParent())?.ActorId ?? "" : ProductionId.Trim();
        _simulation = GetNodeOrNull<GridProductionSimulationComponent>(SimulationPath);
        if (_simulation is null || _boundProductionId.Length == 0)
        {
            GD.PushWarning($"[{Name}] World production requires a simulation service and a stable ProductionId or actor ID.");
            return true;
        }
        _localProduction.Recipes = _simulation.Recipes;
        if (!EnsureWorldActorLink())
        {
            GD.PushWarning($"[{Name}] Production record is linked to another actor or registry.");
            _simulation = null;
            return true;
        }
        _simulation.ProductionStarted += OnWorldProductionStarted;
        _simulation.ProductionCompleted += OnWorldProductionCompleted;
        _simulation.ProductionRejected += OnWorldProductionRejected;
        _simulation.ProductionStateChanged += OnWorldProductionStateChanged;
        return true;
    }

    private bool EnsureWorldActorLink()
    {
        var actor = ActorComponent.ForBody(GetParent());
        return actor?.Registry is not { } registry || WorldSimulation?.LinkActor(_boundProductionId, registry, actor.ActorId) == true;
    }

    private void UnbindWorldSimulation()
    {
        if (GodotObject.IsInstanceValid(_simulation))
        {
            _simulation!.ProductionStarted -= OnWorldProductionStarted;
            _simulation.ProductionCompleted -= OnWorldProductionCompleted;
            _simulation.ProductionRejected -= OnWorldProductionRejected;
            _simulation.ProductionStateChanged -= OnWorldProductionStateChanged;
        }
        _simulation = null;
    }

    private void StartAutomatically()
    {
        if (!AutoStart) return;
        GameStateManagerComponent.AfterWorldReady(this, () =>
        {
            if (State == ProductionState.Idle) StartProduction(ActiveRecipeId);
        }, _usesWorldSimulation ? WorldSimulation?.SaveKey : ParticipatesInSave ? SaveKey : null);
    }

    public bool CanSuspendActor() => _usesWorldSimulation
        ? WorldSimulation?.FindProcess(_boundProductionId) is not null
        : State == ProductionState.Idle;

    private bool AcceptWorldEvent(string id) => id == _boundProductionId && IsInsideTree()
        && !IsQueuedForDeletion() && WorldSimulation is not null;

    private void OnWorldProductionStarted(string id, string recipe)
    {
        if (AcceptWorldEvent(id)) EmitSignal(SignalName.ProductionStarted, recipe);
    }
    private void OnWorldProductionCompleted(string id, string recipe)
    {
        if (AcceptWorldEvent(id)) EmitSignal(SignalName.ProductionCompleted, recipe);
    }
    private void OnWorldProductionRejected(string id, string recipe, string reason)
    {
        if (AcceptWorldEvent(id)) EmitSignal(SignalName.ProductionRejected, recipe, reason);
    }
    private void OnWorldProductionStateChanged(string id, int state)
    {
        if (AcceptWorldEvent(id)) EmitSignal(SignalName.ProductionStateChanged, state);
    }
}
