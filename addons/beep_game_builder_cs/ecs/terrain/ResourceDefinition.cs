using Godot;

namespace Beep.ECS
{
    /// <summary>What a resource is for, in the Civilization sense.</summary>
    public enum ResourceCategory : byte
    {
        Bonus = 0,
        Luxury = 1,
        Strategic = 2,
    }

    /// <summary>
    /// ONE definition of a resource, for the map and for the game alike.
    ///
    /// It used to be two. The generator had a private record struct listing where
    /// each resource occurs, and the economy had bare strings - a wallet keyed by
    /// "wood", a node whose ResourceId defaulted to "wood". Nothing connected
    /// them, so "iron" on the map and "iron" in the wallet were two unrelated
    /// strings that merely looked alike: the generator could place crude_oil and
    /// the economy had no idea it existed, while the scatter component put wood
    /// nodes wherever it liked.
    ///
    /// Both halves live here because they are facts about the SAME thing. Where a
    /// resource occurs is the map's question; what gathering it yields is the
    /// game's; and an id that answered one but not the other is precisely what
    /// let the two drift apart.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ResourceDefinition : Resource
    {
        [ExportGroup("Identity")]
        /// <summary>
        /// The id the map writes and the wallet counts. One id, deliberately: a
        /// separate "wallet id" would reintroduce the split this type closes.
        /// </summary>
        [Export] public string Id { get; set; } = "iron";

        [Export] public string DisplayName { get; set; } = "Iron";
        [Export] public ResourceCategory Category { get; set; } = ResourceCategory.Strategic;

        [ExportGroup("Where it occurs")]
        /// <summary>
        /// The terrain kinds that would actually produce it - fish in the sea,
        /// deer on tundra, gems in jungle, iron in the hills. Empty means the
        /// generator never places it, which is how a game adds a resource that
        /// only exists as a gathered or refined good.
        /// </summary>
        [Export] public Godot.Collections.Array<string> TerrainKinds { get; set; } = new();

        /// <summary>Relative chance against the other resources on that ground.</summary>
        [Export(PropertyHint.Range, "0,4,0.05")] public float Weight { get; set; } = 1.0f;

        /// <summary>
        /// Restricts it to flat, hills or mountains. Off means any relief the
        /// terrain kinds allow.
        /// </summary>
        [Export] public bool RequiresRelief { get; set; } = false;
        [Export] public TerrainReliefKind RequiredRelief { get; set; } = TerrainReliefKind.Hills;

        [ExportGroup("How it is gathered")]
        /// <summary>How much one deposit holds before it is worked out.</summary>
        [Export(PropertyHint.Range, "1,9999,1")] public int Amount { get; set; } = 8;
        [Export(PropertyHint.Range, "1,9999,1")] public int AmountPerGather { get; set; } = 1;
        [Export(PropertyHint.Range, "0.01,600,0.01")] public float GatherSeconds { get; set; } = 1.5f;
        [Export] public string GatherJobKind { get; set; } = "gather";

        /// <summary>
        /// The node placed where the map says this resource is. Empty means the
        /// resource is on the map but not harvestable - a mineral shown for
        /// planning rather than gathered.
        /// </summary>
        [Export] public PackedScene? NodeScene { get; set; }

        /// <summary>Whether a deposit blocks the cell it stands on.</summary>
        [Export] public bool OccupiesCell { get; set; } = false;
    }

    /// <summary>
    /// Relief, exposed for authoring. The generator's own TerrainRelief is
    /// internal to the pipeline; this is the same three values in a form a
    /// Resource can export.
    /// </summary>
    public enum TerrainReliefKind : byte
    {
        Flat = 0,
        Hills = 1,
        Mountains = 2,
    }
}
