using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>
/// One start-kit placement: the resource, the cell (generator-local), and how far its
/// constraints had to be relaxed to fit - 0 as authored, 1 the distance band widened to the
/// whole area, 2 the same-resource spacing dropped, 3 a Bonus resource overwritten.
/// </summary>
internal readonly record struct TerrainStartAreaPlacement(string ResourceId, Vector2I Cell, int Relaxation);

/// <summary>
/// What the start-area stage built for one start, and why it is or is not playable.
/// Unusable starts stay in the start positions: dropping them would hide the fact the
/// workflow review requires reported ("report an unusable seed, never silently reroll it").
/// </summary>
internal sealed record TerrainStartAreaReport(
    int Index,
    Vector2I Origin,
    Vector2I Footprint,
    int CellCount,
    int Exits,
    IReadOnlyList<TerrainStartAreaPlacement> Placements,
    IReadOnlyList<string> Problems)
{
    /// <summary>Problem prefix for a non-critical kit shortfall - the only problem a usable start may carry.</summary>
    public const string MissingPrefix = "missing:";

    /// <summary>True unless a problem other than a non-critical kit shortfall was found.</summary>
    public bool Usable
    {
        get
        {
            foreach (string problem in Problems)
                if (!problem.StartsWith(MissingPrefix, System.StringComparison.Ordinal)) return false;
            return true;
        }
    }

    /// <summary>The report as GDScript reads it. Cells are generator-local, as GetStartPositions returns them.</summary>
    public Godot.Collections.Dictionary ToDictionary()
    {
        var placements = new Godot.Collections.Array();
        foreach (TerrainStartAreaPlacement placement in Placements)
            placements.Add(new Godot.Collections.Dictionary
            {
                ["resource"] = placement.ResourceId,
                ["cell"] = placement.Cell,
                ["relaxation"] = placement.Relaxation,
            });
        var problems = new Godot.Collections.Array<string>();
        foreach (string problem in Problems) problems.Add(problem);
        return new Godot.Collections.Dictionary
        {
            ["index"] = Index,
            ["origin"] = Origin,
            ["footprint"] = Footprint,
            ["cell_count"] = CellCount,
            ["exits"] = Exits,
            ["placements"] = placements,
            ["problems"] = problems,
            ["usable"] = Usable,
        };
    }
}
