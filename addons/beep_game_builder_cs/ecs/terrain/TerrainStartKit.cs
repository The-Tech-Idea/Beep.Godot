using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// What a playable start needs, per scenario: room for a headquarters footprint on level
    /// ground, ways out of it, space from the neighbouring start, and the resources every
    /// player is guaranteed. The workflow review's E01 asks exactly this - "playable starts
    /// need a headquarters footprint, clear exits, and reachable essential resources ...
    /// report an unusable seed, never silently reroll it" - and every field is read by
    /// <see cref="TerrainStartAreaStage"/> or <see cref="TerrainStartPositionStage"/>.
    ///
    /// A generator with a start area radius and no kit uses these defaults with no entries.
    /// Like <see cref="ResourceCatalog"/>, editing a kit already assigned to a generator does
    /// not invalidate a field it has built; generate again.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainStartKit : Resource
    {
        /// <summary>
        /// The headquarters footprint, anchored at the start cell the way
        /// <see cref="GridPlacementComponent"/> anchors a footprint. A start is only chosen
        /// where every footprint cell is in bounds, dry, startable, not mountainous and level.
        /// </summary>
        [Export] public Vector2I HqFootprint { get; set; } = new(3, 3);

        /// <summary>Area cells that must touch the footprint's sides, so it is not walled in.</summary>
        [Export(PropertyHint.Range, "0,16,1")] public int ExitCount { get; set; } = 2;

        /// <summary>Cells of unreserved ground kept between two start areas.</summary>
        [Export(PropertyHint.Range, "0,8,1")] public int AreaGap { get; set; } = 1;

        /// <summary>Smallest usable area, in cells. Zero means 60% of the radius disc.</summary>
        [Export(PropertyHint.Range, "0,4096,1")] public int MinAreaCells { get; set; }

        /// <summary>
        /// The resources the kit places, in this order: every PerPlayer entry for each start, then every
        /// Neutral entry between the starts once all starts have their own (FEAT-14).
        /// </summary>
        [Export] public Godot.Collections.Array<TerrainStartKitEntry> Entries { get; set; } = new();
    }
}
