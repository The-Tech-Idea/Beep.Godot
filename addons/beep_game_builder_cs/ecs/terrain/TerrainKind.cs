using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// One terrain kind's meaning - what "grass" or "rock" IS, independent of the string id that
    /// names it. The generator and the views read a kind's properties from here instead of from a
    /// dozen private tables, so a kind (or a game's new kind) is defined in one place. Properties are
    /// folded in one at a time as each table that used to own them is retired (DUP-13); the string
    /// ids themselves never change - cells, saves and shaders still speak "grass".
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainKind : Resource
    {
        /// <summary>The kind's string id, e.g. "grass". This is what cells and saves store.</summary>
        [Export] public string Id { get; set; } = "";

        /// <summary>The generated ground class - Land, Water (deep/shallow water) or Steep (rock,
        /// lava): land that is not level enough to cross. Independent of an agent's movement policy;
        /// it is what the cell IS. An unknown kind is Land, the same permissive default the hardcoded
        /// <see cref="TerrainTileSets.GroundOf"/> gave a kind it did not name.</summary>
        [Export] public TerrainTileSets.Ground Class { get; set; } = TerrainTileSets.Ground.Land;

        /// <summary>The terrain-layer level this kind sits at when its relief is unknown, as a
        /// <see cref="TerrainLayers"/> level (Sea/Ground/Hills/Mountains). Water is Sea, gravel is
        /// Hills, rock is Mountains; every other kind (lava included) is flat Ground.</summary>
        [Export] public int Level { get; set; } = TerrainLayers.Ground;

        /// <summary>Whether a start position may be placed on this kind. Snow, ice, rock and lava are
        /// not startable; every other land kind is (water and mountain-relief cells are excluded by
        /// separate checks, not by kind).</summary>
        [Export] public bool Startable { get; set; } = true;

        /// <summary>Whether a small region of this kind may be DISSOLVED into its neighbours when it is
        /// too small for the landmass (the biome-coherence absorb pass). The rainfall grounds plus snow
        /// and tundra; not sand, water, or a peak/steep material.</summary>
        [Export] public bool Absorbable { get; set; }

        /// <summary>Whether an absorbed region may BECOME this kind. The absorbable kinds plus rock and
        /// gravel (so a dissolved snow cap surrounded by rock has somewhere to go); never sand, which is
        /// the coast-placed beach.</summary>
        [Export] public bool AbsorbTarget { get; set; }

        /// <summary>Whether this kind is a PEAK material - what high ground is made of (rock, gravel,
        /// snow). Never valid at sea level: stone at the shore is not a biome.</summary>
        [Export] public bool PeakMaterial { get; set; }

        /// <summary>Whether this kind must NOT be chosen as a drained lake bed - sand (the coast beach)
        /// and the peak materials - so a drained lake becomes the surrounding ground, not a beach or a
        /// mountain in the middle of the map.</summary>
        [Export] public bool NotLakeBed { get; set; }

        /// <summary>Whether the grid blocks building, roading, spawning and scattering on this kind by
        /// default (the build-side default the placement/spawn/scatter components inherit). The water
        /// kinds and lava; overridable per component. This is the BUILD default, distinct from an
        /// agent's movement policy - navigation keeps its own blocked list so units can wade shallows
        /// at a cost while nothing may be built there.</summary>
        [Export] public bool BlockedByDefault { get; set; }

        /// <summary>Which prop palette the scatter places on this kind - "grass", "desert", "mud",
        /// "rock", "water", or "" for no props (deep water, lava). Groups kinds that share a look:
        /// grass/dry_grass/jungle all take the grass palette, rock/gravel/snow/ice/tundra the rock one.
        /// "water" is placed only where a scatter opts in (its AllowShallowWaterProps), so the sea and
        /// lakes stay clear by default.</summary>
        [Export] public string PropPalette { get; set; } = "";

        /// <summary>The painted view's material slot for this kind - the index the splat shader
        /// (terrain_splat.gdshader) uses to pick this kind's texture. Two kinds may share a slot (mud
        /// draws with swamp's material). This is the shader's OWN material ordering, distinct from the
        /// catalog's tile-index order; a game that adds a kind either reuses an existing slot or extends
        /// the shader's material array to match.</summary>
        [Export] public int MaterialSlot { get; set; }

        /// <summary>Which terrain FEATURE this kind carries, as a layer over the base terrain - "jungle",
        /// "marsh", "oasis", "woods" (grass/dry_grass/tundra are woods-capable), or "" for none. Jungle,
        /// marsh and (a rare) oasis are placed unconditionally by kind; "woods" only marks the kind as
        /// woods-CAPABLE - whether a woods-capable cell actually grows trees is decided by the feature
        /// stage's temperature floor, moisture ranking and Forest-vs-Woods density, which stay there.</summary>
        [Export] public string FeatureEligibility { get; set; } = "";
    }
}
