using Godot;

namespace Beep.ECS
{
    /// <summary>Whose a start-kit entry is.</summary>
    public enum TerrainStartKitScope
    {
        /// <summary>Every start gets Count of it, inside its own area - the per-player kit.</summary>
        PerPlayer,

        /// <summary>
        /// Count per start, placed BETWEEN the starts: outside every area and its gap, where the two
        /// nearest starts are about equally far - Age of Empires' contested gold between player lands.
        /// </summary>
        Neutral,
    }

    /// <summary>
    /// One resource the start kit promises - the per-player object kit of Age of Empires' player
    /// lands and 0 A.D.'s bases (the same set, at matched distances, for every start), or with
    /// <see cref="Scope"/> Neutral the contested sites between them. <see cref="TerrainStartAreaStage"/>
    /// places it, relaxing its constraints in a fixed order when the ground will not take it, and
    /// reports every placement and every shortfall.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainStartKitEntry : Resource
    {
        /// <summary>The catalog id to place - the same id the map writes and the wallet counts.</summary>
        [Export] public string ResourceId { get; set; } = "";

        /// <summary>
        /// How many to place per start (surface), or deposits to stamp (underground). A Neutral entry
        /// places Count for every start, so a four-player map gets four times as many.
        /// </summary>
        [Export(PropertyHint.Range, "1,8,1")] public int Count { get; set; } = 1;

        /// <summary>Nearest a placement may be to the start - to the NEAREST start, for a Neutral entry - in cells.</summary>
        [Export(PropertyHint.Range, "0,32,1")] public int MinDistance { get; set; } = 2;

        /// <summary>
        /// Farthest a placement may be from the start, in cells. Zero means the area radius; for a
        /// Neutral entry, zero means no upper bound inside the band between the starts.
        /// </summary>
        [Export(PropertyHint.Range, "0,32,1")] public int MaxDistance { get; set; } = 0;

        /// <summary>
        /// A start without this entry is unusable, and is reported as such. A non-critical
        /// shortfall is reported but leaves the start usable. A Neutral entry's shortfall is
        /// reported on the neutral-site report and never makes a start unusable - it belongs to none.
        /// </summary>
        [Export] public bool Critical { get; set; }

        /// <summary>Whether every start gets this in its own area, or it goes between them (FEAT-14).</summary>
        [Export] public TerrainStartKitScope Scope { get; set; } = TerrainStartKitScope.PerPlayer;
    }
}
