using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class ActorRegistryComponent
{
    private readonly Dictionary<string, Node> _dormantMotionOwners = new();

    internal bool ClaimDormantMotion(string actorId, Node owner)
    {
        if (!CanClaimDormantMotion(actorId, owner)) return false;
        _dormantMotionOwners[actorId] = owner;
        return true;
    }

    internal bool CanClaimDormantMotion(string actorId, Node owner) =>
        _dormant.ContainsKey(actorId) && GodotObject.IsInstanceValid(owner) && owner.IsInsideTree()
        && (!_dormantMotionOwners.TryGetValue(actorId, out var previous)
            || !GodotObject.IsInstanceValid(previous) || !previous.IsInsideTree() || previous == owner);

    internal void ReleaseDormantMotion(string actorId, Node owner)
    {
        if (_dormantMotionOwners.TryGetValue(actorId, out var previous) && previous == owner) _dormantMotionOwners.Remove(actorId);
    }

    internal bool MoveDormantActor(string actorId, Vector2 position, Node owner)
    {
        if (!position.IsFinite() || !_dormant.TryGetValue(actorId, out var record)
            || !_dormantMotionOwners.TryGetValue(actorId, out var previous) || previous != owner) return false;
        record["x"] = position.X;
        record["y"] = position.Y;
        UpdateSpatialPosition(actorId, position);
        return true;
    }
}
