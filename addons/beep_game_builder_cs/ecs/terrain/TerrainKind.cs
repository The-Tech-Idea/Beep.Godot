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
    }
}
