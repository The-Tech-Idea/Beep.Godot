namespace Beep.ECS
{
    /// <summary>
    /// What a site tells you before you commission it: the ground it takes, what
    /// it costs in material, and how long it takes in turns. Three facts, asked
    /// one way, so a build menu, a tooltip, a planner or an AI reads them from
    /// one contract instead of knowing each definition type by name.
    ///
    /// A SITE is something you commission onto the grid that OCCUPIES CELLS.
    /// That is the whole test, and it is deliberately narrow:
    ///
    /// - A build definition is a site. So is the placed build site it becomes.
    /// - A production recipe and an extractor are PROCESSES HOSTED IN a site.
    ///   They declare turns and materials and take their footprint from the
    ///   GridObjectComponent they sit on, which is what GridExtractorComponent
    ///   already does.
    /// - A storage hold, a road and a resource node are NOT sites. Storage has
    ///   capacity and its host's area; a road is per-cell and is commissioned
    ///   through an ordinary 1x1 build; a resource node is found, not built.
    ///   Giving them invented 1x1 footprints would buy uniformity by making the
    ///   contract mean less.
    ///
    /// Read it through GridSitePorts, never by casting - a GDScript definition
    /// or a plain Dictionary answers the same three questions by name.
    /// </summary>
    public interface IGridSite
    {
        /// <summary>Cells the site occupies, width by height. At least 1x1.</summary>
        Godot.Vector2I SiteFootprint { get; }

        /// <summary>
        /// Material that must physically arrive before work starts, as
        /// GridResourceAmount entries. Distinct from a wallet cost, which is
        /// currency spent the instant the build is confirmed.
        /// </summary>
        Godot.Collections.Array SiteMaterials { get; }

        /// <summary>
        /// Turns of work the site takes. A turn is a day: five turns is five
        /// end-turns in a turn-based game and five in-game days in a real-time
        /// one, so the authored number means the same thing on both axes.
        /// </summary>
        int SiteTurns { get; }
    }
}
