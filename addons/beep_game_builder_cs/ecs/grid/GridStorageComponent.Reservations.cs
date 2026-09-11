using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridStorageComponent
{
    private sealed record MaterialClaim(Node Owner, Dictionary<string, int> Amounts, Action Exit);
    private readonly Dictionary<ulong, MaterialClaim> _materialClaims = new();
    // Canonical GridIds.Normalize keys, matching _stored and the TryTotals cost keys (DUP-14).
    private readonly Dictionary<string, int> _reservedMaterials = new();

    public int Reserved(string resourceId)
        => _reservedMaterials.GetValueOrDefault(GridIds.Normalize(resourceId));

    public int Available(string resourceId) => Math.Max(0, Stored(resourceId) - Reserved(resourceId));

    /// <summary>Replaces this owner's complete claim atomically; stock stays in storage.</summary>
    public bool TryReserveMaterials(Node owner, Godot.Collections.Array amounts)
    {
        foreach (Variant entry in amounts)
            if (!GridResourceAmount.TryRead(entry, out _, out int amount) || amount < 0) return false;
        if (!GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree() || !IsInsideTree()
            || owner.GetTree() != GetTree() || !GridResourceAmount.TryTotals(amounts, out var totals)) return false;
        ulong key = owner.GetInstanceId();
        _materialClaims.TryGetValue(key, out var previous);
        foreach (var (id, amount) in totals)
            if ((long)Available(id) + (previous?.Amounts.GetValueOrDefault(id) ?? 0) < amount) return false;
        ReleaseMaterials(owner);
        if (totals.Count == 0) return true;
        Action exit = () => ReleaseMaterials(owner);
        _materialClaims.Add(key, new(owner, totals, exit));
        foreach (var (id, amount) in totals) _reservedMaterials[id] = Reserved(id) + amount;
        owner.TreeExiting += exit;
        return true;
    }

    public void ReleaseMaterials(Node owner)
    {
        if (!GodotObject.IsInstanceValid(owner) || !_materialClaims.Remove(owner.GetInstanceId(), out var claim)) return;
        claim.Owner.TreeExiting -= claim.Exit;
        foreach (var (id, amount) in claim.Amounts)
        {
            int remaining = Reserved(id) - amount;
            if (remaining == 0) _reservedMaterials.Remove(id);
            else _reservedMaterials[id] = remaining;
        }
    }

    /// <summary>Consumes all of an owner's promised materials before notifying observers.</summary>
    public bool TryConsumeReserved(Node owner)
    {
        if (!GodotObject.IsInstanceValid(owner) || !_materialClaims.TryGetValue(owner.GetInstanceId(), out var claim)) return false;
        foreach (var (id, amount) in claim.Amounts)
            if (Stored(id) < amount) return false;
        ReleaseMaterials(owner);
        foreach (var (id, amount) in claim.Amounts)
        {
            int remaining = Stored(id) - amount;
            if (remaining == 0) _stored.Remove(id);
            else _stored[id] = remaining;
        }
        foreach (string id in claim.Amounts.Keys)
            EmitSignal(SignalName.StorageChanged, id, Stored(id), CurrentLoad);
        return true;
    }

    private void ClearMaterialClaims()
    {
        foreach (var claim in _materialClaims.Values)
            if (GodotObject.IsInstanceValid(claim.Owner)) claim.Owner.TreeExiting -= claim.Exit;
        _materialClaims.Clear();
        _reservedMaterials.Clear();
    }
}
