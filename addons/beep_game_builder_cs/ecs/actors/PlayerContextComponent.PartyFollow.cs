using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

public partial class PlayerContextComponent
{
    private bool _followPartyOnPossession;
    [Export] public bool FollowPartyOnPossession
    {
        get => _followPartyOnPossession;
        set
        {
            if (_followPartyOnPossession == value) return;
            _followPartyOnPossession = value;
            if (Engine.IsEditorHint()) return;
            if (!value) StopPartyFollowing();
            else if (IsInsideTree()) ApplyPartyFollowing();
        }
    }
    private float _partyFollowDistance = 64;
    [Export(PropertyHint.Range, "1,65536,1")] public float PartyFollowDistance
    {
        get => _partyFollowDistance;
        set => _partyFollowDistance = float.IsFinite(value) ? Mathf.Clamp(value, 1, 65536) : 64;
    }
    private readonly Dictionary<string, (ActorComponent Actor, ulong Revision)> _partyFollowing = new(StringComparer.Ordinal);
    private readonly HashSet<string> _partyHeld = new(StringComparer.Ordinal);
    private ulong _partyVersion;

    private bool OwnsPartyOrder(string id, (ActorComponent Actor, ulong Revision) record)
        => GodotObject.IsInstanceValid(Registry) && Registry!.FindActor(id) == record.Actor
            && record.Actor.OwnerId == PlayerId && record.Actor.CommandRevision == record.Revision
            && record.Actor.FollowTargetActorId.Length > 0;

    private void StopPartyFollowing()
    {
        ulong version = ++_partyVersion;
        var previous = _partyFollowing.ToArray();
        _partyFollowing.Clear();
        foreach (var (id, record) in previous)
        {
            if (version != _partyVersion) return;
            if (OwnsPartyOrder(id, record)) record.Actor.CancelOrders();
            else if (Registry?.GetActorOwner(id) == PlayerId) _partyHeld.Add(id);
        }
    }

    /// <summary>Explicitly release manual holds and rebuild following for the current possession group.</summary>
    public int RegroupParty()
    {
        StopPartyFollowing();
        _partyHeld.Clear();
        return ApplyPartyFollowing();
    }

    private int ApplyPartyFollowing()
    {
        StopPartyFollowing();
        if (!FollowPartyOnPossession || ControlMode != PlayerControlMode.Direct || !IsInsideTree()
            || !GodotObject.IsInstanceValid(Registry) || Registry!.FindActor(PossessedActorId) is not { IsDead: false, IsActive: true }) return 0;
        ulong version = _partyVersion;
        var members = PossessionGroup == -1 ? GetOwnedActors() : GetGroupActors(PossessionGroup);
        if (!members.Contains(PossessedActorId)) return 0;
        _partyHeld.Remove(PossessedActorId);
        int accepted = 0;
        foreach (string id in members.Order(StringComparer.Ordinal))
        {
            if (version != _partyVersion) break;
            if (id == PossessedActorId || _partyHeld.Contains(id) || HasPlannedOrder(id)) continue;
            var actor = Registry.WakeActor(id);
            if (version != _partyVersion) break;
            if (actor is not { IsDead: false, IsActive: true } || actor.OwnerId != PlayerId) continue;
            if (!actor.IsAvailableForPartyFollow) { _partyHeld.Add(id); continue; }
            using var command = new ActorCommand { IssuerId = PlayerId, Recipients = new() { id },
                Action = ActorAction.Follow, TargetActorId = PossessedActorId,
                FollowDistance = PartyFollowDistance };
            accepted += Registry.SubmitTracked(command, (member, revision) =>
            {
                // Capture ownership before public command callbacks can replace the new order.
                if (version == _partyVersion) _partyFollowing[id] = (member, revision);
            });
        }
        return accepted;
    }

    private Godot.Collections.Dictionary CapturePartyFollowing()
    {
        var held = new HashSet<string>(_partyHeld, StringComparer.Ordinal);
        var following = new Godot.Collections.Dictionary();
        foreach (var (id, record) in _partyFollowing)
        {
            if (OwnsPartyOrder(id, record)) following[id] = record.Actor.FollowTargetActorId;
            else if (Registry?.GetActorOwner(id) == PlayerId) held.Add(id);
        }
        return new() { ["enabled"] = FollowPartyOnPossession, ["distance"] = PartyFollowDistance,
            ["group"] = PossessionGroup, ["held"] = new Godot.Collections.Array<string>(held.Order(StringComparer.Ordinal)), ["following"] = following };
    }

    private void RestorePartyFollowing(Godot.Collections.Dictionary state)
    {
        ++_partyVersion;
        _partyFollowing.Clear();
        _partyHeld.Clear();
        if (state.Count == 0) return;
        _followPartyOnPossession = state["enabled"].AsBool();
        PartyFollowDistance = state["distance"].AsSingle();
        PossessionGroup = state["group"].AsInt32();
        foreach (var id in state["held"].AsGodotArray())
            if (Registry?.GetActorOwner(id.AsString()) == PlayerId) _partyHeld.Add(id.AsString());
        foreach (var (id, target) in state["following"].AsGodotDictionary())
            if (Registry?.FindActor(id.AsString()) is { } actor && actor.OwnerId == PlayerId
                && actor.FollowTargetActorId == target.AsString() && target.AsString().Length > 0)
                _partyFollowing[id.AsString()] = (actor, actor.CommandRevision);
    }
}
