using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

public partial class GridProductionSimulationComponent
{
    [Export] public NodePath ActorRegistryPath { get; set; } = new("");
    private ActorRegistryComponent? _actorRegistry;
    private bool _registryConnected;
    private readonly Dictionary<string, string> _actorLinks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _actorProductionIds = new(StringComparer.Ordinal);

    private void ConnectActorRegistry()
    {
        if (_registryConnected) return;
        if (!ActorRegistryPath.IsEmpty) _actorRegistry = GetNodeOrNull<ActorRegistryComponent>(ActorRegistryPath);
        if (!GodotObject.IsInstanceValid(_actorRegistry) || !_actorRegistry!.IsInsideTree()) return;
        _actorRegistry.ActorDestroyed += OnActorDestroyed;
        _registryConnected = true;
    }

    private void DisconnectActorRegistry()
    {
        if (_registryConnected && GodotObject.IsInstanceValid(_actorRegistry))
            _actorRegistry!.ActorDestroyed -= OnActorDestroyed;
        _registryConnected = false;
    }

    internal bool LinkActor(string productionId, ActorRegistryComponent registry, string actorId)
    {
        if (string.IsNullOrWhiteSpace(productionId) || string.IsNullOrWhiteSpace(actorId)) return false;
        ConnectActorRegistry();
        if (GodotObject.IsInstanceValid(_actorRegistry) && _actorRegistry != registry) return false;
        if (!ActorRegistryPathMatches(registry)) return false;
        if (_actorLinks.TryGetValue(productionId, out var existing) && existing != actorId) return false;
        _actorRegistry = registry;
        if (ActorRegistryPath.IsEmpty) ActorRegistryPath = GetPathTo(registry);
        ConnectActorRegistry();
        RememberActorLink(productionId, actorId);
        if (_records.TryGetValue(productionId, out var entry)) entry.ActorId = actorId;
        return true;
    }

    private bool ActorRegistryPathMatches(ActorRegistryComponent registry)
        => ActorRegistryPath.IsEmpty || GetNodeOrNull<ActorRegistryComponent>(ActorRegistryPath) == registry;

    private void OnActorDestroyed(string actorId)
    {
        if (!_actorProductionIds.TryGetValue(actorId, out var ids)) return;
        foreach (var id in ids.ToArray())
        {
            ForgetActorLink(id);
            RemoveProduction(id, refundInputs: false);
        }
    }

    private void RememberActorLink(string id, string actorId)
    {
        ForgetActorLink(id);
        _actorLinks[id] = actorId;
        if (!_actorProductionIds.TryGetValue(actorId, out var ids)) _actorProductionIds.Add(actorId, ids = new());
        ids.Add(id);
    }

    private void ForgetActorLink(string id)
    {
        if (!_actorLinks.Remove(id, out var actorId) || !_actorProductionIds.TryGetValue(actorId, out var ids)) return;
        ids.Remove(id);
        if (ids.Count == 0) _actorProductionIds.Remove(actorId);
    }

    private void FinishRemoval(string id, Entry entry)
    {
        if (!entry.RemovePending || entry.Process.IsTransitioning || entry.Process.IsAdvancing) return;
        RemoveProduction(id, entry.RefundOnRemoval);
    }
}
