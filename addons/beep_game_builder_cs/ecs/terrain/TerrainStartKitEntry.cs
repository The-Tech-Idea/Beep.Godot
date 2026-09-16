using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// One resource every player start must have within reach - the per-player object
    /// kit of Age of Empires' player lands and 0 A.D.'s bases: the same set, at matched
    /// distances, for every start. <see cref="TerrainStartAreaStage"/> places it inside
    /// the start's reserved area, relaxing its constraints in a fixed order when the
    /// ground will not take it, and reports every placement and every shortfall.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainStartKitEntry : Resource
    {
        /// <summary>The catalog id to place - the same id the map writes and the wallet counts.</summary>
        [Export] public string ResourceId { get; set; } = "";

        /// <summary>How many to place per start (surface), or deposits to stamp (underground).</summary>
        [Export(PropertyHint.Range, "1,8,1")] public int Count { get; set; } = 1;

        /// <summary>Nearest a placement may be to the start, in cells.</summary>
        [Export(PropertyHint.Range, "0,32,1")] public int MinDistance { get; set; } = 2;

        /// <summary>Farthest a placement may be from the start, in cells. Zero means the area radius.</summary>
        [Export(PropertyHint.Range, "0,32,1")] public int MaxDistance { get; set; } = 0;

        /// <summary>
        /// A start without this entry is unusable, and is reported as such. A non-critical
        /// shortfall is reported but leaves the start usable.
        /// </summary>
        [Export] public bool Critical { get; set; }
    }
}
