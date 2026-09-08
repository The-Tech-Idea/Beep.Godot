using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class ActorComponent
{
    internal bool IsAvailableForPartyFollow => !HasOrders
        && GetSiblingComponent<GridWorkerComponent>() is not { CurrentJobId.Length: > 0 }
        && GetSiblingComponent<GridPathFollowerComponent>() is not { IsMoving: true }
        && (GetSiblingComponent<GridHaulerComponent>() is not { } hauler || (!hauler.IsBusy && hauler.StoredIds().Count == 0));
    public string FollowTargetActorId
    {
        get
        {
            var order = _current;
            if (order is null) _orders.TryPeek(out order);
            return order is not null && (ActorAction)order["action"].AsInt32() == ActorAction.Follow
                ? order["target"].AsString() : "";
        }
    }
    private bool _followSettled, _followHasTarget;
    private Vector2 _followTargetPosition;
    private int _followCandidate;

    private bool WouldCreateFollowCycle(string targetId)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(targetId);
        while (pending.TryPop(out string? id))
        {
            if (id == ActorId) return true;
            if (!visited.Add(id) || Registry?.FindActor(id) is not { } actor) continue;
            if (actor._current is { } current && (ActorAction)current["action"].AsInt32() == ActorAction.Follow)
                pending.Push(current["target"].AsString());
            foreach (var order in actor._orders)
                if ((ActorAction)order["action"].AsInt32() == ActorAction.Follow)
                    pending.Push(order["target"].AsString());
        }
        return false;
    }

    private void ResetFollowState()
    {
        if (GodotObject.IsInstanceValid(Registry)) Registry!.ReleaseFollowRest(this);
        _followSettled = _followHasTarget = false;
        _followCandidate = 0;
        _repathDelay = 0;
    }

    private void AdvanceFollow(double delta, GridPathFollowerComponent? follower)
    {
        var target = Registry?.FindActor(_current!["target"].AsString());
        if (target is not { IsActive: true, IsDead: false, Body: { } leader }
            || target == this || target.OwnerId != OwnerId || !leader.GlobalPosition.IsFinite())
        {
            follower?.CancelMove();
            Finish(false, "follow_target_unavailable");
            return;
        }
        if (follower is not { IsActive: true }) { Finish(false, "missing_path_follower"); return; }
        float range = _current!["follow_distance"].AsSingle();
        if (!float.IsFinite(range) || range < 1 || range > 65536)
        {
            follower.CancelMove();
            Finish(false, "invalid_follow_distance");
            return;
        }
        Vector2 offset = Body!.GlobalPosition - leader.GlobalPosition;
        float distance = offset.Length();
        float hysteresis = Mathf.Max(8, range * 0.25f);
        bool near = distance <= range || ((_followSettled || follower.HasReachedDestination) && distance <= range + hysteresis);
        if (near && _followSettled && Registry!.HasFollowRest(this, Body.GlobalPosition)
            && !ActorRegistryComponent.FollowBounds(this, Body.GlobalPosition).Intersects(ActorRegistryComponent.FollowBounds(target, leader.GlobalPosition))) return;
        _repathDelay -= double.IsFinite(delta) && delta > 0 ? delta : 0;
        if (_repathDelay > 0 || follower.IsPathPending) return;
        if (near && Registry!.TryClaimFollowRest(this, target, Body.GlobalPosition))
        {
            if (follower.IsMoving) follower.CancelMove();
            _followSettled = true;
            return;
        }
        _followSettled = false;
        // Preserve in-flight searches, including terrain demand, instead of restarting every frame.
        bool moved = !_followHasTarget || _followTargetPosition.DistanceSquaredTo(leader.GlobalPosition) > hysteresis * hysteresis;
        if (follower.IsMoving && !moved) return;
        _repathDelay = 0.5;
        _followCandidate = moved ? 0 : (_followCandidate + 1) % 16;
        _followTargetPosition = leader.GlobalPosition;
        _followHasTarget = true;
        // Alternate approach directions after a failed/insufficient route; never move directly through terrain.
        Vector2 direction = distance > 0.001f ? offset / distance : Vector2.Right;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            int candidate = (_followCandidate + attempt) % 16;
            Vector2 requested = leader.GlobalPosition + direction.Rotated(candidate * Mathf.Tau / 16) * (range * 0.9f);
            if (!follower.TryProjectFollowDestination(requested, out Vector2 goal)
                || goal.DistanceTo(leader.GlobalPosition) > range + hysteresis
                || !Registry!.TryClaimFollowRest(this, target, goal)) continue;
            _followCandidate = candidate;
            if (!follower.MoveToWorld(goal)) Registry.ReleaseFollowRest(this);
            return;
        }
        follower.CancelMove();
        Registry!.ReleaseFollowRest(this);
    }
}
