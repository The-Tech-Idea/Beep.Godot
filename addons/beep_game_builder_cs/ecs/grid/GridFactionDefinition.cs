using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// One faction, as map and session data: who they are, what colour they draw in, and whether
    /// the map pins them to a particular start.
    ///
    /// This is data, not behaviour. Hostility stays where it already lives, on
    /// <see cref="ActorRegistryComponent"/>, and <see cref="Team"/> here is a number a game may
    /// group by - nothing in the engine reads it as an alliance. OpenRA keeps the same split: its
    /// Players block carries name, faction, colour, team and LockSpawn, and the rules decide what
    /// any of it means.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridFactionDefinition : Resource
    {
        /// <summary>The faction's id, as PlayerContextComponent.FactionId spells it.</summary>
        [Export] public string FactionId { get; set; } = "";

        /// <summary>What a player is shown. The id is for code; this is for people.</summary>
        [Export] public string DisplayName { get; set; } = "";

        /// <summary>
        /// The one colour this faction is drawn in: minimap tint, start-area border, start ring.
        /// A second colour table is how a map comes to show a player as blue in one view and green
        /// in another.
        /// </summary>
        [Export] public Color Colour { get; set; } = new(0.25f, 0.44f, 0.88f);

        /// <summary>Whether a player may be assigned this faction. False for scenery or neutral sides.</summary>
        [Export] public bool Playable { get; set; } = true;

        /// <summary>A grouping number for a game's own diplomacy. The engine reads it nowhere.</summary>
        [Export(PropertyHint.Range, "0,16,1")] public int Team { get; set; }

        /// <summary>
        /// The start this faction must have, as an index in the generator's start order, or -1 to
        /// let the assignment choose. A scenario that means "the defenders begin in the valley"
        /// says it here rather than relying on the order the catalog happens to be in.
        /// </summary>
        [Export(PropertyHint.Range, "-1,23,1")] public int LockedStart { get; set; } = -1;
    }
}
