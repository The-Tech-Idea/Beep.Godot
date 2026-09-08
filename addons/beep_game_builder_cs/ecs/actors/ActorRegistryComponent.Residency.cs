using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

public partial class ActorRegistryComponent
{
    private readonly Dictionary<string, Godot.Collections.Dictionary> _dormant = new(StringComparer.Ordinal);
    [Signal] public delegate void ActorSleptEventHandler(string actorId);
    [Signal] public delegate void ActorWokeEventHandler(string actorId);
    public int ResidentActorCount => _actors.Count;
    public int DormantActorCount => _dormant.Count;
    public bool IsDormant(string actorId) => _dormant.ContainsKey(actorId);

    public Godot.Collections.Array<string> GetActorIds()
        => new(_actors.Keys.Concat(_dormant.Keys).Order(StringComparer.Ordinal));

    public string GetActorOwner(string actorId) => FindActor(actorId)?.OwnerId
        ?? (_dormant.TryGetValue(actorId, out var record) ? record["owner"].AsString() : "");

    public Vector2 GetActorPosition(string actorId) => FindActor(actorId)?.Body?.GlobalPosition
        ?? (_dormant.TryGetValue(actorId, out var record)
            ? new Vector2(record["x"].AsSingle(), record["y"].AsSingle()) : new Vector2(float.NaN, float.NaN));

    public Godot.Collections.Dictionary GetActorRecord(string actorId)
    {
        if (_dormant.TryGetValue(actorId, out var record)) return record.Duplicate(true);
        if (FindActor(actorId) is not { } actor) return new();
        var snapshot = actor.CaptureActor();
        snapshot["resident"] = true;
        return snapshot;
    }

    public bool TrySleepActor(string actorId)
    {
        var actor = FindActor(actorId);
        if (actor?.Body is not { } body || actor.Definition?.SimulationPolicy != ActorSimulationPolicy.AmbientDormancy
            || !actor.CanBecomeDormant()) return false;
        if (!Definitions.Any(d => d is not null && d.Id == actor.Definition.Id && d.Scene is not null)
            || GetNodeOrNull<Node2D>(ActorsRootPath) is null) return false;
        foreach (var player in _players.Values)
            if (player.PossessedActorId == actorId || player.GetSelectedActors().Contains(actorId) || player.HasPlannedOrder(actorId)) return false;
        if (_actors.Values.Any(other => other.ReferencesActor(actorId))) return false;
        // Capture before removing the scene, so exit callbacks cannot erase persisted state.
        var record = actor.CaptureActor();
        record["resident"] = false;
        _dormant.Add(actorId, record);
        UpdatePosition(actor);
        body.GetParent().RemoveChild(body);
        body.QueueFree();
        EmitSignal(SignalName.ActorSlept, actorId);
        return true;
    }

    public ActorComponent? WakeActor(string actorId)
    {
        if (FindActor(actorId) is { } live) return live;
        if (!_dormant.Remove(actorId, out var record)) return null;
        ActorComponent? actor = null;
        try
        {
            actor = SpawnActor(record["definition"].AsString(), record["owner"].AsString(),
                new(record["x"].AsSingle(), record["y"].AsSingle()), actorId);
            if (actor is null) { _dormant.Add(actorId, record); return null; }
            actor.RestoreActor(record);
        }
        catch
        {
            _dormant[actorId] = record;
            if (actor?.Body is { } body)
            {
                body.GetParent().RemoveChild(body);
                body.QueueFree();
            }
            throw;
        }
        EmitSignal(SignalName.ActorWoke, actorId);
        return actor;
    }

    [Signal] public delegate void ActorDestroyedEventHandler(string actorId);

    public bool RemoveActor(string actorId) => RemoveActorCore(actorId, permanent: true);

    private bool RemoveActorCore(string actorId, bool permanent)
    {
        if (FindActor(actorId) is { Body: { } body } actor)
        {
            UnregisterActor(actor);
            body.GetParent().RemoveChild(body);
            body.QueueFree();
            if (permanent) EmitSignal(SignalName.ActorDestroyed, actorId);
            return true;
        }
        if (!_dormant.Remove(actorId)) return false;
        RemoveSpatialEntry(actorId);
        foreach (var player in _players.Values) player.ForgetActor(actorId);
        EmitSignal(SignalName.ActorRemoved, actorId);
        if (permanent) EmitSignal(SignalName.ActorDestroyed, actorId);
        return true;
    }

    private void RemoveSpatialEntry(string actorId)
    {
        if (!_actorCells.Remove(actorId, out var cell) || !_spatial.TryGetValue(cell, out var bucket)) return;
        bucket.Remove(actorId);
        if (bucket.Count == 0) _spatial.Remove(cell);
    }
}
