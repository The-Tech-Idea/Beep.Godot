using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class ActorRegistryComponent
{
    private readonly record struct FollowRest(string Target, ulong Revision, Rect2 Bounds, Rect2I Buckets, bool Indexed);
    private readonly Dictionary<ActorComponent, FollowRest> _followRests = new();
    private readonly Dictionary<Vector2I, HashSet<ActorComponent>> _followBuckets = new();
    private readonly HashSet<ActorComponent> _largeFollowRests = new();
    private readonly HashSet<ActorComponent> _followCandidates = new();
    private readonly List<ActorComponent> _staleFollowRests = new();
    public int FollowRestReservationCount => _followRests.Count;
    public int FollowRestBucketCount => _followBuckets.Count;
    public int LastFollowRestCandidateCount { get; private set; }

    private static bool TryFollowBuckets(Rect2 bounds, out Rect2I buckets)
    {
        buckets = default;
        if (!bounds.Position.IsFinite() || !bounds.End.IsFinite()) return false;
        double left = System.Math.Floor(bounds.Position.X / 256.0), top = System.Math.Floor(bounds.Position.Y / 256.0);
        double right = System.Math.Floor(bounds.End.X / 256.0), bottom = System.Math.Floor(bounds.End.Y / 256.0);
        double width = right - left + 1, height = bottom - top + 1;
        // Oversized bounds use one overflow entry instead of allocating an unbounded grid.
        if (left < int.MinValue || top < int.MinValue || right >= int.MaxValue || bottom >= int.MaxValue
            || width < 1 || height < 1 || width * height > 64) return false;
        buckets = new((int)left, (int)top, (int)width, (int)height);
        return true;
    }

    internal static Rect2 FollowBounds(ActorComponent actor, Vector2 position)
    {
        Vector2 size = actor.Definition?.Footprint ?? new Vector2(20, 16);
        if (!size.IsFinite()) size = new(20, 16);
        size = size.Abs().Max(Vector2.One) + Vector2.One * 4;
        return new(position - size * 0.5f, size);
    }

    internal void ReleaseFollowRest(ActorComponent actor)
    {
        if (!_followRests.Remove(actor, out var rest)) return;
        if (!rest.Indexed) { _largeFollowRests.Remove(actor); return; }
        for (int y = rest.Buckets.Position.Y; y < rest.Buckets.End.Y; y++)
            for (int x = rest.Buckets.Position.X; x < rest.Buckets.End.X; x++)
                if (_followBuckets.TryGetValue(new(x, y), out var bucket))
                {
                    bucket.Remove(actor);
                    if (bucket.Count == 0) _followBuckets.Remove(new(x, y));
                }
    }

    internal bool HasFollowRest(ActorComponent actor, Vector2 position)
        => _followRests.TryGetValue(actor, out var rest) && rest.Revision == actor.CommandRevision
            && rest.Target == actor.FollowTargetActorId && rest.Bounds == FollowBounds(actor, position);

    internal bool TryClaimFollowRest(ActorComponent actor, ActorComponent leader, Vector2 position)
    {
        LastFollowRestCandidateCount = 0;
        if (!position.IsFinite() || leader.Body is null || actor.Body is null) return false;
        Rect2 bounds = FollowBounds(actor, position);
        if (!bounds.Position.IsFinite() || !bounds.End.IsFinite()) return false;
        if (bounds.Intersects(FollowBounds(leader, leader.Body.GlobalPosition))) return false;
        bool indexed = TryFollowBuckets(bounds, out Rect2I buckets);
        _followCandidates.Clear();
        if (indexed)
        {
            for (int y = buckets.Position.Y; y < buckets.End.Y; y++)
                for (int x = buckets.Position.X; x < buckets.End.X; x++)
                    if (_followBuckets.TryGetValue(new(x, y), out var bucket)) _followCandidates.UnionWith(bucket);
            _followCandidates.UnionWith(_largeFollowRests);
        }
        else _followCandidates.UnionWith(_followRests.Keys);
        // These are disposable destinations, not saved actor identities or a second party roster.
        bool available = true;
        foreach (var other in _followCandidates)
        {
            LastFollowRestCandidateCount++;
            var rest = _followRests[other];
            if (!GodotObject.IsInstanceValid(other) || FindActor(other.ActorId) != other || !other.IsActive
                || other.IsDead || other.CommandRevision != rest.Revision || other.FollowTargetActorId != rest.Target)
            {
                _staleFollowRests.Add(other);
                continue;
            }
            if (other != actor && bounds.Intersects(rest.Bounds)) available = false;
        }
        foreach (var stale in _staleFollowRests) ReleaseFollowRest(stale);
        _staleFollowRests.Clear();
        _followCandidates.Clear();
        if (!available) return false;
        ReleaseFollowRest(actor);
        _followRests[actor] = new(leader.ActorId, actor.CommandRevision, bounds, buckets, indexed);
        if (!indexed) _largeFollowRests.Add(actor);
        else
            for (int y = buckets.Position.Y; y < buckets.End.Y; y++)
                for (int x = buckets.Position.X; x < buckets.End.X; x++)
                {
                    var cell = new Vector2I(x, y);
                    if (!_followBuckets.TryGetValue(cell, out var bucket)) _followBuckets[cell] = bucket = new();
                    bucket.Add(actor);
                }
        return true;
    }
}
