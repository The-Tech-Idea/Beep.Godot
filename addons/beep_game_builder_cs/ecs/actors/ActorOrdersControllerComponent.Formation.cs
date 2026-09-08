using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class ActorOrdersControllerComponent
{
    [Signal] public delegate void FormationFinishedEventHandler(int dispatched, int rejected);
    [Export(PropertyHint.Range, "1,64,1")] public int MaximumQueuedFormations { get; set; } = 16;
    public bool IsFormationPending => _formation.Count > 0;
    public int QueuedFormationCount => _queuedFormations.Count;
    private sealed class FormationMember(ActorComponent? actor, ulong revision, string id = "")
    {
        public ActorComponent? Actor = actor;
        public readonly string Id = id.Length > 0 ? id : actor?.ActorId ?? "";
        public ulong Revision = revision;
        public void Deconstruct(out ActorComponent? actor, out ulong revision) { actor = Actor; revision = Revision; }
    }
    private sealed record FormationPlan(Queue<FormationMember> Members, List<Vector2I> Candidates, bool Append);
    private Queue<FormationMember> _formation = new();
    private readonly Queue<FormationPlan> _queuedFormations = new();
    private readonly HashSet<Vector2I> _assigned = new();
    private List<Vector2I> _candidates = new();
    private GridNavigationComponent? _formationNavigation;
    private string _issuer = "";
    private long _formationRequest;
    private ulong _formationVersion;
    private int _candidateIndex, _dispatched, _rejected;
    private bool _append;
    private Vector2I _plannedStart, _plannedGoal;

    internal bool ReferencesActor(string id)
    {
        foreach (var member in _formation)
            if (member.Id == id && member.Actor is not null && FormationActorValid(member.Actor, member.Revision)) return true;
        foreach (var plan in _queuedFormations)
            foreach (var member in plan.Members)
                if (member.Id == id && member.Actor is not null && FormationActorValid(member.Actor, member.Revision)) return true;
        return false;
    }

    private int BeginFormation(string[] ids, List<Vector2I> candidates, bool append)
    {
        if (candidates.Count == 0) return 0;
        if (IsFormationPending && (_issuer != _player!.PlayerId
            || _queuedFormations.Count >= Mathf.Clamp(MaximumQueuedFormations, 1, 64))) return 0;
        _issuer = _player!.PlayerId;
        var members = new Queue<FormationMember>();
        foreach (string id in ids)
        {
            var actor = _player.Registry!.FindActor(id);
            using var command = new ActorCommand { IssuerId = _issuer, Action = ActorAction.Move, TargetCell = candidates[0] };
            if (actor is null || actor.OwnerId != _issuer || actor.Validate(command).Length > 0) continue;
            if (!append) actor.CancelOrders();
            members.Enqueue(new(actor, actor.CommandRevision));
        }
        if (members.Count == 0) return 0;
        var plan = new FormationPlan(members, candidates, append);
        if (IsFormationPending) _queuedFormations.Enqueue(plan);
        else StartFormation(plan);
        return members.Count;
    }

    private void StartFormation(FormationPlan plan)
    {
        _formation = plan.Members;
        _candidates = plan.Candidates;
        _append = plan.Append;
        _formationNavigation = _navigation;
        _formationNavigation!.PathRequestCompleted += FormationPathCompleted;
    }

    public void CancelFormation()
    {
        _formationVersion++;
        _queuedFormations.Clear();
        ResetCurrentFormation();
    }

    private void ResetCurrentFormation()
    {
        if (GodotObject.IsInstanceValid(_formationNavigation))
        {
            if (_formationRequest != 0) _formationNavigation!.CancelPathRequest(_formationRequest);
            _formationNavigation!.PathRequestCompleted -= FormationPathCompleted;
        }
        _formationNavigation = null;
        _formationRequest = 0;
        _formation.Clear(); _assigned.Clear(); _candidates.Clear();
        _candidateIndex = _dispatched = _rejected = 0;
    }

    private bool FormationActorValid(ActorComponent actor, ulong revision)
        => GodotObject.IsInstanceValid(_player) && _player!.IsInsideTree() && _player.PlayerId == _issuer
            && GodotObject.IsInstanceValid(_player.Registry)
            && GodotObject.IsInstanceValid(actor) && actor.IsInsideTree() && actor.Body is not null
            && actor.Registry == _player?.Registry && actor.OwnerId == _issuer
            && actor.CommandRevision == revision && actor.IsActive && !actor.IsDead;

    private void AdvanceFormation()
    {
        if (!IsFormationPending) return;
        if (!GodotObject.IsInstanceValid(_formationNavigation) || !_formationNavigation!.IsInsideTree()
            || !GodotObject.IsInstanceValid(_grid) || !GodotObject.IsInstanceValid(_player)
            || _player!.PlayerId != _issuer || _player.Registry is null)
        { CancelFormation(); return; }
        if (_formationRequest != 0)
        {
            if (!_formationNavigation.HasPathRequest(_formationRequest))
            { _formationRequest = 0; FinishFormationActor(false); }
            return;
        }
        var (actor, revision) = _formation.Peek();
        if (actor is null || !FormationActorValid(actor, revision)) { FinishFormationActor(false); return; }
        if (_append && actor.HasOrders) return;
        // Bound candidate filtering independently of the navigation expansion budget.
        for (int inspected = 0; inspected < 64 && _candidateIndex < _candidates.Count; inspected++)
        {
            Vector2I goal = _candidates[_candidateIndex++];
            if (_assigned.Contains(goal) || !_formationNavigation.IsInBounds(goal) || _formationNavigation.IsBlocked(goal)) continue;
            _plannedStart = _grid!.WorldToCell(actor.Body!.GlobalPosition);
            _plannedGoal = goal;
            _formationRequest = _formationNavigation.RequestCellPath(_plannedStart, goal);
            if (_formationRequest == 0) _candidateIndex--; // Queue pressure is temporary.
            return;
        }
        if (_candidateIndex >= _candidates.Count) FinishFormationActor(false);
    }

    private void FormationPathCompleted(long id, Godot.Collections.Array<Vector2I> path, string reason)
    {
        if (id != _formationRequest || _formation.Count == 0) return;
        _formationRequest = 0;
        var (actor, revision) = _formation.Peek();
        if (actor is null || !FormationActorValid(actor, revision) || !GodotObject.IsInstanceValid(_grid))
        { FinishFormationActor(false); return; }
        // An obsolete preflight must never dispatch a command from stale connectivity.
        if (reason == "navigation_changed" || _grid!.WorldToCell(actor.Body!.GlobalPosition) != _plannedStart)
        { FinishFormationActor(false); return; }
        if (reason.Length > 0 || path.Count == 0) return;
        using var command = new ActorCommand { IssuerId = _issuer, Recipients = new() { actor.ActorId },
            Action = ActorAction.Move, TargetCell = _plannedGoal, Append = _append };
        ulong version = _formationVersion;
        bool accepted = _player!.Registry!.Submit(command) == 1;
        if (version != _formationVersion) return;
        if (accepted)
        {
            _assigned.Add(_plannedGoal);
            // Only our own enqueue may advance the queue's revision. A listener that
            // stopped or reordered the actor during Submit must invalidate later plans.
            ulong expectedRevision = revision + (_append ? 1UL : 2UL);
            if (actor.CommandRevision == expectedRevision)
                foreach (var plan in _queuedFormations)
                    foreach (var member in plan.Members)
                        if (member.Actor == actor && member.Revision == revision) member.Revision = expectedRevision;
        }
        FinishFormationActor(accepted);
    }

    private void FinishFormationActor(bool accepted)
    {
        _formation.Dequeue();
        if (accepted) _dispatched++; else _rejected++;
        _candidateIndex = 0;
        if (_formation.Count > 0) return;
        int dispatched = _dispatched, rejected = _rejected;
        ResetCurrentFormation();
        if (_queuedFormations.TryDequeue(out var next)) StartFormation(next);
        EmitSignal(SignalName.FormationFinished, dispatched, rejected);
    }
}
