using Godot;
using System;

namespace Beep.ECS;

public partial class GridProductionComponent
{
    private GridWorkClockComponent? _productionClock;
    private long _productionDeadline;
    private double _scheduledAtTurn;

    private double ScheduledElapsedTurns => _productionDeadline != 0
        && GodotObject.IsInstanceValid(_productionClock)
        ? Math.Max(0, _productionClock!.ElapsedTurns - _scheduledAtTurn) : 0;

    private void CancelProductionDeadline()
    {
        if (_productionDeadline != 0 && GodotObject.IsInstanceValid(_productionClock))
            _productionClock!.CancelScheduledWork(_productionDeadline);
        _productionDeadline = 0;
    }

    private void RetainScheduledProgress()
    {
        double elapsed = ScheduledElapsedTurns;
        CancelProductionDeadline();
        _production.RetainElapsed(elapsed);
    }

    private void ScheduleProduction()
    {
        if (_usesWorldSimulation || _transitioning || _advancingWork || _productionDeadline != 0
            || State != ProductionState.Producing || !IsInsideTree()
            || !GodotObject.IsInstanceValid(_productionClock) || !_productionClock!.IsInsideTree()) return;
        _scheduledAtTurn = _productionClock.ElapsedTurns;
        // Unpaid zero-remaining cycles retry on a later world tick, not recursively.
        double delay = Math.Max(0.000001, EffectiveRemainingTurns - PendingWorkTurns);
        _productionDeadline = _productionClock.ScheduleWork(this, delay, nameof(ProcessScheduledProduction));
    }

    public void ProcessScheduledProduction(long requestId, double elapsedTurns)
    {
        if (_productionDeadline == 0 || requestId != _productionDeadline || _transitioning || _advancingWork) return;
        double elapsed = ScheduledElapsedTurns;
        CancelProductionDeadline();
        if (State == ProductionState.Producing) AdvanceProduction(elapsed);
    }
}
