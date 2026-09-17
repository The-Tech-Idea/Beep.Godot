using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>
/// A <see cref="TerrainStartKit"/> detached from Godot, captured on the main thread before
/// generation is dispatched - the same contract as <see cref="TerrainResourceRules"/>. A
/// generation worker never reads the authored resource.
/// </summary>
internal sealed class TerrainStartKitRules
{
    internal readonly record struct Entry(string ResourceId, int Count, int MinDistance, int MaxDistance, bool Critical,
        TerrainStartKitScope Scope);

    public Vector2I HqFootprint { get; }
    public int ExitCount { get; }
    public int AreaGap { get; }
    public int MinAreaCells { get; }
    public IReadOnlyList<Entry> Entries { get; }

    private TerrainStartKitRules(Vector2I footprint, int exits, int gap, int minCells, List<Entry> entries)
    {
        HqFootprint = footprint;
        ExitCount = exits;
        AreaGap = gap;
        MinAreaCells = minCells;
        Entries = entries.AsReadOnly();
    }

    /// <summary>
    /// The kit's rules, or the defaults with no entries when no kit is assigned. An entry whose Scope
    /// is neither PerPlayer nor Neutral - a script or a hand-edited resource can store one - is a
    /// configuration error and throws, naming the entry: both placement passes would skip it, and a
    /// kit entry that is accepted and never placed is exactly the failure that must not be silent.
    /// </summary>
    public static TerrainStartKitRules Capture(TerrainGenerationSettings settings)
    {
        TerrainStartKit kit = settings.StartKit ?? new TerrainStartKit();
        var entries = new List<Entry>();
        foreach (TerrainStartKitEntry? entry in kit.Entries)
        {
            if (entry is null) continue;
            if (!System.Enum.IsDefined(entry.Scope))
                throw new System.InvalidOperationException(
                    $"Start kit entry '{entry.ResourceId}' has scope {(int)entry.Scope}, which is neither PerPlayer nor Neutral.");
            entries.Add(new Entry(entry.ResourceId.Trim(), Mathf.Clamp(entry.Count, 1, 8),
                Mathf.Clamp(entry.MinDistance, 0, 32), Mathf.Clamp(entry.MaxDistance, 0, 32), entry.Critical, entry.Scope));
        }
        var footprint = new Vector2I(Mathf.Clamp(kit.HqFootprint.X, 1, 16), Mathf.Clamp(kit.HqFootprint.Y, 1, 16));
        return new TerrainStartKitRules(footprint, Mathf.Clamp(kit.ExitCount, 0, 16),
            Mathf.Clamp(kit.AreaGap, 0, 8), Mathf.Clamp(kit.MinAreaCells, 0, 4096), entries);
    }
}
