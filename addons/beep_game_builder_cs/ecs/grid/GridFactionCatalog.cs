using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Who the factions are, once, for the whole map.
    ///
    /// A faction's INDEX is its position in this list plus one, which makes it a byte with 0 free
    /// to mean "nobody". That is deliberate and shared: the per-cell owner a territory layer
    /// writes, the plane a fog layer keeps per faction, and the colour the minimap and the start
    /// overlay tint with are all the same number, so a faction cannot be player 2 on the map and
    /// player 3 on the minimap.
    ///
    /// The actors layer keeps its own model - <see cref="PlayerContextComponent"/> carries a
    /// FactionId string and <see cref="ActorRegistryComponent"/> owns hostility - and this does not
    /// replace it. It answers three questions that model cannot: which index a faction is, which
    /// faction an index is, and what colour to draw it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridFactionCatalog : Resource
    {
        /// <summary>The factions, in order. Position + 1 is the index every grid layer uses.</summary>
        [Export] public Godot.Collections.Array<GridFactionDefinition> Factions { get; set; } = new();

        /// <summary>How many factions the catalog holds.</summary>
        public int Count => Factions.Count;

        /// <summary>
        /// The faction at an index, or null when the index names nobody. Index 0 is "nobody" by
        /// construction, so it answers null rather than the first faction.
        /// </summary>
        public GridFactionDefinition? At(int index)
            => index >= 1 && index <= Factions.Count ? Factions[index - 1] : null;

        /// <summary>The index of a faction id, or 0 when the catalog does not hold it.</summary>
        public int IndexOf(string factionId)
        {
            string wanted = GridIds.Normalize(factionId);
            if (wanted.Length == 0) return 0;
            for (int i = 0; i < Factions.Count; i++)
                if (Factions[i] is { } faction && GridIds.Normalize(faction.FactionId) == wanted)
                    return i + 1;
            return 0;
        }

        /// <summary>The faction id at an index, normalised, or empty when the index names nobody.</summary>
        public string IdOf(int index) => At(index) is { } faction ? GridIds.Normalize(faction.FactionId) : "";

        /// <summary>
        /// The colour of an index. Index 0 - nobody - is transparent rather than a colour, so a
        /// caller tinting by owner draws nothing where there is no owner instead of tinting
        /// everything with faction 1's colour.
        /// </summary>
        public Color ColourOf(int index) => At(index)?.Colour ?? new Color(0f, 0f, 0f, 0f);

        /// <summary>Whether a player may be assigned this index.</summary>
        public bool PlayableAt(int index) => At(index)?.Playable ?? false;

        /// <summary>
        /// The start this index must have, or -1 when it may take any. Out-of-range indices answer
        /// -1: an index that names nobody constrains nothing.
        /// </summary>
        public int LockedStartOf(int index) => At(index)?.LockedStart ?? -1;

        /// <summary>
        /// One tint rule, so every view that shades a cell by its owner shades it the same amount.
        /// A tint rather than a replacement: the terrain must still read through it, or a minimap
        /// of owned land stops being a map.
        /// </summary>
        public static Color TintToward(Color source, Color faction, float strength)
            => faction.A <= 0f ? source : source.Lerp(new Color(faction, source.A), Mathf.Clamp(strength, 0f, 1f));
    }
}
